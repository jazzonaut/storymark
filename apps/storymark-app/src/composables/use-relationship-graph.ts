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

/** One graph edge: a relationship between two visible entities. `source`/`target`
 * are entity ids (matched to {@link GraphNode.entityId}). */
export interface GraphEdge {
  readonly source: string;
  readonly target: string;
  readonly relationshipType: string;
  readonly polarity: string | null;
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
// node. Degree is computed only over the kept edges.
function build(
  entities: readonly StoryGuideEntity[],
  relationships: readonly Relationship[],
): { nodes: readonly GraphNode[]; edges: readonly GraphEdge[] } {
  const known = new Set(entities.map((entity) => entity.entityId));
  const degree = new Map<string, number>();
  const keptEdges: GraphEdge[] = [];
  for (const relationship of relationships) {
    if (!known.has(relationship.fromEntityId) || !known.has(relationship.toEntityId)) {
      continue;
    }
    keptEdges.push({
      source: relationship.fromEntityId,
      target: relationship.toEntityId,
      relationshipType: relationship.relationshipType,
      polarity: relationship.polarity,
    });
    degree.set(relationship.fromEntityId, (degree.get(relationship.fromEntityId) ?? 0) + 1);
    degree.set(relationship.toEntityId, (degree.get(relationship.toEntityId) ?? 0) + 1);
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
