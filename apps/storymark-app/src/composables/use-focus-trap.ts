import { watch } from "vue";
import type { Ref } from "vue";
import { onKeyStroke } from "@vueuse/core";
import { useFocusTrap as useTrap } from "@vueuse/integrations/useFocusTrap";

/** Makes an overlay behave like a modal dialog for keyboard and screen-reader
 * users: when `active` flips true it moves focus into `container`, keeps Tab/Shift+Tab
 * cycling within it, and restores focus to the previously-focused element when it
 * flips false (or the component unmounts). Pair with `role="dialog"`/`aria-modal`
 * on the container. Use only for genuinely modal (scrim-blocking) overlays.
 *
 * Pass `onClose` to get Escape-to-close: it fires only while `active`, so an
 * always-mounted-but-closed panel won't swallow Escape from whatever is on screen.
 *
 * Thin wrapper over @vueuse/integrations' focus-trap so call sites stay decoupled
 * from the library (see ADR 0019). */
export function useFocusTrap(
  active: Ref<boolean>,
  container: Ref<HTMLElement | null>,
  onClose?: () => void,
): void {
  const { activate, deactivate } = useTrap(container, {
    immediate: false,
    // Outside-tap dismissal stays with the overlay (@click.self on the scrim), and
    // Escape is owned by our onClose below — so the focus-trap library must neither
    // swallow Escape nor block the scrim click.
    escapeDeactivates: false,
    allowOutsideClick: true,
    // The dialogs carry tabindex="-1"; if nothing inside is tabbable, pin focus to
    // the container rather than let focus-trap throw.
    fallbackFocus: () => container.value ?? document.body,
  });

  if (onClose) {
    onKeyStroke("Escape", () => {
      if (active.value) {
        onClose();
      }
    });
  }

  // flush:post so the overlay's element is in the DOM before activate; immediate so
  // an overlay that mounts already-open (e.g. the recap card) still traps.
  watch(
    active,
    (isActive) => {
      if (isActive) {
        activate();
      } else {
        deactivate();
      }
    },
    { immediate: true, flush: "post" },
  );
}
