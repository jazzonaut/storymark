using FastEndpoints;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Users;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Endpoints.Admin.Users;

internal sealed record UserItem
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    /// <summary>False for seeded-but-not-activated accounts (ADR-0023).</summary>
    public required bool HasPassword { get; init; }

    public required bool IsLockedOut { get; init; }

    public required bool IsAdmin { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public static UserItem From(StorymarkUser user) => new()
    {
        Id = user.Id,
        UserName = user.RequiredUserName,
        HasPassword = user.PasswordHash is not null,
        IsLockedOut = user.LockoutEnd > DateTimeOffset.UtcNow,
        IsAdmin = user.IsAdmin,
        CreatedAt = user.CreatedAt,
    };
}

internal sealed class ListUsersEndpoint(StorymarkDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<UserItem>>
{
    public override void Configure()
    {
        Get("users");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Projected server-side so the password hash and security stamps never
        // leave the database for a list view. `UserName!` mirrors
        // RequiredUserName (the column is NOT NULL), which EF can't translate.
        var users = await db.Users.AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserItem
            {
                Id = u.Id,
                UserName = u.UserName!,
                HasPassword = u.PasswordHash != null,
                IsLockedOut = u.LockoutEnd > DateTimeOffset.UtcNow,
                IsAdmin = u.IsAdmin,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);
        await Send.OkAsync(users, ct);
    }
}

internal sealed record CreateUserRequest
{
    public required string UserName { get; init; }

    /// <summary>Omit to create the account not-yet-activated; set the password
    /// later via <c>POST users/{id}/password</c>.</summary>
    public string? Password { get; init; }
}

internal sealed class CreateUserEndpoint(UserManager<StorymarkUser> users)
    : Endpoint<CreateUserRequest, UserItem>
{
    public override void Configure()
    {
        Post("users");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var user = new StorymarkUser
        {
            Id = Guid.NewGuid(),
            UserName = req.UserName,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var result = req.Password is null
            ? await users.CreateAsync(user)
            : await users.CreateAsync(user, req.Password);

        this.ThrowIfFailed(result);
        await Send.OkAsync(UserItem.From(user), ct);
    }
}

internal sealed record SetUserPasswordRequest
{
    public required string Password { get; init; }
}

/// <summary>Operator password set/reset. Also clears any lockout — the
/// household recovery path for "locked myself out".</summary>
internal sealed class SetUserPasswordEndpoint(UserManager<StorymarkUser> users, StorymarkDbContext db)
    : Endpoint<SetUserPasswordRequest>
{
    public override void Configure()
    {
        Post("users/{id}/password");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(SetUserPasswordRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(Route<Guid>("id").ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Remove and add are separate saves through the user store; without the
        // transaction a failed add commits the remove, leaving the account
        // passwordless — unable to sign in at all (AuthEndpoints rejects a null
        // hash). The store shares this scoped DbContext, so the transaction
        // covers every UserManager call here.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (user.PasswordHash is not null)
        {
            this.ThrowIfFailed(await users.RemovePasswordAsync(user));
        }

        this.ThrowIfFailed(await users.AddPasswordAsync(user, req.Password));

        this.ThrowIfFailed(await users.SetLockoutEndDateAsync(user, null));
        this.ThrowIfFailed(await users.ResetAccessFailedCountAsync(user));
        await transaction.CommitAsync(ct);
        await Send.NoContentAsync(ct);
    }
}

internal sealed record SetUserAdminRequest
{
    public required bool IsAdmin { get; init; }
}

/// <summary>Grants or revokes admin (ADR-0024). Refusing to demote the last
/// admin who can sign in keeps the instance administrable — with the shared
/// operator token gone there is no other way in (short of
/// <c>Auth:BootstrapPassword</c> plus a database edit).</summary>
internal sealed class SetUserAdminEndpoint(UserManager<StorymarkUser> users, StorymarkDbContext db)
    : Endpoint<SetUserAdminRequest>
{
    public override void Configure()
    {
        Post("users/{id}/admin");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(SetUserAdminRequest req, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(Route<Guid>("id").ToString());
        if (user is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (user.IsAdmin == req.IsAdmin)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        // Only admins with a password count — the seeded account stays admin but
        // passwordless until activated, and a passwordless "last admin" would
        // leave the instance with no working admin login.
        if (!req.IsAdmin
            && !await db.Users.AnyAsync(u => u.IsAdmin && u.PasswordHash != null && u.Id != user.Id, ct))
        {
            ThrowError(r => r.IsAdmin, "Cannot remove admin from the last admin who can sign in.");
        }

        user.IsAdmin = req.IsAdmin;
        // The stamp rotation persists the flag and ends the account's existing
        // sessions at their next stamp check — a cookie's admin claim is set at
        // sign-in, so without it a session would keep the old role for up to
        // 30 days (ReaderCookie).
        this.ThrowIfFailed(await users.UpdateSecurityStampAsync(user));
        await Send.NoContentAsync(ct);
    }
}
