import { ref, shallowRef } from "vue";
import type { Ref, ShallowRef } from "vue";
import type { Relationship, StoryGuideEntity } from "@storymark/shared-types";
import { relationshipsApi } from "@/api/relationships-api";
import { entitiesApi } from "@/api/entities-api";
import { ApiError } from "@/api/api-error";
import type { BookVersion } from "@/db/storymark-db";
import { useLatestOnly } from "@/composables/use-latest-only";

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
const isLoading = ref(false);
const error = ref<string | null>(null);
const isStale = ref(false);
// See useTimeline: a load commits only if it is still the latest, so a slow earlier
// fetch can't overwrite the current boundary's graph.
const latest = useLatestOnly();

interface UseRelationshipGraph {
  readonly nodes: ShallowRef<readonly GraphNode[]>;
  readonly edges: ShallowRef<readonly GraphEdge[]>;
  readonly isLoading: Ref<boolean>;
  readonly error: Ref<string | null>;
  readonly isStale: Ref<boolean>;
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

export function useRelationshipGraph(): UseRelationshipGraph {
  async function load(
    bookId: string,
    section: number,
    location: number,
    version: BookVersion | null,
    canCache: boolean,
  ): Promise<void> {
    const token = latest.begin();
    isLoading.value = true;
    error.value = null;
    try {
      // Relationships are the edges; the entity list supplies the nodes (and the
      // names) — both spoiler-filtered to the same position.
      const [relationships, entities] = await Promise.all([
        relationshipsApi.list(bookId, section, location, version, canCache),
        entitiesApi.list(bookId, section, location, version, canCache),
      ]);
      if (!latest.isCurrent(token)) {
        return;
      }
      const built = build(entities.value, relationships.value);
      nodes.value = built.nodes;
      edges.value = built.edges;
      isStale.value =
        relationships.status === "stale-offline" || entities.status === "stale-offline";
    } catch (caught) {
      if (!latest.isCurrent(token)) {
        return;
      }
      error.value =
        caught instanceof ApiError
          ? "Couldn't load the relationship graph just now."
          : "The relationship graph is unavailable offline.";
      nodes.value = [];
      edges.value = [];
      isStale.value = false;
    } finally {
      if (latest.isCurrent(token)) {
        isLoading.value = false;
      }
    }
  }

  return { nodes, edges, isLoading, error, isStale, load };
}
