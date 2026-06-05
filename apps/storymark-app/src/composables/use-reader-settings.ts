import { computed, reactive, readonly, watch } from "vue";
import type { ComputedRef, DeepReadonly } from "vue";
import { useEventListener } from "@vueuse/core";
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

// Hand-rolled instead of vueuse's useFullscreen: Android can drop fullscreen in
// the background without ever delivering fullscreenchange, and useFullscreen
// only resyncs its cached state on that event — after which its enter() refuses
// while the cache still reads fullscreen. document.fullscreenElement is always
// current, so it is the only state we consult.
function fullscreenActive(): boolean {
  return document.fullscreenElement !== null;
}

async function enterFullscreen(): Promise<void> {
  await document.documentElement.requestFullscreen();
}

// requestFullscreen() only succeeds under transient user activation, so an
// automatic request (on load, or on returning to the app) is rejected; when
// that happens we arm a listener and enter on the reader's next tap/keypress.
// It stays armed across rejected attempts — an Esc keydown, for one, carries no
// activation — and disarms only once fullscreen is restored or no longer wanted.
let armed = false;
function enterFullscreenOnGesture(): void {
  if (armed) return;
  armed = true;
  const stop = useEventListener(window, ["pointerdown", "keydown"], () => {
    if (!settings.fullscreen || fullscreenActive()) {
      armed = false;
      stop();
      return;
    }
    void (async () => {
      try {
        await enterFullscreen();
        armed = false;
        stop();
      } catch {
        // No activation on this gesture; stay armed for the next one.
      }
    })();
  });
}

// Fullscreen is a reader behaviour: restoring it (and re-entering after OS
// drops) must not happen just because the app booted with a stored
// `fullscreen: true` — App.vue loads settings on every route for the theme.
// Only a reader-context load() flips this, after which the mirrors below may
// act for the rest of the session (matching pre-boot-load behaviour, where
// fullscreen stayed on when navigating from the reader back to the library).
let fullscreenManaged = false;

function applyFullscreen(want: boolean): void {
  if (!fullscreenManaged) return;
  if (want && !fullscreenActive()) {
    void enterFullscreen().catch(() => {
      enterFullscreenOnGesture();
    });
  } else if (!want && fullscreenActive()) {
    void document.exitFullscreen().catch(() => undefined);
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
// A bfcache restore brings the page back without re-running setup; re-arm the
// mirror below or it would stay dead for the rest of the session.
useEventListener(window, "pageshow", () => {
  unloading = false;
  foregroundedAt = performance.now();
});

// Android force-exits fullscreen when the app is backgrounded, but delivers the
// fullscreenchange late — after the app is foregrounded again — or not at all.
// So an OS-forced exit can't be told from a user one by visibility at event
// time. Two signals mark it instead: the fullscreen state already being gone
// when the app comes back (observed below, before any event), or the event
// arriving right after the return. A deliberate exit (back gesture, Esc) comes
// while the user has visibly been in the app.
const FORCED_EXIT_GRACE_MS = 1000;
let foregroundedAt = 0;
let observedForcedExit = false;

function isForcedExit(): boolean {
  return (
    observedForcedExit ||
    document.visibilityState === "hidden" ||
    performance.now() - foregroundedAt < FORCED_EXIT_GRACE_MS
  );
}

// Mirror the real fullscreen state back to the stored preference so leaving via
// Esc or the browser chrome keeps the toggle truthful. An OS-forced exit is not
// the user changing their mind: keep the preference and arm the gesture
// fallback instead, so the first tap back in re-enters.
useEventListener(document, "fullscreenchange", () => {
  const active = fullscreenActive();
  if (active) {
    observedForcedExit = false;
  }
  if (unloading) return;
  if (!active && settings.fullscreen && isForcedExit()) {
    observedForcedExit = false;
    enterFullscreenOnGesture();
    return;
  }
  if (settings.fullscreen !== active) {
    settings.fullscreen = active;
  }
});

// Coming back from an app switch: don't wait for the exit event — compare the
// preference against the real state directly. A divergence means the OS dropped
// fullscreen while hidden, however its event delivery turns out; flag it so a
// late event isn't mirrored as the user turning fullscreen off. The re-entry
// attempt has no transient activation and is rejected, which arms the gesture
// fallback — the first tap back in restores fullscreen.
useEventListener(document, "visibilitychange", () => {
  if (document.visibilityState !== "visible") return;
  foregroundedAt = performance.now();
  if (settings.fullscreen && !fullscreenActive()) {
    observedForcedExit = true;
  }
  applyFullscreen(settings.fullscreen);
});

interface UseReaderSettings {
  readonly settings: DeepReadonly<ReaderSettings>;
  readonly cssVars: ComputedRef<Record<string, string>>;
  load: (context: "app" | "reader") => Promise<void>;
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
  async function load(context: "app" | "reader"): Promise<void> {
    if (context === "reader") {
      fullscreenManaged = true;
    }
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
