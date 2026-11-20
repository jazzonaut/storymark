namespace Storymark.Api.Domain.Settings;

/// <summary>A single DB-backed configuration entry. <see cref="Value"/> is JSON
/// text (the column is jsonb); for secrets it is the AES-GCM ciphertext at rest.</summary>
internal sealed class Setting
{
    public const string SeedAuthor = "seed";

    public required string Key { get; set; }

    public required string Value { get; set; }

    public required SettingValueType ValueType { get; set; }

    public bool IsSecret { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}
