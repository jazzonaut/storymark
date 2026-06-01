<script setup lang="ts">
defineProps<{ hint: string }>();

defineEmits<{ (e: "tell-me-more"): void }>();
</script>

<template>
  <div>
    <p class="text-sm whitespace-pre-line text-[var(--sm-fg)]">{{ hint }}</p>
    <button
      type="button"
      class="mt-2 text-xs font-medium text-[var(--sm-accent)] hover:underline"
      @click="$emit('tell-me-more')"
    >
      Tell me more →
    </button>
  </div>
</template>
