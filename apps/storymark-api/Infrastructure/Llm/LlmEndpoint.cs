namespace Storymark.Api.Infrastructure.Llm;

/// <summary>The connection details a client needs for one call, resolved from the
/// model's provider (ADR-0016). Passed per call so two tasks can target two
/// different hosted endpoints.</summary>
internal sealed record LlmEndpoint
{
    public required string BaseUrl { get; init; }

    public string? ApiKey { get; init; }

    /// <summary>Resolves a relative API path against the base URL, tolerating a
    /// trailing slash either way. The one place base-URL normalization lives.</summary>
    public Uri ResolveUri(string path) => new(new Uri(BaseUrl.TrimEnd('/') + "/"), path);
}
