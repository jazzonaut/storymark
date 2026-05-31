import { useToast } from "primevue/usetoast";
import { AdminApiError } from "@/api/admin-api";
import { useAdmin } from "@/composables/use-admin";

/** Shared admin request-error handling: 401 signs out and prompts re-entry;
/// anything else surfaces a toast. Call from a component setup. */
export function useAdminError(): { report: (error: unknown) => void } {
  const toast = useToast();
  const { signOut } = useAdmin();

  function report(error: unknown): void {
    if (error instanceof AdminApiError && error.status === 401) {
      signOut();
      toast.add({
        severity: "warn",
        summary: "Session expired",
        detail: "Re-enter the admin token.",
        life: 4000,
      });
      return;
    }

    toast.add({
      severity: "error",
      summary: "Request failed",
      detail: error instanceof Error ? error.message : "Unexpected error.",
      life: 5000,
    });
  }

  return { report };
}
