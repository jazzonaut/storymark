namespace Storymark.Api.Domain.Books;

/// <summary>The kind of a Story Guide entity (docs/plan.md §12.8). Persisted as
/// text. <see cref="Unknown"/> is the fallback when extraction can't classify
/// an entity (e.g. an unresolved relationship endpoint).</summary>
internal enum EntityType
{
    Person,
    Place,
    Organization,
    Object,
    Event,
    Concept,
    Creature,
    Title,
    Unknown,
}
