using FastEndpoints;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Gates the admin endpoint group on the session cookie's admin claim
/// (ADR-0024): a signed-in non-admin gets 403. Missing sessions never reach
/// here — <see cref="AdminGroup"/> endpoints are secure-by-default, so the
/// cookie scheme answers 401 first.</summary>
internal sealed class AdminAuthPreProcessor : IGlobalPreProcessor
{
    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        if (!context.HttpContext.User.IsAdmin())
        {
            await context.HttpContext.Response.SendForbiddenAsync(ct);
        }
    }
}
