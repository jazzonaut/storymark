import { readonly, ref } from "vue";
import type { Ref } from "vue";
import { useEventListener } from "@vueuse/core";

/** Height in CSS px by which the on-screen keyboard overlaps the layout viewport,
 * 0 when hidden. Modern mobile browsers shrink only the visual viewport for the
 * keyboard, so `dvh`-sized, bottom-anchored UI sits behind it unless offset by
 * this amount. Listeners are removed when the calling component unmounts. */
export function useKeyboardInset(): Readonly<Ref<number>> {
  const inset = ref(0);
  const viewport = window.visualViewport;
  if (viewport === null) {
    return readonly(inset);
  }

  const update = (): void => {
    // Pinch-zoom also shrinks the visual viewport; only an unzoomed shrink is the
    // keyboard. offsetTop subtracts any panning the browser already did to reveal
    // the focused field.
    inset.value =
      viewport.scale === 1
        ? Math.max(0, Math.round(window.innerHeight - viewport.height - viewport.offsetTop))
        : 0;
  };

  useEventListener(viewport, "resize", update);
  useEventListener(viewport, "scroll", update);
  return readonly(inset);
}
