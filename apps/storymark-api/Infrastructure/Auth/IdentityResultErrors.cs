using FastEndpoints;
using Microsoft.AspNetCore.Identity;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Translates a failed <see cref="IdentityResult"/> into the standard
/// FastEndpoints 400 error response, one validation failure per Identity error.</summary>
internal static class IdentityResultErrors
{
    /// <summary>A no-op when the result succeeded; otherwise throws the
    /// validation-failure exception FastEndpoints renders as the 400.</summary>
    public static void ThrowIfFailed<TRequest>(
        this IValidationErrors<TRequest> endpoint, IdentityResult result)
        where TRequest : notnull
    {
        if (result.Succeeded)
        {
            return;
        }

        foreach (var error in result.Errors)
        {
            endpoint.AddError(error.Description);
        }

        endpoint.ThrowIfAnyErrors();
    }
}
