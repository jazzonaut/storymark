using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Infrastructure.Auth;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Admin.Models;

internal sealed record ModelItem
{
    public required Guid Id { get; init; }

    public required string Label { get; init; }

    public required Guid ProviderId { get; init; }

    public required string ModelName { get; init; }

    public decimal? InputPerMillion { get; init; }

    public decimal? OutputPerMillion { get; init; }

    public static ModelItem From(LlmModel m) => new()
    {
        Id = m.Id,
        Label = m.Label,
        ProviderId = m.ProviderId,
        ModelName = m.ModelName,
        InputPerMillion = m.InputPerMillion,
        OutputPerMillion = m.OutputPerMillion,
    };
}

internal sealed record CreateModelRequest
{
    public required string Label { get; init; }

    public Guid ProviderId { get; init; }

    public required string ModelName { get; init; }

    public decimal? InputPerMillion { get; init; }

    public decimal? OutputPerMillion { get; init; }
}

internal sealed record UpdateModelRequest
{
    public Guid Id { get; init; }

    public required string Label { get; init; }

    public Guid ProviderId { get; init; }

    public required string ModelName { get; init; }

    public decimal? InputPerMillion { get; init; }

    public decimal? OutputPerMillion { get; init; }
}

internal sealed class ListModelsEndpoint(LlmRegistryService registry)
    : EndpointWithoutRequest<IReadOnlyList<ModelItem>>
{
    public override void Configure()
    {
        Get("models");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var models = await registry.ListModelsAsync(ct);
        await Send.OkAsync(models.Select(ModelItem.From).ToList(), ct);
    }
}

internal sealed class CreateModelEndpoint(LlmRegistryService registry)
    : Endpoint<CreateModelRequest, ModelItem>
{
    public override void Configure()
    {
        Post("models");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(CreateModelRequest req, CancellationToken ct)
    {
        try
        {
            var model = await registry.CreateModelAsync(
                req.Label, req.ProviderId, req.ModelName, req.InputPerMillion, req.OutputPerMillion, ct);
            await Send.OkAsync(ModelItem.From(model), ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(r => r.ProviderId, ex.Message);
        }
    }
}

internal sealed class UpdateModelEndpoint(LlmRegistryService registry)
    : Endpoint<UpdateModelRequest, ModelItem>
{
    public override void Configure()
    {
        Patch("models/{id}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(UpdateModelRequest req, CancellationToken ct)
    {
        try
        {
            var model = await registry.UpdateModelAsync(
                req.Id, req.Label, req.ProviderId, req.ModelName, req.InputPerMillion, req.OutputPerMillion, ct);
            await Send.OkAsync(ModelItem.From(model), ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            ThrowError(r => r.ProviderId, ex.Message);
        }
    }
}

internal sealed class DeleteModelEndpoint(LlmRegistryService registry) : Endpoint<DeleteByIdRequest>
{
    public override void Configure()
    {
        Delete("models/{id}");
        Group<AdminGroup>();
    }

    public override async Task HandleAsync(DeleteByIdRequest req, CancellationToken ct)
    {
        try
        {
            await registry.DeleteModelAsync(req.Id, ct);
            await Send.NoContentAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await Send.ResultAsync(Results.Conflict(new { message = ex.Message }));
        }
    }
}
