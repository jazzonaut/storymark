using FastEndpoints;
using Storymark.Api.Domain.Llm;
using Storymark.Api.Services.Llm;

namespace Storymark.Api.Endpoints.Debug;

internal sealed record PostChatRequest
{
    public required string Prompt { get; init; }

    public string? System { get; init; }
}

internal sealed record PostChatResponse
{
    public required string Provider { get; init; }

    public required string Model { get; init; }

    public required string Content { get; init; }

    public int? TokensIn { get; init; }

    public int? TokensOut { get; init; }
}

/// <summary>Routes a raw prompt through a task's configured model and records the
/// call. For verifying the LLM pipeline end-to-end. Gated by
/// <c>Storymark:DebugEndpointsEnabled</c>; returns 404 when disabled.</summary>
internal sealed class PostChatEndpoint(IConfiguration configuration, ChatModelRouter router)
    : Endpoint<PostChatRequest, PostChatResponse>
{
    public override void Configure()
    {
        Post("_debug/chat");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PostChatRequest req, CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Storymark:DebugEndpointsEnabled"))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var task = Query<string>("task", isRequired: true);
        if (string.IsNullOrWhiteSpace(task))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(req.System))
        {
            messages.Add(new ChatMessage { Role = ChatRole.System, Content = req.System });
        }

        messages.Add(new ChatMessage { Role = ChatRole.User, Content = req.Prompt });

        var completion = await router.CompleteAsync(task, messages, LlmCallMetadata.None, ct);

        await Send.OkAsync(
            new PostChatResponse
            {
                Provider = completion.Provider,
                Model = completion.Model,
                Content = completion.Content,
                TokensIn = completion.TokensIn,
                TokensOut = completion.TokensOut,
            },
            ct);
    }
}
