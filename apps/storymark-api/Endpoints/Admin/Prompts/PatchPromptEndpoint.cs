using System.Text.Json;
using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Prompts;
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
        // A template missing a placeholder its render site relies on would silently
        // drop that content (rendering is a plain Replace), so reject it here.
        var missing = PromptContract.MissingPlaceholders(fullKey, req.Text);
        if (missing.Count > 0)
        {
            ThrowError(r => r.Text, $"Prompt must contain the placeholder(s): {string.Join(", ", missing)}.");
        }

        try
        {
            await settings.UpdateAsync(fullKey, JsonSerializer.Serialize(req.Text), User.UserName(), ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(new PromptItem { Key = fullKey, Text = req.Text, FollowsDefault = false }, ct);
    }
}
