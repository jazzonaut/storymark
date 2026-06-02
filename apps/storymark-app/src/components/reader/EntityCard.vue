<script setup lang="ts">
import type { SelectionEntityCard } from "@storymark/shared-types";
import EntityTypeBadge from "@/components/reader/EntityTypeBadge.vue";

defineProps<{ card: SelectionEntityCard }>();
</script>

<template>
  <div>
    <div class="flex items-baseline gap-2">
      <span class="text-sm font-semibold text-[var(--sm-fg)]">{{ card.canonicalName }}</span>
      <EntityTypeBadge :type="card.entityType" />
    </div>
    <p v-if="card.firstSeenSection !== null" class="mt-1 text-xs text-[var(--sm-muted)]">
      First seen in section {{ card.firstSeenSection }}
    </p>
  </div>
</template>
