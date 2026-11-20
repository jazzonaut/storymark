using Microsoft.EntityFrameworkCore;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Database;
using Storymark.Api.Infrastructure.Settings;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Services.Llm;

/// <summary>A model resolved to everything a call needs: the wire model name, the
/// client kind, the provider's endpoint (decrypted key), and pricing.</summary>
internal sealed record ResolvedModel
{
    public required Guid ModelId { get; init; }

    public required string ModelName { get; init; }

    public string? ReasoningEffort { get; init; }

    public required string ProviderName { get; init; }

    public required LlmProviderKind Kind { get; init; }

    public required string BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    public decimal? InputPerMillion { get; init; }

    public decimal? OutputPerMillion { get; init; }

    /// <summary>The string stored on <c>book_chunks.embedding_model_id</c>
    /// (e.g. <c>ollama:nomic-embed-text</c>); see ADR-0010/0014/0016.</summary>
    public string EmbeddingIdentity => Kind.EmbeddingIdentity(ModelName);
}

/// <summary>Owns the provider/model registry (ADR-0016): resolves a task or the
/// embedding setting to its <see cref="ResolvedModel"/> for the router, and is the
/// admin CRUD surface. API keys are encrypted here and never returned in clear.</summary>
internal sealed class LlmRegistryService(
    StorymarkDbContext db,
    ISettingsService settings,
    ISettingsEncryptor encryptor)
{
    public Task<ResolvedModel> ResolveTaskAsync(string task, CancellationToken ct) =>
        ResolveSettingAsync(LlmSettingKeys.TaskKey(task), ct);

    public Task<ResolvedModel> ResolveEmbeddingAsync(CancellationToken ct) =>
        ResolveSettingAsync(LlmSettingKeys.EmbeddingModelId, ct);

    /// <summary>The embedding model's identity string (<c>kind:modelName</c>) for the
    /// <c>book_chunks</c> filter, without the full resolve — no provider key is loaded
    /// or decrypted, since callers that only filter chunks never make a call.</summary>
    public async Task<string> ResolveEmbeddingIdentityAsync(CancellationToken ct)
    {
        var modelId = ReadModelIdSetting(LlmSettingKeys.EmbeddingModelId);
        var identity = await db.LlmModels
            .AsNoTracking()
            .Where(m => m.Id == modelId)
            .Join(db.LlmProviders.AsNoTracking(), m => m.ProviderId, p => p.Id, (m, p) => new { p.Kind, m.ModelName })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"No LLM model with id '{modelId}' is configured.");

        return identity.Kind.EmbeddingIdentity(identity.ModelName);
    }

    public async Task<ResolvedModel> ResolveModelAsync(Guid modelId, CancellationToken ct)
    {
        var row = await db.LlmModels
            .AsNoTracking()
            .Where(m => m.Id == modelId)
            .Join(db.LlmProviders.AsNoTracking(), m => m.ProviderId, p => p.Id, (m, p) => new { m, p })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"No LLM model with id '{modelId}' is configured.");

        return new ResolvedModel
        {
            ModelId = row.m.Id,
            ModelName = row.m.ModelName,
            ReasoningEffort = row.m.ReasoningEffort,
            ProviderName = row.p.Name,
            Kind = row.p.Kind,
            BaseUrl = row.p.BaseUrl,
            ApiKey = string.IsNullOrEmpty(row.p.ApiKeyEncrypted) ? null : encryptor.Decrypt(row.p.ApiKeyEncrypted),
            InputPerMillion = row.m.InputPerMillion,
            OutputPerMillion = row.m.OutputPerMillion,
        };
    }

    public async Task<IReadOnlyList<LlmProvider>> ListProvidersAsync(CancellationToken ct) =>
        await db.LlmProviders.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

    public async Task<LlmProvider> CreateProviderAsync(
        string name, LlmProviderKind kind, string baseUrl, string? apiKey, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var provider = new LlmProvider
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Kind = kind,
            BaseUrl = baseUrl.Trim(),
            ApiKeyEncrypted = EncryptKey(apiKey),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.LlmProviders.Add(provider);
        await db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task<LlmProvider> UpdateProviderAsync(
        Guid id, string name, LlmProviderKind kind, string baseUrl, string? apiKey, CancellationToken ct)
    {
        var provider = await db.LlmProviders.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"No provider with id '{id}'.");

        provider.Name = name.Trim();
        provider.Kind = kind;
        provider.BaseUrl = baseUrl.Trim();
        // A blank key means "leave the stored key unchanged" (write-only field).
        if (!string.IsNullOrEmpty(apiKey))
        {
            provider.ApiKeyEncrypted = encryptor.Encrypt(apiKey);
        }

        provider.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return provider;
    }

    public async Task DeleteProviderAsync(Guid id, CancellationToken ct)
    {
        if (await db.LlmModels.AnyAsync(m => m.ProviderId == id, ct))
        {
            throw new InvalidOperationException("Delete the provider's models first.");
        }

        await db.LlmProviders.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<LlmModel>> ListModelsAsync(CancellationToken ct) =>
        await db.LlmModels.AsNoTracking().OrderBy(m => m.Label).ToListAsync(ct);

    public async Task<LlmModel> CreateModelAsync(
        string label, Guid providerId, string modelName, decimal? input, decimal? output,
        string? reasoningEffort, CancellationToken ct)
    {
        await ValidateModelProviderAsync(providerId, reasoningEffort, ct);

        var now = DateTimeOffset.UtcNow;
        var model = new LlmModel
        {
            Id = Guid.NewGuid(),
            Label = label.Trim(),
            ProviderId = providerId,
            ModelName = modelName.Trim(),
            ReasoningEffort = reasoningEffort,
            InputPerMillion = input,
            OutputPerMillion = output,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.LlmModels.Add(model);
        await db.SaveChangesAsync(ct);
        return model;
    }

    public async Task<LlmModel> UpdateModelAsync(
        Guid id, string label, Guid providerId, string modelName, decimal? input, decimal? output,
        string? reasoningEffort, CancellationToken ct)
    {
        var model = await db.LlmModels.FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new KeyNotFoundException($"No model with id '{id}'.");

        await ValidateModelProviderAsync(providerId, reasoningEffort, ct);

        model.Label = label.Trim();
        model.ProviderId = providerId;
        model.ModelName = modelName.Trim();
        model.ReasoningEffort = reasoningEffort;
        model.InputPerMillion = input;
        model.OutputPerMillion = output;
        model.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return model;
    }

    public async Task DeleteModelAsync(Guid id, CancellationToken ct)
    {
        if (IsModelAssignedToTask(id))
        {
            throw new InvalidOperationException("Reassign the tasks using this model first.");
        }

        await db.LlmModels.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
    }

    private async Task ValidateModelProviderAsync(Guid providerId, string? reasoningEffort, CancellationToken ct)
    {
        var provider = await db.LlmProviders.AsNoTracking().FirstOrDefaultAsync(p => p.Id == providerId, ct)
            ?? throw new InvalidOperationException($"No provider with id '{providerId}'.");

        if (reasoningEffort is not (null or "none" or "minimal" or "low" or "medium" or "high" or "xhigh" or "max"))
        {
            throw new ArgumentException("Unsupported reasoning effort.", nameof(reasoningEffort));
        }

        if (reasoningEffort is not null && provider.Kind != LlmProviderKind.OpenAiCompatible)
        {
            throw new ArgumentException("Reasoning effort requires an OpenAI-compatible provider.", nameof(reasoningEffort));
        }
    }

    private Task<ResolvedModel> ResolveSettingAsync(string key, CancellationToken ct) =>
        ResolveModelAsync(ReadModelIdSetting(key), ct);

    private Guid ReadModelIdSetting(string key)
    {
        var raw = settings.GetString(key);
        if (!Guid.TryParse(raw, out var modelId))
        {
            throw new InvalidOperationException($"Setting '{key}' must be a model id; got '{raw}'.");
        }

        return modelId;
    }

    private bool IsModelAssignedToTask(Guid id)
    {
        var idText = id.ToString();
        return settings.GetAll()
            .Where(s => s.Key.StartsWith(LlmSettingKeys.TaskMapPrefix, StringComparison.Ordinal)
                || string.Equals(s.Key, LlmSettingKeys.EmbeddingModelId, StringComparison.Ordinal))
            .Any(s => string.Equals(settings.GetString(s.Key), idText, StringComparison.Ordinal));
    }

    private string? EncryptKey(string? apiKey) =>
        string.IsNullOrEmpty(apiKey) ? null : encryptor.Encrypt(apiKey);
}
