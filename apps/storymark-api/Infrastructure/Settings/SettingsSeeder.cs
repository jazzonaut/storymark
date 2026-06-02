using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Prompts;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Inserts any missing default settings on startup, and re-seeds keys that
/// are still seed-authored (<see cref="Setting.UpdatedBy"/> == <c>"seed"</c>) when their
/// default text has changed — so a default whose *contract* changes (e.g. a prompt the
/// parser now reads differently) reaches existing installs. Operator edits (any other
/// <c>UpdatedBy</c>) always win and are never overwritten. Secrets are insert-only: their
/// stored value is encrypted, so there is no meaningful default to diff against.</summary>
internal sealed class SettingsSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<SettingsDefaultsOptions> defaults,
    IConfiguration configuration,
    ISettingsEncryptor encryptor,
    ILogger<SettingsSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct)
    {
        var seeds = BuildSeeds();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        var existing = (await db.Settings.ToListAsync(ct))
            .ToDictionary(s => s.Key, StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        var added = 0;
        var reseeded = new List<string>();
        foreach (var seed in seeds)
        {
            if (existing.TryGetValue(seed.Key, out var row))
            {
                if (TryReseed(row, seed, now))
                {
                    reseeded.Add(seed.Key);
                }

                continue;
            }

            var value = seed.IsSecret
                ? JsonSerializer.Serialize(encryptor.Encrypt(seed.JsonValue))
                : seed.JsonValue;

            db.Settings.Add(new Setting
            {
                Key = seed.Key,
                Value = value,
                ValueType = seed.ValueType,
                IsSecret = seed.IsSecret,
                UpdatedAt = now,
                UpdatedBy = "seed",
            });
            added++;
        }

        if (added > 0 || reseeded.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Settings seed complete: {Added} inserted, {Updated} re-seeded, {Existing} already present",
            added, reseeded.Count, existing.Count);
        if (reseeded.Count > 0)
        {
            // Name the keys: a re-seed silently changes live behaviour (e.g. a prompt the
            // parser now reads differently), so an operator needs to see which defaults moved.
            logger.LogInformation(
                "Re-seeded defaults the operator never edited: {Keys}", string.Join(", ", reseeded));
        }
    }

    // Re-seed only rows the operator never touched, and only non-secrets (a secret's stored
    // value is ciphertext, so there is no plaintext default to diff). seed.JsonValue is exactly
    // what a non-secret row stores, so an ordinal compare detects a changed default.
    private static bool TryReseed(Setting row, SettingSeed seed, DateTimeOffset now)
    {
        if (seed.IsSecret
            || !string.Equals(row.UpdatedBy, "seed", StringComparison.Ordinal)
            || string.Equals(row.Value, seed.JsonValue, StringComparison.Ordinal))
        {
            return false;
        }

        row.Value = seed.JsonValue;
        row.ValueType = seed.ValueType;
        row.UpdatedAt = now;
        return true;
    }

    private List<SettingSeed> BuildSeeds()
    {
        var options = defaults.Value;
        var seeds = new List<SettingSeed>
        {
            Scalar("upload.maxBytes", options.UploadMaxBytes, SettingValueType.Int),
            Scalar("features.recapCardOnSectionEntry", options.RecapCardOnSectionEntry, SettingValueType.Bool),
            Json("search.synonyms", options.Search.Synonyms),
            Scalar("assistant.evidence.trimLocal", options.Assistant.TrimLocal, SettingValueType.Bool),
            Scalar("assistant.evidence.maxChunks", options.Assistant.MaxChunks, SettingValueType.Int),
            Scalar("assistant.evidence.maxChunksLocal", options.Assistant.MaxChunksLocal, SettingValueType.Int),
            Scalar("entitySummary.refreshThresholdChars", options.EntitySummary.RefreshThresholdChars, SettingValueType.Int),
            Text("prompts.qa", PromptDefaults.Qa),
            Text("prompts.sectionExtraction", PromptDefaults.SectionExtraction),
            Text("prompts.entitySummary", PromptDefaults.EntitySummary),
            Text("prompts.selectionHint", PromptDefaults.SelectionHint),
            Text("prompts.bookMemory", PromptDefaults.BookMemory),
        };

        // LLM routing (providers, models, task assignments) is seeded by
        // LlmRegistrySeeder, not here — see ADR-0016.
        var bearerSeed = configuration["Auth:BearerTokenSeed"];
        if (!string.IsNullOrWhiteSpace(bearerSeed))
        {
            seeds.Add(Secret("auth.bearerToken", bearerSeed));
        }

        return seeds;
    }

    private static SettingSeed Text(string key, string value) => Scalar(key, value, SettingValueType.String);

    private static SettingSeed Json<T>(string key, T value) => Scalar(key, value, SettingValueType.Json);

    private static SettingSeed Secret(string key, string value) =>
        Scalar(key, value, SettingValueType.String) with { IsSecret = true };

    private static SettingSeed Scalar<T>(string key, T value, SettingValueType valueType) => new()
    {
        Key = key,
        JsonValue = JsonSerializer.Serialize(value),
        ValueType = valueType,
    };
}
