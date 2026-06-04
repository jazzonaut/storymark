using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Gates the Hangfire dashboard behind the same session-cookie admin
/// claim as the admin API (ADR-0024). The dashboard is served on the SPA's
/// origin, so the browser sends the cookie on its own — an admin signs into the
/// app, then opens <c>/hangfire</c>.</summary>
internal sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        if (http.User.IsAdmin())
        {
            return true;
        }

        http.Response.StatusCode = http.User.Identity?.IsAuthenticated == true
            ? StatusCodes.Status403Forbidden
            : StatusCodes.Status401Unauthorized;
        return false;
    }
}
