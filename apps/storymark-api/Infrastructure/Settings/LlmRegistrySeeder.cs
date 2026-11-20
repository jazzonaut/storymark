using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Database;

namespace Storymark.Api.Infrastructure.Settings;

/// <summary>Provisions the LLM registry (ADR-0016): on first run it seeds OpenRouter
/// for chat and local Ollama for embeddings, then ensures every task/embedding
/// setting points at a real model id — healing any reference that doesn't (e.g. a
/// legacy <c>provider:model</c> string) to a default. Also clears the retired
/// <c>llm.openai.*</c> / <c>llm.costRates</c> settings. Existing operator-created
/// providers/models are never overwritten.</summary>
internal sealed class LlmRegistrySeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<LlmRegistrySeeder> logger)
{
    private const string GeminiModelName = "google/gemini-2.5-flash-lite";

    private static readonly Guid OllamaProviderId = new("a0000000-0000-0000-0000-0000000000aa");
    private static readonly Guid OpenRouterProviderId = new("a0000000-0000-0000-0000-0000000000ab");
    private static readonly Guid GeminiId = new("b0000000-0000-0000-0000-0000000000bf");
    private static readonly Guid Llama3bId = new("b0000000-0000-0000-0000-0000000000b3");
    private static readonly Guid Llama1bId = new("b0000000-0000-0000-0000-0000000000b1");
    private static readonly Guid NomicId = new("b0000000-0000-0000-0000-0000000000be");

    private static readonly Dictionary<string, Guid> LocalTaskDefaults = new(StringComparer.Ordinal)
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
        var fresh = !await db.LlmProviders.AnyAsync(ct);
        if (fresh)
        {
            db.LlmProviders.Add(new LlmProvider
            {
                Id = OllamaProviderId,
                Name = "Ollama (local)",
                Kind = LlmProviderKind.Ollama,
                BaseUrl = configuration["Ollama:Url"] ?? "http://localhost:11434",
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.LlmProviders.Add(new LlmProvider
            {
                Id = OpenRouterProviderId,
                Name = "OpenRouter",
                Kind = LlmProviderKind.OpenAiCompatible,
                BaseUrl = "https://openrouter.ai/api/v1",
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.LlmModels.Add(new LlmModel
            {
                Id = GeminiId,
                Label = "Gemini 2.5 Flash-Lite",
                ProviderId = OpenRouterProviderId,
                ModelName = GeminiModelName,
                InputPerMillion = 0.10m,
                OutputPerMillion = 0.40m,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }

        // Also repair an existing installation whose local model registry was emptied.
        if (fresh || (await db.LlmProviders.AnyAsync(p => p.Id == OllamaProviderId, ct)
            && !await db.LlmModels.AnyAsync(ct)))
        {
            db.LlmModels.AddRange(
                LocalModel(Llama3bId, "Llama 3.2 3B", "llama3.2:3b", now),
                LocalModel(Llama1bId, "Llama 3.2 1B", "llama3.2:1b", now),
                LocalModel(NomicId, "nomic-embed-text", "nomic-embed-text", now));
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task HealTaskAssignmentsAsync(StorymarkDbContext db, DateTimeOffset now, CancellationToken ct)
    {
        var models = await db.LlmModels
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.ModelName, m.ReasoningEffort })
            .ToListAsync(ct);
        if (models.Count == 0)
        {
            return;
        }

        var valid = models.Select(m => m.Id).ToHashSet();
        var fallback = valid.Contains(Llama3bId) ? Llama3bId : models[0].Id;
        var gemini = models.FirstOrDefault(m => string.Equals(m.ModelName, GeminiModelName, StringComparison.Ordinal) && m.ReasoningEffort is null);
        var healed = 0;

        foreach (var (key, localDefault) in LocalTaskDefaults)
        {
            var desired = valid.Contains(localDefault) ? localDefault : fallback;
            if (gemini is not null && !string.Equals(key, LlmSettingKeys.EmbeddingModelId, StringComparison.Ordinal))
            {
                desired = gemini.Id;
            }

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

    private static LlmModel LocalModel(Guid id, string label, string modelName, DateTimeOffset now) => new()
    {
        Id = id,
        Label = label,
        ProviderId = OllamaProviderId,
        ModelName = modelName,
        CreatedAt = now,
        UpdatedAt = now,
    };
}
