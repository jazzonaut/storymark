using Microsoft.AspNetCore.Identity;
using Storymark.Api.Domain.Users;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Breaks the fresh-database deadlock (ADR-0024): user management
/// lives behind admin endpoints, but the only admin — the seeded account — is
/// passwordless and so cannot sign in. When <c>Auth:BootstrapPassword</c> is
/// configured and that account has no password yet, set it on startup. Once a
/// password exists this never touches the account again, so the config value
/// cannot overwrite a rotated password or resurrect a locked-down install.</summary>
internal static class AdminBootstrap
{
    /// <summary>The account seeded by the <c>AddUsers</c> migration and promoted
    /// to admin by <c>AddUserIsAdmin</c>.</summary>
    private static readonly Guid SeededUserId = new("00000000-0000-0000-0000-000000000001");

    public static async Task EnsurePasswordAsync(IServiceProvider services, IConfiguration configuration)
    {
        var password = configuration["Auth:BootstrapPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var users = services.GetRequiredService<UserManager<StorymarkUser>>();
        var admin = await users.FindByIdAsync(SeededUserId.ToString());
        if (admin is null || !admin.IsAdmin || admin.PasswordHash is not null)
        {
            return;
        }

        var result = await users.AddPasswordAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Auth:BootstrapPassword was rejected: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }
}
