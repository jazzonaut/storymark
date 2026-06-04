using FastEndpoints;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Route group for <c>/api/admin/*</c>. Applies <see cref="AdminAuthPreProcessor"/>
/// to every member endpoint, so admin endpoints never repeat the auth wiring. The
/// group is anonymous to ASP.NET authorization — deliberately outside the reader
/// cookie scheme (ADR-0023) — the pre-processor is the gate, validating the shared
/// operator token from settings.</summary>
internal sealed class AdminGroup : Group
{
    public AdminGroup()
    {
        Configure("admin", ep =>
        {
            ep.AllowAnonymous();
            ep.PreProcessor<AdminAuthPreProcessor>(Order.Before);
        });
    }
}
