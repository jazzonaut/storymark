using System.Reflection;
using FastEndpoints;

namespace Storymark.Api.Endpoints.Health;

internal sealed record HealthResponse
{
    public required string Status { get; init; }

    public required string Version { get; init; }
}

internal sealed class HealthEndpoint : EndpointWithoutRequest<HealthResponse>
{
    public override void Configure()
    {
        Get("health");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        await Send.OkAsync(new HealthResponse { Status = "ok", Version = version }, ct);
    }
}
