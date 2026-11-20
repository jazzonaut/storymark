using System.Text.Json;
using FastEndpoints;
using Storymark.Api.Domain.Settings;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Prompts;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Prompts;

internal sealed class ResetPromptEndpoint(ISettingsService settings) : EndpointWithoutRequest<PromptItem>
{
    public override void Configure()
    {
        Post("prompts/{key}/reset");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var key = $"prompts.{Route<string>("key")}";
        if (!PromptDefaults.ByKey.TryGetValue(key, out var text))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await settings.UpdateAsync(key, JsonSerializer.Serialize(text), Setting.SeedAuthor, ct);
        await Send.OkAsync(new PromptItem { Key = key, Text = text, FollowsDefault = true }, ct);
    }
}
