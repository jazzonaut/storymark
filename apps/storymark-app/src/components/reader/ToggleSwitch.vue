<script setup lang="ts">
defineProps<{ modelValue: boolean; label: string }>();

const emit = defineEmits<{ (e: "update:modelValue", value: boolean): void }>();
</script>

<template>
  <div class="flex items-center justify-between">
    <p class="text-sm font-medium">{{ label }}</p>
    <button
      type="button"
      role="switch"
      :aria-checked="modelValue"
      class="relative h-6 w-11 shrink-0 rounded-full transition-colors"
      :class="modelValue ? 'bg-[var(--sm-accent)]' : 'bg-[var(--sm-muted)]/30'"
      @click="emit('update:modelValue', !modelValue)"
    >
      <span
        class="absolute top-0.5 left-0.5 size-5 rounded-full bg-white transition-transform"
        :class="modelValue ? 'translate-x-5' : 'translate-x-0'"
      ></span>
    </button>
  </div>
</template>
