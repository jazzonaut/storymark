namespace Storymark.Api.Infrastructure.Llm;

/// <summary>Thrown when a provider reports the requested model is not available —
/// Ollama answers <c>404</c> from <c>/api/embed</c> and <c>/api/chat</c> when the
/// model has not been pulled. This is deterministic: retrying without operator
/// action just fails the same way, so callers fail the job fast (and surface a
/// clear reason) rather than exhausting the Hangfire retry schedule.</summary>
internal sealed class ModelNotFoundException(string provider, string model)
    : Exception(BuildMessage(provider, model))
{
    public string Provider { get; } = provider;

    public string Model { get; } = model;

    private static string BuildMessage(string provider, string model)
    {
        var action = string.Equals(provider, "ollama", StringComparison.Ordinal)
            ? $"Pull it first: docker exec -it storymark-ollama ollama pull {model}"
            : "Make sure the model is downloaded/available on the provider.";
        return $"Model '{model}' is not available on the '{provider}' provider. {action}";
    }
}
