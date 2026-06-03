using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Provisions the LLM registry (ADR-0016): on first run it seeds a local
/// Ollama provider and its default models, then ensures every task/embedding
/// setting points at a real model id — healing any reference that doesn't (e.g. a
/// legacy <c>provider:model</c> string) to a default. Also clears the retired
/// <c>llm.openai.*</c> / <c>llm.costRates</c> settings. Existing operator-created
/// providers/models are never overwritten.</summary>
internal sealed class LlmRegistrySeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LlmRegistrySeeder> logger)
{
    private static readonly Guid OllamaProviderId = new("a0000000-0000-0000-0000-0000000000aa");
    private static readonly Guid Llama3bId = new("b0000000-0000-0000-0000-0000000000b3");
    private static readonly Guid Llama1bId = new("b0000000-0000-0000-0000-0000000000b1");
    private static readonly Guid NomicId = new("b0000000-0000-0000-0000-0000000000be");

    private static readonly Dictionary<string, Guid> TaskDefaults = new(StringComparer.Ordinal)
    {
        [LlmSettingKeys.TaskKey("qa")] = Llama3bId,
        [LlmSettingKeys.TaskKey("extract")] = Llama1bId,
        [LlmSettingKeys.TaskKey("entity_summary")] = Llama3bId,
        [LlmSettingKeys.TaskKey("selection_hint")] = Llama1bId,
        [LlmSettingKeys.TaskKey("memory")] = Llama3bId,
        [LlmSettingKeys.TaskKey("current_scene")] = Llama3bId,
        [LlmSettingKeys.EmbeddingModelId] = NomicId,
    };

    private static readonly string[] ObsoleteKeys = ["llm.openai.baseUrl", "llm.openai.apiKey", "llm.costRates"];

    public async Task SeedAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StorymarkDbContext>();
        var now = DateTimeOffset.UtcNow;

        await SeedDefaultsAsync(db, now, ct);
        await HealTaskAssignmentsAsync(db, now, ct);
        await RemoveObsoleteSettingsAsync(db, ct);
    }

    private async Task SeedDefaultsAsync(StorymarkDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        if (!await db.LlmProviders.AnyAsync(ct))
        {
            db.LlmProviders.Add(new LlmProvider
            {
                Id = OllamaProviderId,
                Name = "Ollama (local)",
                Kind = LlmProviderKind.Ollama,
                BaseUrl = configuration["Ollama:Url"] ?? "http://localhost:11434",
                ApiKeyEncrypted = null,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(ct);
        }

        var hasOllama = await db.LlmProviders.AnyAsync(p => p.Id == OllamaProviderId, ct);
        if (hasOllama && !await db.LlmModels.AnyAsync(ct))
        {
            db.LlmModels.AddRange(
                Model(Llama3bId, "Llama 3.2 3B", "llama3.2:3b", now),
                Model(Llama1bId, "Llama 3.2 1B", "llama3.2:1b", now),
                Model(NomicId, "nomic-embed-text", "nomic-embed-text", now));
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task HealTaskAssignmentsAsync(StorymarkDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var modelIds = await db.LlmModels
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => m.Id)
            .ToListAsync(ct);
        if (modelIds.Count == 0)
        {
            return;
        }

        var valid = modelIds.ToHashSet();
        var fallback = valid.Contains(Llama3bId) ? Llama3bId : modelIds[0];
        var healed = 0;

        foreach (var (key, defaultId) in TaskDefaults)
        {
            var desired = valid.Contains(defaultId) ? defaultId : fallback;
            var setting = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, ct);

            if (setting is null)
            {
                db.Settings.Add(new Setting
                {
                    Key = key,
                    Value = JsonSerializer.Serialize(desired.ToString()),
                    ValueType = SettingValueType.String,
                    IsSecret = false,
                    UpdatedAt = now,
                    UpdatedBy = "seed",
                });
                healed++;
            }
            else if (!ReferencesValidModel(setting.Value, valid))
            {
                setting.Value = JsonSerializer.Serialize(desired.ToString());
                setting.UpdatedAt = now;
                setting.UpdatedBy = "seed";
                healed++;
            }
        }

        if (healed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("LLM registry: {Healed} task assignment(s) set to a default model", healed);
        }
    }

    private static async Task RemoveObsoleteSettingsAsync(StorymarkDbContext db, CancellationToken ct)
    {
        await db.Settings.Where(s => ObsoleteKeys.Contains(s.Key)).ExecuteDeleteAsync(ct);
    }

    private static bool ReferencesValidModel(string settingValue, HashSet<Guid> valid)
    {
        try
        {
            var raw = JsonSerializer.Deserialize<string>(settingValue);
            return Guid.TryParse(raw, out var id) && valid.Contains(id);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static LlmModel Model(Guid id, string label, string modelName, DateTimeOffset now) => new()
    {
        Id = id,
        Label = label,
        ProviderId = OllamaProviderId,
        ModelName = modelName,
        CreatedAt = now,
        UpdatedAt = now,
    };
}
