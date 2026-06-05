<script setup lang="ts">
defineProps<{
  title: string;
  description?: string;
}>();
</script>

<template>
  <div class="mb-5 flex flex-wrap items-start justify-between gap-x-4 gap-y-2">
    <div class="min-w-0">
      <h2 class="text-lg font-semibold">{{ title }}</h2>
      <p v-if="description" class="mt-0.5 max-w-2xl text-sm text-muted-color">
        {{ description }}
      </p>
    </div>
    <div class="flex shrink-0 items-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>
