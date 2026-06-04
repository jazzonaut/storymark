using Microsoft.AspNetCore.Identity;

namespace Storymark.Api.Domain.Users;

/// <summary>A reader account (ADR-0023). Trimmed Identity user: only password
/// auth with lockout is used, so the email/phone/2FA properties are excluded
/// from the EF model in <c>UsersConfiguration</c>. A null
/// <see cref="IdentityUser{TKey}.PasswordHash"/> means the account is seeded
/// but not yet activated — login rejects it.</summary>
internal sealed class StorymarkUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Grants access to the admin area (<c>/api/admin/*</c> and the
    /// Hangfire dashboard). Carried into the session cookie as a claim at
    /// sign-in; toggling it rotates the security stamp so stale tickets die.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>The non-null username. <c>users.user_name</c> is NOT NULL, so the
    /// inherited nullable property is a store-contract artifact; a computed get-only
    /// property stays outside the EF model.</summary>
    public string RequiredUserName =>
        UserName ?? throw new InvalidOperationException("users.user_name is required.");
}
