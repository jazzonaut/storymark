using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Storymark.Api.Domain.Users;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>The reader cookie ticket: what goes into it at sign-in and how it is
/// revalidated afterwards. The ticket carries the account's security stamp so a
/// password set/change/reset — which rotates the stamp — invalidates every other
/// session instead of leaving stolen or stale cookies valid for the full 30-day
/// sliding window (ADR-0023).</summary>
internal static class ReaderCookie
{
    private const string SecurityStampClaimType = "storymark.security_stamp";

    /// <summary>Mirrors Identity's SecurityStampValidator cadence: the stamp is
    /// checked against the store only once the ticket is older than this, then the
    /// ticket is renewed — one user lookup per session per interval, not per request.</summary>
    private static readonly TimeSpan StampValidationInterval = TimeSpan.FromMinutes(30);

    public static async Task SignInAsync(
        HttpContext httpContext, UserManager<StorymarkUser> users, StorymarkUser user)
    {
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.RequiredUserName));
        identity.AddClaim(new Claim(SecurityStampClaimType, await users.GetSecurityStampAsync(user)));

        // IsPersistent makes the browser keep the cookie for the full 30-day
        // window instead of dropping it with the session.
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var issuedUtc = context.Properties.IssuedUtc;
        if (issuedUtc is not null && DateTimeOffset.UtcNow - issuedUtc < StampValidationInterval)
        {
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<StorymarkUser>>();
        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);

        // Rejects deleted accounts, rotated stamps, and pre-stamp tickets (no
        // stamp claim) alike; SignOutAsync also clears the dead cookie so the
        // client stops replaying it.
        if (user is null
            || !string.Equals(
                context.Principal?.FindFirstValue(SecurityStampClaimType),
                await users.GetSecurityStampAsync(user),
                StringComparison.Ordinal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        context.ShouldRenew = true;
    }
}
