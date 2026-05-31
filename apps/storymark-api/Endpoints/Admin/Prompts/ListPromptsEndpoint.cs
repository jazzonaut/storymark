using System.Text.Json;
using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Prompts;

internal sealed record PromptItem
{
    public required string Key { get; init; }

    public required string Text { get; init; }
}

/// <summary>Returns the current text of every <c>prompts.*</c> setting. Admin-only.</summary>
internal sealed class ListPromptsEndpoint(ISettingsService settings)
    : EndpointWithoutRequest<IReadOnlyList<PromptItem>>
{
    public override void Configure()
    {
        Get("prompts");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = settings.GetAll()
            .Where(s => s.Key.StartsWith("prompts.", StringComparison.Ordinal))
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => new PromptItem
            {
                Key = s.Key,
                Text = JsonSerializer.Deserialize<string>(s.Value) ?? string.Empty,
            })
            .ToList();

        await Send.OkAsync(items, ct);
    }
}
