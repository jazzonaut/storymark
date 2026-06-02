import { computed, reactive, readonly, watch } from "vue";
import type { ComputedRef, DeepReadonly } from "vue";
import { useEventListener, useFullscreen } from "@vueuse/core";
import { db } from "@/db/storymark-db";
import type { EntityHighlightMode, ReaderFontFamily, ReaderTheme } from "@/db/storymark-db";

export interface ReaderSettings {
  fontFamily: ReaderFontFamily;
  fontSizePx: number;
  lineHeight: number;
  paragraphSpacingEm: number;
  contentWidthPx: number;
  pageMarginPx: number;
  theme: ReaderTheme;
  entityHighlight: EntityHighlightMode;
  recapCardOnSectionEntry: boolean;
  fullscreen: boolean;
}

const DEFAULTS: ReaderSettings = {
  fontFamily: "serif",
  fontSizePx: 19,
  lineHeight: 1.6,
  paragraphSpacingEm: 0.8,
  contentWidthPx: 680,
  pageMarginPx: 24,
  theme: "light",
  entityHighlight: "default",
  recapCardOnSectionEntry: true,
  fullscreen: false,
};

const FONT_STACKS: Record<ReaderFontFamily, string> = {
  serif: 'Georgia, "Iowan Old Style", "Times New Roman", serif',
  sans: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
  mono: 'ui-monospace, "Cascadia Code", "Courier New", monospace',
  dyslexic: '"OpenDyslexic", system-ui, sans-serif',
  lexend: '"Lexend", system-ui, sans-serif',
};

const settings = reactive<ReaderSettings>({ ...DEFAULTS });
let loaded = false;

function applyTheme(theme: ReaderTheme): void {
  document.documentElement.dataset.theme = theme;
}

// Persist the whole row on every change; settings are tiny and writes are rare
// (a human nudging a slider), so a single put is simpler than field-level diffs.
watch(
  settings,
  (current) => {
    void db.readerSettings.put({ id: "global", ...current });
    applyTheme(current.theme);
  },
  { deep: true },
);

const { isFullscreen, enter: enterFullscreen, exit: exitFullscreen } = useFullscreen();

// requestFullscreen() only succeeds under transient user activation, so an
// automatic request on load is rejected; when that happens we arm a one-shot
// listener and enter on the reader's next tap/keypress instead.
let armed = false;
function enterFullscreenOnGesture(): void {
  if (armed) return;
  armed = true;
  const stop = useEventListener(window, ["pointerdown", "keydown"], () => {
    armed = false;
    stop();
    if (settings.fullscreen && !isFullscreen.value) {
      void enterFullscreen().catch(() => undefined);
    }
  });
}

function applyFullscreen(want: boolean): void {
  if (want && !isFullscreen.value) {
    void enterFullscreen().catch(() => {
      enterFullscreenOnGesture();
    });
  } else if (!want && isFullscreen.value) {
    void exitFullscreen().catch(() => undefined);
  }
}

watch(
  () => settings.fullscreen,
  (want) => {
    applyFullscreen(want);
  },
);

// A reload exits fullscreen as the page tears down; ignore that change so we
// don't persist `false` over the preference the user wants restored next load.
let unloading = false;
useEventListener(window, "pagehide", () => {
  unloading = true;
});

// useFullscreen tracks the real fullscreen state; mirror it back to the stored
// preference so leaving via Esc or the browser chrome keeps the toggle truthful.
watch(isFullscreen, (active) => {
  if (unloading) return;
  if (settings.fullscreen !== active) {
    settings.fullscreen = active;
  }
});

interface UseReaderSettings {
  readonly settings: DeepReadonly<ReaderSettings>;
  readonly cssVars: ComputedRef<Record<string, string>>;
  load: () => Promise<void>;
  update: <K extends keyof ReaderSettings>(key: K, value: ReaderSettings[K]) => void;
  reset: () => void;
}

const cssVars = computed<Record<string, string>>(() => ({
  "--reader-font-family": FONT_STACKS[settings.fontFamily],
  "--reader-font-size": `${String(settings.fontSizePx)}px`,
  "--reader-line-height": String(settings.lineHeight),
  "--reader-paragraph-spacing": `${String(settings.paragraphSpacingEm)}em`,
  "--reader-content-width": `${String(settings.contentWidthPx)}px`,
  "--reader-page-margin": `${String(settings.pageMarginPx)}px`,
}));

export function useReaderSettings(): UseReaderSettings {
  async function load(): Promise<void> {
    if (!loaded) {
      const stored = await db.readerSettings.get("global");
      if (stored) {
        const { id: _id, ...rest } = stored;
        Object.assign(settings, rest);
      }
      loaded = true;
    }
    applyTheme(settings.theme);
    applyFullscreen(settings.fullscreen);
  }

  function update<K extends keyof ReaderSettings>(key: K, value: ReaderSettings[K]): void {
    settings[key] = value;
  }

  function reset(): void {
    Object.assign(settings, DEFAULTS);
  }

  return { settings: readonly(settings), cssVars, load, update, reset };
}
