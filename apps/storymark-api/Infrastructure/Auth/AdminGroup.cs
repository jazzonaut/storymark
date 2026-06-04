using FastEndpoints;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Route group for <c>/api/admin/*</c>. Secure-by-default like the
/// reader endpoints — the cookie scheme answers 401 for missing sessions — and
/// <see cref="AdminAuthPreProcessor"/> adds the admin-claim check (403 for a
/// signed-in non-admin) so admin endpoints never repeat the auth wiring (ADR-0024).</summary>
internal sealed class AdminGroup : Group
{
    public AdminGroup()
    {
        Configure("admin", ep => ep.PreProcessor<AdminAuthPreProcessor>(Order.Before));
    }
}
