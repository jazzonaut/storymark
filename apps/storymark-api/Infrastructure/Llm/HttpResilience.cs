using System.Net;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Bounded transient-fault retry for the LLM HTTP clients. A provider that
/// is briefly unavailable (restarting Ollama, a transient 5xx/429, a dropped
/// connection) is retried with a short backoff instead of failing the call on the
/// first blip. The per-call <see cref="HttpClient.Timeout"/> still bounds each
/// attempt; a genuine timeout or caller cancellation is not retried (it surfaces so
/// the recorder logs it). 4xx responses (a missing model is a 404, a bad key a 401)
/// are deterministic and pass straight through.
///
/// <para>The caller passes a <em>factory</em> that builds and sends a fresh request
/// each attempt — an <see cref="HttpRequestMessage"/> cannot be resent, so retrying
/// requires re-issuing it. A full circuit breaker is intentionally omitted: it would
/// need an external resilience library, and at single-operator scale a bounded retry
/// covers the realistic failure (a provider bouncing) without one.</para></summary>
internal static class HttpResilience
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);

    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<CancellationToken, Task<HttpResponseMessage>> send, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await send(ct);
                if (attempt >= MaxAttempts || !IsTransientStatus(response.StatusCode))
                {
                    return response;
                }
            }
            catch (HttpRequestException) when (attempt < MaxAttempts && !ct.IsCancellationRequested)
            {
                // Transport-level failure (connection refused/reset): retry below.
            }

            response?.Dispose();
            await Task.Delay(BaseDelay * attempt, ct);
        }
    }

    private static bool IsTransientStatus(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;
}
