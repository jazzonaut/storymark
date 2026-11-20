using Storymark.Api.Domain.Settings;

namespace Storymark.Api.Services.Settings;

/// <summary>An in-memory, decrypted view of a setting. <see cref="Value"/> is
/// always plaintext JSON text, even for secrets.</summary>
internal sealed record SettingSnapshot
{
    public required string Key { get; init; }

    public required string Value { get; init; }

    public required SettingValueType ValueType { get; init; }

    public required bool IsSecret { get; init; }

    public required bool IsSeedAuthored { get; init; }
}
