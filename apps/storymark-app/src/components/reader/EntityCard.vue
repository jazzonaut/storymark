<script setup lang="ts">
import type { SelectionEntityCard } from "@storymark/shared-types";

defineProps<{ card: SelectionEntityCard }>();
</script>

<template>
  <div>
    <div class="flex items-baseline gap-2">
      <span class="text-sm font-semibold text-[var(--sm-fg)]">{{ card.canonicalName }}</span>
      <span class="rounded-full bg-[var(--sm-surface)] px-2 py-0.5 text-xs text-[var(--sm-muted)]">
        {{ card.entityType }}
      </span>
    </div>
    <p v-if="card.aliases.length > 0" class="mt-1 text-xs text-[var(--sm-muted)]">
      Also known as: {{ card.aliases.join(", ") }}
    </p>
    <p v-if="card.firstSeenSection !== null" class="mt-1 text-xs text-[var(--sm-muted)]">
      First seen in section {{ card.firstSeenSection }}
    </p>
  </div>
</template>
