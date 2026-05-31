using FastEndpoints;
using Storymark.Api.Services.Settings;

namespace Storymark.Api.Endpoints.Debug;

internal sealed record GetSettingResponse
{
    public required string Key { get; init; }

    public required string ValueType { get; init; }

    public required bool IsSecret { get; init; }

    public required string Value { get; init; }
}

/// <summary>Roundtrip-verification endpoint for the settings cache. Gated by
/// <c>Storymark:DebugEndpointsEnabled</c>; returns 404 when disabled. Secret
/// values are masked.</summary>
internal sealed class GetSettingEndpoint(IConfiguration configuration, ISettingsService settings)
    : EndpointWithoutRequest<GetSettingResponse>
{
    public override void Configure()
    {
        Get("_debug/setting");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Storymark:DebugEndpointsEnabled"))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var key = Query<string>("key", isRequired: true);
        if (string.IsNullOrEmpty(key))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!settings.TryGetSnapshot(key, out var snapshot))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(
            new GetSettingResponse
            {
                Key = snapshot.Key,
                ValueType = snapshot.ValueType.ToString(),
                IsSecret = snapshot.IsSecret,
                Value = snapshot.IsSecret ? "••••" : snapshot.Value,
            },
            ct);
    }
}
