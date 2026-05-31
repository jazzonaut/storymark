import { computed, shallowRef } from "vue";
import type { ComputedRef } from "vue";

// Not yet in the DOM lib; modelled minimally so we avoid `any`.
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  readonly userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

const deferredPrompt = shallowRef<BeforeInstallPromptEvent | null>(null);

window.addEventListener("beforeinstallprompt", (event) => {
  // Suppress the mini-infobar so we can offer install from our own UI instead.
  event.preventDefault();
  deferredPrompt.value = event as BeforeInstallPromptEvent;
});
window.addEventListener("appinstalled", () => {
  deferredPrompt.value = null;
});

interface UsePwaInstall {
  readonly canInstall: ComputedRef<boolean>;
  promptInstall: () => Promise<void>;
}

export function usePwaInstall(): UsePwaInstall {
  const canInstall = computed<boolean>(() => deferredPrompt.value !== null);

  async function promptInstall(): Promise<void> {
    const prompt = deferredPrompt.value;
    if (!prompt) {
      return;
    }
    // Clear up front so a second click can't fire the (single-use) prompt twice;
    // the browser only honours one prompt() per gathered event.
    deferredPrompt.value = null;
    await prompt.prompt();
    await prompt.userChoice;
  }

  return { canInstall, promptInstall };
}
