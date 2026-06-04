using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Storymark.Api.Domain.Users;
using Storymark.Api.Infrastructure.Auth;

namespace Storymark.Api.Endpoints.Auth;

internal sealed record AuthUserView
{
    public required Guid UserId { get; init; }

    public required string UserName { get; init; }

    public required bool IsAdmin { get; init; }
}

internal sealed record LoginRequest
{
    public required string UserName { get; init; }

    public required string Password { get; init; }
}

/// <summary>Password sign-in. Every failure is the same 401 — lockout, unknown
/// user, not-yet-activated account, and wrong password are deliberately
/// indistinguishable to the caller.</summary>
internal sealed class PostLoginEndpoint(UserManager<StorymarkUser> users)
    : Endpoint<LoginRequest, AuthUserView>
{
    public override void Configure()
    {
        Post("auth/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var user = await users.FindByNameAsync(req.UserName);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        // A null hash is a seeded-but-not-activated account (ADR-0023); it can
        // never sign in, and CheckPasswordAsync would throw on it.
        if (user.PasswordHash is null || !await users.CheckPasswordAsync(user, req.Password))
        {
            await users.AccessFailedAsync(user);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        await users.ResetAccessFailedCountAsync(user);

        await ReaderCookie.SignInAsync(HttpContext, users, user);

        await Send.OkAsync(
            new AuthUserView { UserId = user.Id, UserName = user.RequiredUserName, IsAdmin = user.IsAdmin },
            ct);
    }
}

internal sealed class PostLogoutEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("auth/logout");
        // Anonymous: signing out with an already-dead cookie (expired, or
        // revoked by a stamp rotation) must still clear it — secure-by-default
        // would 401 before SignOutAsync could send the removal Set-Cookie.
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await Send.NoContentAsync(ct);
    }
}

/// <summary>The SPA's session probe: who am I, according to the cookie.</summary>
internal sealed class GetMeEndpoint : EndpointWithoutRequest<AuthUserView>
{
    public override void Configure()
    {
        Get("auth/me");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(
            new AuthUserView { UserId = User.UserId(), UserName = User.UserName(), IsAdmin = User.IsAdmin() },
            ct);
    }
}

internal sealed record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}

internal sealed class PostPasswordEndpoint(UserManager<StorymarkUser> users)
    : Endpoint<ChangePasswordRequest>
{
    public override void Configure()
    {
        Post("auth/password");
    }

    public override async Task HandleAsync(ChangePasswordRequest req, CancellationToken ct)
    {
        // Null only if the account behind a still-valid cookie was deleted.
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        this.ThrowIfFailed(await users.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword));

        // The change rotated the security stamp, which signs out every *other*
        // session at its next stamp check (ReaderCookie) — the point of the
        // rotation. Re-issuing this session's cookie keeps the changer signed in.
        await ReaderCookie.SignInAsync(HttpContext, users, user);

        await Send.NoContentAsync(ct);
    }
}
