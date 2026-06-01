using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Admin.Models;

/// <summary>A provider as the admin UI sees it. The key is never returned —
/// <see cref="HasApiKey"/> reports only whether one is stored (write-only).</summary>
internal sealed record ProviderItem
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string BaseUrl { get; init; }

    public required bool HasApiKey { get; init; }

    public static ProviderItem From(LlmProvider p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Kind = p.Kind.ToToken(),
        BaseUrl = p.BaseUrl,
        HasApiKey = !string.IsNullOrEmpty(p.ApiKeyEncrypted),
    };
}

internal sealed record CreateProviderRequest
{
    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string BaseUrl { get; init; }

    public string? ApiKey { get; init; }
}

internal sealed record UpdateProviderRequest
{
    public Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Kind { get; init; }

    public required string BaseUrl { get; init; }

    /// <summary>A new key, or null/blank to keep the stored one (write-only).</summary>
    public string? ApiKey { get; init; }
}

internal sealed record DeleteByIdRequest
{
    public Guid Id { get; init; }
}

internal sealed class ListProvidersEndpoint(LlmRegistryService registry)
    : EndpointWithoutRequest<IReadOnlyList<ProviderItem>>
{
    public override void Configure()
    {
        Get("models/providers");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var providers = await registry.ListProvidersAsync(ct);
        await Send.OkAsync(providers.Select(ProviderItem.From).ToList(), ct);
    }
}

internal sealed class CreateProviderEndpoint(LlmRegistryService registry)
    : Endpoint<CreateProviderRequest, ProviderItem>
{
    public override void Configure()
    {
        Post("models/providers");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CreateProviderRequest req, CancellationToken ct)
    {
        if (!LlmProviderKindExtensions.TryParseToken(req.Kind, out var kind))
        {
            ThrowError(r => r.Kind, "kind must be 'ollama' or 'openai_compatible'.");
        }

        var provider = await registry.CreateProviderAsync(req.Name, kind, req.BaseUrl, req.ApiKey, ct);
        await Send.OkAsync(ProviderItem.From(provider), ct);
    }
}

internal sealed class UpdateProviderEndpoint(LlmRegistryService registry)
    : Endpoint<UpdateProviderRequest, ProviderItem>
{
    public override void Configure()
    {
        Patch("models/providers/{id}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(UpdateProviderRequest req, CancellationToken ct)
    {
        if (!LlmProviderKindExtensions.TryParseToken(req.Kind, out var kind))
        {
            ThrowError(r => r.Kind, "kind must be 'ollama' or 'openai_compatible'.");
        }

        try
        {
            var provider = await registry.UpdateProviderAsync(req.Id, req.Name, kind, req.BaseUrl, req.ApiKey, ct);
            await Send.OkAsync(ProviderItem.From(provider), ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}

internal sealed class DeleteProviderEndpoint(LlmRegistryService registry) : Endpoint<DeleteByIdRequest>
{
    public override void Configure()
    {
        Delete("models/providers/{id}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(DeleteByIdRequest req, CancellationToken ct)
    {
        try
        {
            await registry.DeleteProviderAsync(req.Id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await Send.ResultAsync(Results.Conflict(new { message = ex.Message }));
        }
    }
}
