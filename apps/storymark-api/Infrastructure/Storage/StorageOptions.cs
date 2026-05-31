namespace Storymark.Api.Infrastructure.Storage;

/// <summary>Where book artifacts live on disk (docs/plan.md §14). Bound from the
/// <c>Storage</c> configuration section; the compose stack mounts a volume at
/// <c>/data</c> and sets <c>Storage__RootPath</c>.</summary>
internal sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string RootPath { get; init; } = "/data";
}
