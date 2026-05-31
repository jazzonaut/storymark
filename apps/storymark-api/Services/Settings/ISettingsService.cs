using System.Diagnostics.CodeAnalysis;

namespace Storymark.Api.Services.Settings;

internal interface ISettingsService
{
    string GetString(string key);

    int GetInt(string key);

    bool GetBool(string key);

    T GetJson<T>(string key);

    bool TryGetSnapshot(string key, [NotNullWhen(true)] out SettingSnapshot? snapshot);

    IReadOnlyCollection<SettingSnapshot> GetAll();

    Task LoadAllAsync(CancellationToken ct);

    Task RefreshKeyAsync(string key, CancellationToken ct);

    /// <summary>Updates an existing setting's value. Preserves the row's type and
    /// secrecy (encrypting when secret) and refreshes the cache immediately. Type
    /// and secrecy are intrinsic to a key and owned by the seeder, so neither is
    /// mutable here. Throws <see cref="KeyNotFoundException"/> if the key does not
    /// exist — new keys are created only by the seeder.</summary>
    Task UpdateAsync(string key, string jsonValue, string updatedBy, CancellationToken ct);
}
