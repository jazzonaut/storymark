<script setup lang="ts">
import { ref, toRef } from "vue";
import RangeControl from "@/components/reader/RangeControl.vue";
import SegmentedControl from "@/components/reader/SegmentedControl.vue";
import { useFocusTrap } from "@/composables/use-focus-trap";
import { useReaderSettings } from "@/composables/use-reader-settings";
import type { EntityHighlightMode, ReaderFontFamily, ReaderTheme } from "@/db/storymark-db";

const props = defineProps<{ open: boolean }>();

defineEmits<{ (e: "close"): void }>();

const { settings, update, reset } = useReaderSettings();

const panel = ref<HTMLElement | null>(null);
useFocusTrap(toRef(props, "open"), panel);

const THEMES: readonly { value: ReaderTheme; label: string }[] = [
  { value: "light", label: "Light" },
  { value: "sepia", label: "Sepia" },
  { value: "dark", label: "Dark" },
];

const FONTS: readonly { value: ReaderFontFamily; label: string }[] = [
  { value: "serif", label: "Serif" },
  { value: "sans", label: "Sans" },
  { value: "mono", label: "Mono" },
];

const HIGHLIGHTS: readonly { value: EntityHighlightMode; label: string }[] = [
  { value: "off", label: "Off" },
  { value: "subtle", label: "Subtle" },
  { value: "always", label: "Always" },
];
</script>

<template>
  <div v-if="open" class="absolute inset-0 z-30">
    <div class="absolute inset-0 bg-black/30" @click="$emit('close')"></div>
    <section
      ref="panel"
      role="dialog"
      aria-modal="true"
      aria-label="Reader settings"
      tabindex="-1"
      class="absolute inset-x-0 bottom-0 max-h-[85vh] overflow-y-auto rounded-t-2xl bg-[var(--sm-bg)] p-5 shadow-2xl outline-none sm:inset-y-0 sm:right-0 sm:left-auto sm:w-80 sm:max-h-none sm:rounded-none"
    >
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-base font-semibold">Reader settings</h2>
        <button
          type="button"
          class="rounded px-2 py-1 text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)]"
          @click="$emit('close')"
        >
          Done
        </button>
      </div>

      <div class="space-y-5">
        <div>
          <p class="mb-1 text-sm font-medium">Theme</p>
          <SegmentedControl
            :model-value="settings.theme"
            :options="THEMES"
            @update:model-value="(value) => update('theme', value)"
          />
        </div>

        <div>
          <p class="mb-1 text-sm font-medium">Font</p>
          <SegmentedControl
            :model-value="settings.fontFamily"
            :options="FONTS"
            @update:model-value="(value) => update('fontFamily', value)"
          />
        </div>

        <RangeControl
          label="Font size"
          :model-value="settings.fontSizePx"
          :min="14"
          :max="28"
          :step="1"
          unit="px"
          @update:model-value="(value) => update('fontSizePx', value)"
        />

        <RangeControl
          label="Line height"
          :model-value="settings.lineHeight"
          :min="1.2"
          :max="2.2"
          :step="0.05"
          :fraction-digits="2"
          @update:model-value="(value) => update('lineHeight', value)"
        />

        <RangeControl
          label="Paragraph spacing"
          :model-value="settings.paragraphSpacingEm"
          :min="0"
          :max="2"
          :step="0.1"
          unit="em"
          :fraction-digits="1"
          @update:model-value="(value) => update('paragraphSpacingEm', value)"
        />

        <RangeControl
          label="Content width"
          :model-value="settings.contentWidthPx"
          :min="480"
          :max="960"
          :step="20"
          unit="px"
          @update:model-value="(value) => update('contentWidthPx', value)"
        />

        <div>
          <p class="mb-1 text-sm font-medium">Entity highlight</p>
          <SegmentedControl
            :model-value="settings.entityHighlight"
            :options="HIGHLIGHTS"
            @update:model-value="(value) => update('entityHighlight', value)"
          />
        </div>

        <div class="flex items-center justify-between">
          <p class="text-sm font-medium">Story-so-far recaps</p>
          <button
            type="button"
            role="switch"
            :aria-checked="settings.recapCardOnSectionEntry"
            class="relative h-6 w-11 shrink-0 rounded-full transition-colors"
            :class="
              settings.recapCardOnSectionEntry ? 'bg-[var(--sm-accent)]' : 'bg-[var(--sm-muted)]/30'
            "
            @click="update('recapCardOnSectionEntry', !settings.recapCardOnSectionEntry)"
          >
            <span
              class="absolute top-0.5 left-0.5 size-5 rounded-full bg-white transition-transform"
              :class="settings.recapCardOnSectionEntry ? 'translate-x-5' : 'translate-x-0'"
            ></span>
          </button>
        </div>

        <button
          type="button"
          class="text-sm text-[var(--sm-accent)] hover:underline"
          @click="reset"
        >
          Reset to defaults
        </button>
      </div>
    </section>
  </div>
</template>
