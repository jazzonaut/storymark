using System.Security.Claims;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Reads the reader's identity off the cookie principal. Reader
/// endpoints are secure-by-default, so an authenticated principal always
/// carries the claims — an absence is a programming error, not a 401.</summary>
internal static class ReaderClaims
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated principal lacks a user id claim."));

    public static string UserName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Name)
            ?? throw new InvalidOperationException("Authenticated principal lacks a name claim.");
}
