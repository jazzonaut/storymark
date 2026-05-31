using System.Text.Json;
using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Prompts;

internal sealed record PatchPromptRequest
{
    public required string Text { get; init; }
}

/// <summary>Replaces the text of a single <c>prompts.{key}</c> setting. Admin-only.
/// The change is live on the next call that reads that prompt.</summary>
internal sealed class PatchPromptEndpoint(ISettingsService settings)
    : Endpoint<PatchPromptRequest, PromptItem>
{
    public override void Configure()
    {
        Patch("prompts/{key}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(PatchPromptRequest req, CancellationToken ct)
    {
        var key = Route<string>("key");
        if (string.IsNullOrWhiteSpace(key))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var fullKey = $"prompts.{key}";
        var updatedBy = HttpContext.Items[AdminAuthPreProcessor.FingerprintItemKey] as string ?? "admin";

        try
        {
            await settings.UpdateAsync(fullKey, JsonSerializer.Serialize(req.Text), updatedBy, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new PromptItem { Key = fullKey, Text = req.Text }, ct);
    }
}
