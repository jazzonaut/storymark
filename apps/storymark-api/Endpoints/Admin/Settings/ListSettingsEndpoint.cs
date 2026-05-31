using FastEndpoints;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Admin.Settings;

internal sealed record SettingListItem
{
    public required string Key { get; init; }

    public required string ValueType { get; init; }

    public required bool IsSecret { get; init; }

    public required string Value { get; init; }
}

/// <summary>Lists every setting with secret values masked. Admin-only.</summary>
internal sealed class ListSettingsEndpoint(ISettingsService settings)
    : EndpointWithoutRequest<IReadOnlyList<SettingListItem>>
{
    public override void Configure()
    {
        Get("settings");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var items = settings.GetAll()
            .OrderBy(s => s.Key, StringComparer.Ordinal)
            .Select(s => new SettingListItem
            {
                Key = s.Key,
                ValueType = s.ValueType.ToString(),
                IsSecret = s.IsSecret,
                Value = s.IsSecret ? "••••" : s.Value,
            })
            .ToList();

        await Send.OkAsync(items, ct);
    }
}
