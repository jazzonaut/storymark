namespace Storymark.Api.Domain.Llm;

internal static class ChatRoleExtensions
{
    /// <summary>The role's wire value as expected by Ollama and OpenAI-compatible APIs.</summary>
    public static string ToWireValue(this ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown chat role."),
    };
}
