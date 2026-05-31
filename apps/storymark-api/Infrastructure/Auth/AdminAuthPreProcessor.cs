using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

        // Fail closed: a missing token setting means no one can authenticate (401,
        // not a 500), and an empty configured token must never match (otherwise a
        // bare "Authorization: Bearer " header would authenticate as the empty secret).
        var expected = settings.TryGetSnapshot("auth.bearerToken", out var snapshot)
            ? JsonSerializer.Deserialize<string>(snapshot.Value) ?? string.Empty
            : string.Empty;

        if (expected.Length == 0 || string.IsNullOrEmpty(presented) || !FixedTimeEquals(presented, expected))
        {
            await context.HttpContext.Response.SendUnauthorizedAsync(ct);
            return;
        }

        context.HttpContext.Items[FingerprintItemKey] = BearerTokenFingerprint.Of(presented);
    }

    private static string? ExtractToken(string header) =>
        header.StartsWith(Scheme, StringComparison.Ordinal) ? header[Scheme.Length..] : null;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
