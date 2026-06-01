import { computed, reactive, readonly, watch } from "vue";
import type { ComputedRef, DeepReadonly } from "vue";
import { db } from "@/db/storymark-db";
import type { EntityHighlightMode, ReaderFontFamily, ReaderTheme } from "@/db/storymark-db";

export interface ReaderSettings {
  fontFamily: ReaderFontFamily;
  fontSizePx: number;
  lineHeight: number;
  paragraphSpacingEm: number;
  contentWidthPx: number;
  theme: ReaderTheme;
  entityHighlight: EntityHighlightMode;
  recapCardOnSectionEntry: boolean;
}

const DEFAULTS: ReaderSettings = {
  fontFamily: "serif",
  fontSizePx: 19,
  lineHeight: 1.6,
  paragraphSpacingEm: 0.8,
  contentWidthPx: 680,
  theme: "light",
  entityHighlight: "subtle",
  recapCardOnSectionEntry: true,
};

const FONT_STACKS: Record<ReaderFontFamily, string> = {
  serif: 'Georgia, "Iowan Old Style", "Times New Roman", serif',
  sans: 'system-ui, -apple-system, "Segoe UI", Roboto, sans-serif',
  mono: 'ui-monospace, "Cascadia Code", "Courier New", monospace',
  dyslexic: '"OpenDyslexic", system-ui, sans-serif',
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
  }

  function update<K extends keyof ReaderSettings>(key: K, value: ReaderSettings[K]): void {
    settings[key] = value;
  }

  function reset(): void {
    Object.assign(settings, DEFAULTS);
  }

  return { settings: readonly(settings), cssVars, load, update, reset };
}
