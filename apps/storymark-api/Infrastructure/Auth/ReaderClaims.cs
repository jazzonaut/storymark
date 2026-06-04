using System.Security.Claims;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Reads the reader's identity off the cookie principal. Reader
/// endpoints are secure-by-default, so an authenticated principal always
/// carries the claims — an absence is a programming error, not a 401.</summary>
internal static class ReaderClaims
{
    /// <summary>Issued by <c>ReaderCookie.SignInAsync</c> from <c>users.is_admin</c>.
    /// Toggling the flag rotates the account's security stamp, so a ticket can
    /// never carry a stale value past the next stamp check.</summary>
    public const string IsAdminClaimType = "storymark.is_admin";

    /// <summary>The claim as written into a fresh ticket — kept beside
    /// <see cref="IsAdmin"/> so the wire value is defined and parsed in one place.</summary>
    public static Claim IsAdminClaim(bool isAdmin) => new(IsAdminClaimType, isAdmin ? "true" : "false");

    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated principal lacks a user id claim."));

    public static string UserName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name)
            ?? throw new InvalidOperationException("Authenticated principal lacks a name claim.");

    /// <summary>False also for tickets issued before the claim existed — those
    /// sessions are plain readers until their next sign-in.</summary>
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(IsAdminClaimType), "true", StringComparison.Ordinal);
}
