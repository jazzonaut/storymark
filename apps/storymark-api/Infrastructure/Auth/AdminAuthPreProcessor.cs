using FastEndpoints;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Gates the admin endpoint group on the bearer token held in
/// <c>settings.auth.bearerToken</c>, read live so a rotated token takes effect
/// immediately. A single shared operator secret — not per-user identity — so a
/// pre-processor is proportionate (no ASP.NET auth scheme). On success it stashes
/// the token fingerprint for audit columns; on failure it short-circuits 401.</summary>
internal sealed class AdminAuthPreProcessor(ISettingsService settings) : IGlobalPreProcessor
{
    public const string FingerprintItemKey = "admin.tokenFingerprint";

    private const string Scheme = "Bearer ";

    public async Task PreProcessAsync(IPreProcessorContext context, CancellationToken ct)
    {
        var presented = ExtractToken(context.HttpContext.Request.Headers.Authorization.ToString());

        // Fail closed (see AdminToken): a missing/empty configured token matches
        // nothing, so no header can authenticate as an unset secret.
        if (presented is null || !AdminToken.Matches(presented, AdminToken.Current(settings)))
        {
            await context.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        context.HttpContext.Items[FingerprintItemKey] = BearerTokenFingerprint.Of(presented);
    }

    private static string? ExtractToken(string header) =>
        header.StartsWith(Scheme, StringComparison.Ordinal) ? header[Scheme.Length..] : null;
}
