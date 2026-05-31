namespace Storymark.Api.Domain.Books;

/// <summary>Maps between image file extensions and MIME types for the cover and
/// inline-resource artifacts. Pure (no IO/DI), so endpoints, services, and storage
/// can all share it. The known set is the allow-list of image types we serve —
/// <see cref="ContentTypeForExtension"/> returns <c>null</c> for anything else,
/// which the resource endpoint treats as not-found (only images are served).</summary>
internal static class ImageContentTypes
{
    public static string? ContentTypeForExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => null,
        };

    /// <summary>Extension (with leading dot) for a manifest MIME type, or
    /// <c>null</c> if it is not an image type we recognise.</summary>
    public static string? ExtensionForMime(string mimeType) =>
        mimeType.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            _ => null,
        };
}
