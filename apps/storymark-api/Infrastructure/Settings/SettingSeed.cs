using Storymark.Api.Domain.Settings;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>A default setting to insert if its key is missing. <see cref="JsonValue"/>
/// is plaintext JSON text; secrets are encrypted by the seeder before persisting.</summary>
internal sealed record SettingSeed
{
    public required string Key { get; init; }

    public required string JsonValue { get; init; }

    public required SettingValueType ValueType { get; init; }

    public bool IsSecret { get; init; }
}
