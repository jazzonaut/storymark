/** The one way admin tables render an ISO timestamp, so a future format change
 * (fixed date/time style, relative times) lands everywhere at once. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString();
}
