<script setup lang="ts">
import { computed } from "vue";

const props = defineProps<{
  label: string;
  modelValue: number;
  min: number;
  max: number;
  step: number;
  unit?: string;
  fractionDigits?: number;
}>();

const emit = defineEmits<{ (e: "update:modelValue", value: number): void }>();

const display = computed<string>(
  () => `${props.modelValue.toFixed(props.fractionDigits ?? 0)}${props.unit ?? ""}`,
);

function onInput(event: Event): void {
  if (event.target instanceof HTMLInputElement) {
    emit("update:modelValue", Number(event.target.value));
  }
}
</script>

<template>
  <label class="block">
    <span class="mb-1 flex items-center justify-between text-sm text-[var(--sm-fg)]">
      <span>{{ label }}</span>
      <span class="text-[var(--sm-muted)] tabular-nums">{{ display }}</span>
    </span>
    <input
      type="range"
      class="w-full accent-[var(--sm-accent)]"
      :min="min"
      :max="max"
      :step="step"
      :value="modelValue"
      @input="onInput"
    />
  </label>
</template>
