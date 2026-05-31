/** Thrown on any non-2xx response from the API. Carries the HTTP status so
 * callers can branch (e.g. 401 → sign out, 404 → not found). */
export class ApiError extends Error {
  readonly status: number;

  constructor(status: number, message: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}
