import { shallowRef } from "vue";
import type { ShallowRef } from "vue";
import type { Relationship, StoryGuideEntity } from "@storymark/shared-types";
import { relationshipsApi } from "@/api/relationships-api";
import { entitiesApi } from "@/api/entities-api";
import type { BookVersion } from "@/db/storymark-db";
import { describeLoadError, useCachedLoad } from "@/composables/use-cached-load";
import type { CachedLoadState } from "@/composables/use-cached-load";

/** One graph node: a visible entity. `degree` (number of incident relationships)
 * sizes the node — mention count isn't on the entity DTO yet. */
export interface GraphNode {
  readonly entityId: string;
  readonly name: string;
  readonly entityType: string;
  readonly degree: number;
}

/** One distinct relationship folded into a merged {@link GraphEdge}. Reciprocal
 * copies of the same type collapse to one entry: evidence comes from the first
 * copy that has it, polarity only when the copies that state one agree (null on
 * disagreement, same rule as the edge-level polarity). */
export interface EdgeRelationship {
  readonly relationshipType: string;
  readonly polarity: string | null;
  readonly evidence: string | null;
}

/** One graph edge: all relationships between two visible entities, merged.
 * `source`/`target` are entity ids (matched to {@link GraphNode.entityId}) with
 * their display names alongside, so the edge card needs no id→name join;
 * `relationshipType` is the joined edge label, `polarity` the agreed-on sentiment
 * (null when the merged relationships disagree), and `relationships` the per-type
 * details behind them. */
export interface GraphEdge {
  readonly source: string;
  readonly target: string;
  readonly sourceName: string;
  readonly targetName: string;
  readonly relationshipType: string;
  readonly polarity: string | null;
  readonly relationships: readonly EdgeRelationship[];
}

/** Per-type accumulator while merging a pair's relationships: every stated polarity
 * is collected (not first-wins) so disagreement between reciprocal copies is still
 * visible when the {@link EdgeRelationship} is built. */
interface PendingRelationship {
  readonly relationshipType: string;
  readonly polarities: string[];
  evidence: string | null;
}

// Shared by the edge line and the per-type card dot, so the two can't contradict
// each other: a polarity renders only when every copy that states one agrees.
function agreedPolarity(polarities: readonly string[]): string | null {
  const distinct = new Set(polarities);
  return distinct.size === 1 ? ([...distinct][0] ?? null) : null;
}

// Module-level: one reader (one graph) is open at a time.
const nodes = shallowRef<readonly GraphNode[]>([]);
const edges = shallowRef<readonly GraphEdge[]>([]);
const loader = useCachedLoad();
const { isLoading, isRefreshing, error, fallback } = loader;

interface UseRelationshipGraph extends CachedLoadState {
  readonly nodes: ShallowRef<readonly GraphNode[]>;
  readonly edges: ShallowRef<readonly GraphEdge[]>;
  load: (
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ) => Promise<void>;
}

