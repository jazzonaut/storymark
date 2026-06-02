<script setup lang="ts">
import { ref, toRef } from "vue";
import RangeControl from "@/components/reader/RangeControl.vue";
import SegmentedControl from "@/components/reader/SegmentedControl.vue";
import ToggleSwitch from "@/components/reader/ToggleSwitch.vue";
import CloseButton from "@/components/reader/CloseButton.vue";
import { useClearLocalData, UnsyncedChangesError } from "@/composables/use-clear-local-data";
import { useFocusTrap } from "@/composables/use-focus-trap";
import { useReaderSettings } from "@/composables/use-reader-settings";
import type { EntityHighlightMode, ReaderFontFamily, ReaderTheme } from "@/db/storymark-db";

const props = defineProps<{ open: boolean }>();

const emit = defineEmits<{ (e: "close"): void }>();

const { settings, update, reset } = useReaderSettings();
const { isClearing, clear: clearLocalData } = useClearLocalData();

const panel = ref<HTMLElement | null>(null);
useFocusTrap(toRef(props, "open"), panel, () => emit("close"));

const clearStep = ref<"idle" | "confirm">("idle");
const clearError = ref<string | null>(null);

function startClearConfirm(): void {
  clearError.value = null;
  clearStep.value = "confirm";
}

// On success the page navigates away, so control only returns here on failure.
async function onClearLocalData(): Promise<void> {
  clearError.value = null;
  try {
    await clearLocalData();
  } catch (caught) {
    clearStep.value = "idle";
    clearError.value =
      caught instanceof UnsyncedChangesError
        ? "Some changes haven't synced yet. Reconnect to the server, then try again."
        : "Couldn't clear local data. Please try again.";
  }
}

const THEMES: readonly { value: ReaderTheme; label: string }[] = [
  { value: "light", label: "Light" },
  { value: "sepia", label: "Sepia" },
  { value: "dark", label: "Dark" },
];

const FONTS: readonly { value: ReaderFontFamily; label: string }[] = [
  { value: "serif", label: "Serif" },
  { value: "sans", label: "Sans" },
  { value: "mono", label: "Mono" },
  { value: "dyslexic", label: "Dyslexic" },
  { value: "lexend", label: "Lexend" },
];

const HIGHLIGHTS: readonly { value: EntityHighlightMode; label: string }[] = [
  { value: "off", label: "Off" },
  { value: "default", label: "Default" },
  { value: "subtle", label: "Subtle" },
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
        <CloseButton @close="$emit('close')" />
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

        <RangeControl
          label="Page margins"
          :model-value="settings.pageMarginPx"
          :min="0"
          :max="80"
          :step="4"
          unit="px"
          @update:model-value="(value) => update('pageMarginPx', value)"
        />

        <div>
          <p class="mb-1 text-sm font-medium">Entity highlight</p>
          <SegmentedControl
            :model-value="settings.entityHighlight"
            :options="HIGHLIGHTS"
            @update:model-value="(value) => update('entityHighlight', value)"
          />
        </div>

        <ToggleSwitch
          label="Fullscreen"
          :model-value="settings.fullscreen"
          @update:model-value="(value) => update('fullscreen', value)"
        />

        <ToggleSwitch
          label="Story-so-far recaps"
          :model-value="settings.recapCardOnSectionEntry"
          @update:model-value="(value) => update('recapCardOnSectionEntry', value)"
        />

        <button
          type="button"
          class="text-sm text-[var(--sm-accent)] hover:underline"
          @click="reset"
        >
          Reset to defaults
        </button>

        <div class="border-t border-[var(--sm-muted)]/15 pt-4">
          <p class="mb-1 text-sm font-medium">Local data</p>
          <p class="mb-2 text-xs text-[var(--sm-muted)]">
            Clears books and other content cached on this device. Your settings, bookmarks, and
            reading position are kept — they sync from the server.
          </p>
          <button
            v-if="clearStep === 'idle'"
            type="button"
            class="text-sm text-red-600 hover:underline"
            @click="startClearConfirm"
          >
            Clear cached content
          </button>
          <div v-else class="flex items-center gap-3">
            <span class="text-sm">Clear cached content?</span>
            <button
              type="button"
              class="text-sm font-semibold text-red-600 hover:underline disabled:opacity-50"
              :disabled="isClearing"
              @click="onClearLocalData"
            >
              {{ isClearing ? "Clearing…" : "Clear" }}
            </button>
            <button
              type="button"
              class="text-sm text-[var(--sm-muted)] hover:text-[var(--sm-fg)] disabled:opacity-50"
              :disabled="isClearing"
              @click="clearStep = 'idle'"
            >
              Cancel
            </button>
          </div>
          <p v-if="clearError" class="mt-2 text-xs text-red-600">{{ clearError }}</p>
        </div>
      </div>
    </section>
  </div>
</template>
