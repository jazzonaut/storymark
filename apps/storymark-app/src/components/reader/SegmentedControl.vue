<script setup lang="ts" generic="T extends string">
defineProps<{ modelValue: T; options: readonly { value: T; label: string }[] }>();

const emit = defineEmits<{ (e: "update:modelValue", value: T): void }>();
</script>

<template>
  <div class="inline-flex rounded-md border border-[var(--sm-muted)]/30 p-0.5">
    <button
      v-for="option in options"
      :key="option.value"
      type="button"
      class="rounded px-3 py-1 text-sm transition-colors"
      :class="
        option.value === modelValue
          ? 'bg-[var(--sm-accent)] text-white'
          : 'text-[var(--sm-fg)] hover:bg-[var(--sm-surface)]'
      "
      @click="emit('update:modelValue', option.value)"
    >
      {{ option.label }}
    </button>
  </div>
</template>