// Edges may reference an entity not in the visible list (a relationship's own
// first-seen can precede an endpoint's); drop those so no link dangles to a missing
// node. Extraction often stores a relationship in both directions (A→B and B→A) —
// rendered separately, the two edges overlap and both midpoint labels show. Same-pair
// relationships are merged into one edge: distinct types joined into one label, the
// polarity kept only when the merged relationships don't disagree on it, and the
// per-type details (polarity, evidence) retained for the edge tap card. Degree is
// computed over the merged edges.
function build(
  entities: readonly StoryGuideEntity[],
  relationships: readonly Relationship[],
): { nodes: readonly GraphNode[]; edges: readonly GraphEdge[] } {
  const names = new Map(entities.map((entity) => [entity.entityId, entity.canonicalName]));
  const pairs = new Map<
    string,
    {
      source: string;
      target: string;
      sourceName: string;
      targetName: string;
      relationships: PendingRelationship[];
    }
  >();
  for (const relationship of relationships) {
    const { fromEntityId: from, toEntityId: to } = relationship;
    const fromName = names.get(from);
    const toName = names.get(to);
    if (fromName === undefined || toName === undefined) {
      continue;
    }
    const key = from < to ? `${from}\n${to}` : `${to}\n${from}`;
    let pair = pairs.get(key);
    if (pair === undefined) {
      pair = {
        source: from,
        target: to,
        sourceName: fromName,
        targetName: toName,
        relationships: [],
      };
      pairs.set(key, pair);
    }
    let entry = pair.relationships.find(
      (candidate) => candidate.relationshipType === relationship.relationshipType,
    );
    if (entry === undefined) {
      entry = {
        relationshipType: relationship.relationshipType,
        polarities: [],
        evidence: relationship.evidence,
      };
      pair.relationships.push(entry);
    } else {
      entry.evidence ??= relationship.evidence;
    }
    if (relationship.polarity !== null && !entry.polarities.includes(relationship.polarity)) {
      entry.polarities.push(relationship.polarity);
    }
  }
  const degree = new Map<string, number>();
  const keptEdges: GraphEdge[] = [];
  for (const pair of pairs.values()) {
    const merged = pair.relationships.map<EdgeRelationship>((entry) => ({
      relationshipType: entry.relationshipType,
      polarity: agreedPolarity(entry.polarities),
      evidence: entry.evidence,
    }));
    keptEdges.push({
      source: pair.source,
      target: pair.target,
      sourceName: pair.sourceName,
      targetName: pair.targetName,
      relationshipType: merged.map((entry) => entry.relationshipType).join(" · "),
      polarity: agreedPolarity(pair.relationships.flatMap((entry) => entry.polarities)),
      relationships: merged,
    });
    degree.set(pair.source, (degree.get(pair.source) ?? 0) + 1);
    degree.set(pair.target, (degree.get(pair.target) ?? 0) + 1);
  }
  const builtNodes = entities.map<GraphNode>((entity) => ({
    entityId: entity.entityId,
    name: entity.canonicalName,
    entityType: entity.entityType,
    degree: degree.get(entity.entityId) ?? 0,
  }));
  return { nodes: builtNodes, edges: keptEdges };
}

// Structural equality via JSON: safe here because every node/edge is constructed
// in build() above (plus the empty initial/failure arrays), so key order is stable.
function sameGraph(
  a: { readonly nodes: readonly GraphNode[]; readonly edges: readonly GraphEdge[] },
  b: { readonly nodes: readonly GraphNode[]; readonly edges: readonly GraphEdge[] },
): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

export function useRelationshipGraph(): UseRelationshipGraph {
  async function load(
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<void> {
    await loader.run({
      load: async (ctx) => {
        // The graph needs both endpoints to build, so a placeholder is shown only once
        // both have surfaced a boundary-safe cached copy for the *new* position — built
        // from those, never from the previously-rendered graph (which could be ahead of
        // the reader after a backward jump). One alone leaves the skeleton until the
        // fetches resolve.
        let cachedRels: readonly Relationship[] | null = null;
        let cachedEntities: readonly StoryGuideEntity[] | null = null;
        const showPlaceholder = ctx.showCached((built: ReturnType<typeof build>) => {
          // Same skip as the final paint below: the cached graph for the new position
          // is often identical to what's already rendered, and re-assigning would
          // reshuffle the force layout for nothing. The refreshing flip still happens.
          if (!sameGraph(built, { nodes: nodes.value, edges: edges.value })) {
            nodes.value = built.nodes;
            edges.value = built.edges;
          }
        });
        const tryPlaceholder = (): void => {
          if (cachedRels !== null && cachedEntities !== null) {
            showPlaceholder(build(cachedEntities, cachedRels));
          }
        };
        // Relationships are the edges; the entity list supplies the nodes (and the
        // names) — both spoiler-filtered to the same position.
        const [relationships, entities] = await Promise.all([
          relationshipsApi.list(bookId, section, location, version, canCache, (cached) => {
            cachedRels = cached;
            tryPlaceholder();
          }),
          entitiesApi.list(bookId, section, location, version, canCache, (cached) => {
            cachedEntities = cached;
            tryPlaceholder();
          }),
        ]);
        if (!ctx.isCurrent()) {
          return;
        }
        const built = build(entities.value, relationships.value);
        // The fetch usually resolves to the same graph the placeholder (or the
        // previous load) already painted, and a re-assign re-runs the chart's force
        // layout — visibly reshuffling an unchanged graph. Skip identical rebuilds.
        if (!sameGraph(built, { nodes: nodes.value, edges: edges.value })) {
          nodes.value = built.nodes;
          edges.value = built.edges;
        }
        ctx.commit(relationships, entities);
      },
      describeError: describeLoadError(
        "Couldn't load the relationship graph just now.",
        "The relationship graph is unavailable offline.",
      ),
      onFailure: () => {
        nodes.value = [];
        edges.value = [];
      },
    });
  }

  return { nodes, edges, isLoading, isRefreshing, error, fallback, load };
}
