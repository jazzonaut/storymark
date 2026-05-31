using System.Text;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Gates the Hangfire dashboard behind the same live admin bearer token
/// as the admin API, but via HTTP Basic auth so a browser can present it: the
/// password is compared (constant-time, fail-closed) against
/// <c>auth.bearerToken</c> and the username is ignored. On failure it issues a
/// <c>WWW-Authenticate: Basic</c> challenge so the browser shows a login dialog.</summary>
internal sealed class HangfireDashboardAuthFilter(ISettingsService settings) : IDashboardAuthorizationFilter
{
    private const string Scheme = "Basic ";

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var password = ExtractPassword(http.Request.Headers.Authorization.ToString());
        if (AdminToken.Matches(password, AdminToken.Current(settings)))
        {
            return true;
        }

        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        http.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Storymark\", charset=\"UTF-8\"";
        return false;
    }

    private static string? ExtractPassword(string header)
    {
        if (!header.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header[Scheme.Length..]));
            var colon = decoded.IndexOf(':', StringComparison.Ordinal);
            return colon >= 0 ? decoded[(colon + 1)..] : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
