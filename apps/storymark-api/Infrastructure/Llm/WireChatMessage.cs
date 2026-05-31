using System.Text.Json.Serialization;
using Storymark.Api.Domain.Llm;

namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The OpenAI-style <c>{role, content}</c> message object shared by the
/// Ollama and OpenAI-compatible wire formats (both request and response).</summary>
internal sealed record WireChatMessage
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    public static WireChatMessage From(ChatMessage message) =>
        new() { Role = message.Role.ToWireValue(), Content = message.Content };

    public static List<WireChatMessage> From(IReadOnlyList<ChatMessage> messages) =>
        messages.Select(From).ToList();
}
