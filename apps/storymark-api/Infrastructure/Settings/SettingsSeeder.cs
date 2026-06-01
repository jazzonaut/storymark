using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Prompts;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Inserts any missing default settings on startup. Existing keys are
/// never overwritten — operator edits win.</summary>
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
        var existing = (await db.Settings.Select(s => s.Key).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        var added = 0;
        foreach (var seed in seeds)
        {
            if (existing.Contains(seed.Key))
            {
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

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Settings seed complete: {Added} inserted, {Existing} already present",
            added, existing.Count);
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
