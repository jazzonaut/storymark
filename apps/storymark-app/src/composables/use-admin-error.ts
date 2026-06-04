import { useToast } from "primevue/usetoast";
import { AdminApiError } from "@/api/admin-api";
import { useAdmin } from "@/composables/use-admin";

/** The first validation message carried by a 400's FastEndpoints error envelope
 * (`{ errors: { generalErrors: [...] } }`), or null for any other error. Identity
 * failures (password too short, duplicate username, …) arrive this way. */
function validationMessage(error: unknown): string | null {
  if (!(error instanceof AdminApiError) || error.status !== 400) {
    return null;
  }
  const body: unknown = error.body;
  if (typeof body !== "object" || body === null || !("errors" in body)) {
    return null;
  }
  const errors: unknown = body.errors;
  if (typeof errors !== "object" || errors === null || !("generalErrors" in errors)) {
    return null;
  }
  const general: unknown = errors.generalErrors;
  if (Array.isArray(general)) {
    const first: unknown = general[0];
    if (typeof first === "string") {
      return first;
    }
  }
  return null;
}

/** Shared admin request-error handling: 401 signs out and prompts re-entry;
/// anything else surfaces a toast. Call from a component setup. */
export function useAdminError(): {
  report: (error: unknown) => void;
  reportWithValidation: (error: unknown, summary: string) => void;
} {
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

  /** Like `report`, but a 400 carrying a validation message becomes a warn toast
   * with the actual message under `summary`. */
  function reportWithValidation(error: unknown, summary: string): void {
    const message = validationMessage(error);
    if (message !== null) {
      toast.add({ severity: "warn", summary, detail: message, life: 5000 });
      return;
    }
    report(error);
  }

  return { report, reportWithValidation };
}
