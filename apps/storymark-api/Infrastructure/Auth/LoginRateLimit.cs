namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Names the per-IP throttle on <c>POST /api/auth/login</c> (ADR-0025).
/// Identity lockout is per-account; without a source-address limit an internet
/// caller could spray attempts across accounts — or deliberately lock an
/// account out — at line rate.</summary>
internal static class LoginRateLimit
{
    public const string PolicyName = "auth-login";

    public const int PermitLimit = 10;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}
