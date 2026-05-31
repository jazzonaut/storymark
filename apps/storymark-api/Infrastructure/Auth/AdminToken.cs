using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Infrastructure.Auth;

/// <summary>Reads the live admin bearer token from settings and compares a
/// presented credential against it. Fail-closed: an unset/empty configured token
/// matches nothing (so a missing setting can never authenticate), and the compare
/// is constant-time. Shared by the FastEndpoints admin gate and the Hangfire
/// dashboard filter so the comparison logic exists in exactly one place.</summary>
internal static class AdminToken
{
    public static string Current(ISettingsService settings) =>
        settings.TryGetSnapshot("auth.bearerToken", out var snapshot)
            ? JsonSerializer.Deserialize<string>(snapshot.Value) ?? string.Empty
            : string.Empty;

    public static bool Matches(string? presented, string expected) =>
        expected.Length > 0
        && !string.IsNullOrEmpty(presented)
        && CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(expected));
}
