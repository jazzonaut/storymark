import { useOnline } from "@vueuse/core";
import type { ShallowRef } from "vue";

// Module-level so every consumer shares one source of truth; the window listeners
// are registered once when the module loads.
const isOnline = useOnline();

interface UseOfflineSync {
  readonly isOnline: Readonly<ShallowRef<boolean>>;
}

export function useOfflineSync(): UseOfflineSync {
  return { isOnline };
}
