using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Services.Books;

namespace Storymark.Api.Endpoints.Books;

/// <summary>The shared HTTP mapping for owner-gated book mutations (ADR-0023):
/// not-visible is a 404, visible-but-not-owned a 403. Success responses differ
/// per endpoint, so only the failure half lives here.</summary>
internal static class OwnedBookOutcomeResponses
{
    /// <summary>Sends the failure response when the outcome is one. Returns
    /// <c>true</c> if a response was sent (the endpoint must return).</summary>
    public static async Task<bool> TrySendErrorAsync(
        this OwnedBookOutcome outcome, HttpResponse response, CancellationToken ct)
    {
        switch (outcome)
        {
            case OwnedBookOutcome.NotFound:
                await response.SendNotFoundAsync(ct);
                return true;
            case OwnedBookOutcome.NotOwned:
                await response.SendForbiddenAsync(ct);
                return true;
            default:
                return false;
        }
    }
}
