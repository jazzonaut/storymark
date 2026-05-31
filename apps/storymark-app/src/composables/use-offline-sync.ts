import { ref } from "vue";
import type { Ref } from "vue";

// Module-level so every consumer shares one source of truth; the listeners are
// registered once when the module loads.
const isOnline = ref(navigator.onLine);

window.addEventListener("online", () => {
  isOnline.value = true;
});
window.addEventListener("offline", () => {
  isOnline.value = false;
});

interface UseOfflineSync {
  readonly isOnline: Ref<boolean>;
}

export function useOfflineSync(): UseOfflineSync {
  return { isOnline };
}
