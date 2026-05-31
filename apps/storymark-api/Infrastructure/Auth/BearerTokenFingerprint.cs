using System.Security.Cryptography;
using System.Text;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Derives a short, non-reversible label for a bearer token so audit
/// columns (<c>updated_by</c>) can identify the actor without storing the token.</summary>
internal static class BearerTokenFingerprint
{
    public static string Of(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(hash)[..12];
    }
}
