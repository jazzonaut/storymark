using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Settings;

namespace Storymark.Api.Services.Settings;

internal sealed class SettingsService(
    IServiceScopeFactory scopeFactory,
    ISettingsEncryptor encryptor,
    ILogger<SettingsService> logger) : ISettingsService
{
    private readonly ConcurrentDictionary<string, SettingSnapshot> _cache = new(StringComparer.Ordinal);

    public async Task LoadAllAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        var rows = await db.Settings.AsNoTracking().ToListAsync(ct);

        _cache.Clear();
        foreach (var row in rows)
        {
            _cache[row.Key] = ToSnapshot(row);
        }

        logger.LogInformation("Loaded {Count} settings into cache", rows.Count);
    }

    public async Task RefreshKeyAsync(string key, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct);

        if (row is null)
        {
            _cache.TryRemove(key, out _);
        }
        else
        {
            _cache[key] = ToSnapshot(row);
        }

        logger.LogDebug("Refreshed setting {Key} in cache", key);
    }

    public string GetString(string key) => GetJson<string>(key);

    public int GetInt(string key) => GetJson<int>(key);

    public bool GetBool(string key) => GetJson<bool>(key);

    public T GetJson<T>(string key)
    {
        if (!_cache.TryGetValue(key, out var snapshot))
        {
            throw new KeyNotFoundException($"Setting '{key}' is not present.");
        }

        return JsonSerializer.Deserialize<T>(snapshot.Value)
            ?? throw new InvalidOperationException($"Setting '{key}' deserialised to null.");
    }

    public bool TryGetSnapshot(string key, [NotNullWhen(true)] out SettingSnapshot? snapshot)
        => _cache.TryGetValue(key, out snapshot);

    private SettingSnapshot ToSnapshot(Setting row)
    {
        var value = row.IsSecret
            ? encryptor.Decrypt(JsonSerializer.Deserialize<string>(row.Value)
                ?? throw new InvalidOperationException($"Secret setting '{row.Key}' is malformed."))
            : row.Value;

        return new SettingSnapshot
        {
            Key = row.Key,
            Value = value,
            ValueType = row.ValueType,
            IsSecret = row.IsSecret,
        };
    }
}
