using System.Text.Json;
using FastEndpoints;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Settings;

internal sealed record PatchSettingRequest
{
    public required string Key { get; init; }

    public required JsonElement Value { get; init; }

    public required string Type { get; init; }
}

/// <summary>Updates an existing setting's value and type. Admin-only. Secrecy is
/// preserved from the stored row; the value is encrypted at rest when secret.</summary>
internal sealed class PatchSettingEndpoint(ISettingsService settings)
    : Endpoint<PatchSettingRequest, SettingListItem>
{
    public override void Configure()
    {
        Patch("settings");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(PatchSettingRequest req, CancellationToken ct)
    {
        if (!settings.TryGetSnapshot(req.Key, out var existing))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Changing the embedding model without reindexing leaves every book's chunks
        // embedded under the old model, silently emptying retrieval (search filters by
        // the active model id). Only the apply-swap endpoint may write this key — it
        // persists the value and enqueues the per-book reindex atomically.
        if (string.Equals(req.Key, LlmSettingKeys.EmbeddingModelId, StringComparison.Ordinal))
        {
            ThrowError(r => r.Key, "Use POST admin/embedding/apply-swap to change the embedding model; it queues the reindex every book needs.");
        }

        if (!Enum.TryParse<SettingValueType>(req.Type, ignoreCase: true, out var requestedType)
            || requestedType != existing.ValueType)
        {
            ThrowError(r => r.Type, $"Setting '{req.Key}' is of type {existing.ValueType}; its type cannot be changed.");
        }

        if (!Matches(existing.ValueType, req.Value))
        {
            ThrowError(r => r.Value, $"Value is not a valid {existing.ValueType}.");
        }

        await settings.UpdateAsync(req.Key, req.Value.GetRawText(), User.UserName(), ct);

        if (!settings.TryGetSnapshot(req.Key, out var updated))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(
            new SettingListItem
            {
                Key = updated.Key,
                ValueType = updated.ValueType.ToString(),
                IsSecret = updated.IsSecret,
                Value = updated.IsSecret ? "••••" : updated.Value,
            },
            ct);
    }

    private static bool Matches(SettingValueType type, JsonElement value) => type switch
    {
        SettingValueType.String => value.ValueKind == JsonValueKind.String,
        SettingValueType.Int => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
        SettingValueType.Bool => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        SettingValueType.Json => value.ValueKind is JsonValueKind.Object or JsonValueKind.Array,
        _ => false,
    };
}
