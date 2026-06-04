import { useToast } from "primevue/usetoast";
import { AdminApiError } from "@/api/admin-api";

/** The first validation message carried by a 400's FastEndpoints error envelope
 * (`{ errors: { <bucket>: [...] } }`), or null for any other error. Identity
 * failures land in `generalErrors`; `ThrowError(r => r.Prop, …)` keys the bucket
 * by property name instead, so every bucket is searched, not just the first
 * known one. */
function validationMessage(error: unknown): string | null {
  if (!(error instanceof AdminApiError) || error.status !== 400) {
    return null;
  }
  const body: unknown = error.body;
  if (typeof body !== "object" || body === null || !("errors" in body)) {
    return null;
  }
  const errors: unknown = body.errors;
  if (typeof errors !== "object" || errors === null) {
    return null;
  }
  const buckets: unknown[] = Object.values(errors);
  for (const bucket of buckets) {
    if (Array.isArray(bucket)) {
      const first: unknown = bucket[0];
      if (typeof first === "string") {
        return first;
      }
    }
  }
  return null;
}

/** Shared admin request-error handling: a 401 stays silent (the global
 * auth-expired handler is already routing to the login screen — a toast would
 * just flash mid-navigation); anything else surfaces a toast. Call from a
 * component setup. */
export function useAdminError(): {
  report: (error: unknown) => void;
  reportWithValidation: (error: unknown, summary: string) => void;
} {
  const toast = useToast();

  function report(error: unknown): void {
    if (error instanceof AdminApiError && error.status === 401) {
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
