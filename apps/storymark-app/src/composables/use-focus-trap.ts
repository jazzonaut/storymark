import { nextTick, onBeforeUnmount, watch } from "vue";
import type { Ref } from "vue";

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(",");

/** Makes an overlay behave like a modal dialog for keyboard and screen-reader
 * users: when `active` flips true it moves focus into `container`, keeps Tab/Shift+Tab
 * cycling within it, and restores focus to the previously-focused element when it
 * flips false (or the component unmounts). Pair with `role="dialog"`/`aria-modal`
 * on the container. Use only for genuinely modal (scrim-blocking) overlays. */
export function useFocusTrap(active: Ref<boolean>, container: Ref<HTMLElement | null>): void {
  let restoreTo: HTMLElement | null = null;

  function focusables(): HTMLElement[] {
    const root = container.value;
    if (!root) {
      return [];
    }
    return [...root.querySelectorAll<HTMLElement>(FOCUSABLE)].filter(
      (el) => el.getClientRects().length > 0,
    );
  }

  function onKeydown(event: KeyboardEvent): void {
    if (event.key !== "Tab") {
      return;
    }
    const root = container.value;
    if (!root) {
      return;
    }
    const items = focusables();
    const first = items[0];
    const last = items[items.length - 1];
    if (first === undefined || last === undefined) {
      // Nothing focusable inside: keep focus pinned to the container itself.
      event.preventDefault();
      root.focus();
      return;
    }
    const current = document.activeElement;
    const outside = !(current instanceof Node) || !root.contains(current);
    if (event.shiftKey && (current === first || outside)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && (current === last || outside)) {
      event.preventDefault();
      first.focus();
    }
  }

  function activate(): void {
    restoreTo = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    document.addEventListener("keydown", onKeydown, true);
    void nextTick(() => {
      (focusables()[0] ?? container.value)?.focus();
    });
  }

  function deactivate(): void {
    document.removeEventListener("keydown", onKeydown, true);
    restoreTo?.focus();
    restoreTo = null;
  }

  // immediate so an overlay that mounts already-open (parent `v-if`, e.g. the recap
  // card) still traps; for always-mounted-but-closed panels this is a no-op.
  watch(
    active,
    (isActive) => {
      if (isActive) {
        activate();
      } else {
        deactivate();
      }
    },
    { immediate: true },
  );

  onBeforeUnmount(() => {
    document.removeEventListener("keydown", onKeydown, true);
    restoreTo?.focus();
  });
}
