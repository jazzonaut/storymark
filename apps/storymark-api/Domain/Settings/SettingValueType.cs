namespace Storymark.Api.Domain.Settings;

/// <summary>The data type a setting's JSON value deserialises to. Secrecy is a
/// separate concern, tracked by <see cref="Setting.IsSecret"/>.</summary>
internal enum SettingValueType
{
    String,
    Int,
    Bool,
    Json,
}
