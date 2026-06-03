namespace Storymark.Api.Services.Retrieval;

/// <summary>The single in-memory definition of "past the reader's boundary": content
/// whose end lies beyond <c>(currentSection, currentLocation)</c>. Call sites derive
/// <c>LlmCallMetadata.UsedFutureContent</c> from it instead of hardcoding false, so the
/// audit flag fires if a query filter ever regresses. The SQL filters express the
/// complement inline because EF must translate them.</summary>
internal static class SpoilerBoundary
{
    public static bool IsBeyond(int sectionIndex, int locationEnd, int currentSection, int currentLocation) =>
        sectionIndex > currentSection
        || (sectionIndex == currentSection && locationEnd > currentLocation);
}
