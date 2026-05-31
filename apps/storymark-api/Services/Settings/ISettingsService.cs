using System.Diagnostics.CodeAnalysis;

namespace Storymark.Api.Services.Settings;

internal interface ISettingsService
{
    string GetString(string key);

    int GetInt(string key);

    bool GetBool(string key);

    T GetJson<T>(string key);

    bool TryGetSnapshot(string key, [NotNullWhen(true)] out SettingSnapshot? snapshot);

    Task LoadAllAsync(CancellationToken ct);

    Task RefreshKeyAsync(string key, CancellationToken ct);
}
