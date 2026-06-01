namespace Storymark.Api.Prompts;

/// <summary>Seeded defaults for the <c>prompts.*</c> settings. Once seeded, the
/// live values are edited from the admin panel; these are only the first-run text.
/// Mirrors docs/plan.md §19.</summary>
internal static class PromptDefaults
{
    public const string Qa =
        """
        You are Storymark, a spoiler-safe reading assistant.

        The reader is currently at:
        Section: {currentSection}
        Location: {currentLocation}

        Rules:
        - Use only the provided context.
        - Do not use outside knowledge of the book.
        - Do not reveal, imply, foreshadow or hint at future events.
        - If the answer is not available from the provided context, say:
          "That has not been revealed yet."
        - If the context is incomplete, say what is known so far.
        - Keep the answer concise and helpful.
        - The context passages are numbered. When a statement is supported by a
          passage, cite it inline using its number in square brackets, e.g. [1].
          Cite only the passages you actually relied on.

        Question:
        {question}

        Context:
        {context}
        """;

    public const string SectionExtraction =
        """
        Extract structured information from this section.

        Return JSON only, with the following shape:
        {
          "shortSummary": "...",
          "importantEvents": [
            {
              "description": "...",
              "eventType": "arrival | departure | death | betrayal | reveal | conflict | reconciliation | discovery | promise | other",
              "locationStart": 0,
              "locationEnd": 0,
              "involvedEntityNames": ["..."]
            }
          ],
          "entitiesIntroduced": [
            {
              "canonicalName": "...",
              "entityType": "Person | Place | Organization | Object | Event | Concept | Creature | Title | Unknown",
              "aliases": ["..."],
              "firstSeenEvidence": "..."
            }
          ],
          "relationshipDeltas": [
            {
              "fromName": "...",
              "toName": "...",
              "type": "parent | sibling | friend | enemy | lover | ally | rival | mentor | servant | other",
              "polarity": "positive | negative | neutral | complex",
              "evidence": "..."
            }
          ],
          "openThreads": ["..."]
        }

        Use only this section.
        Do not infer future events.
        """;

    public const string EntitySummary =
        """
        Create a spoiler-safe summary of this entity for a reader.

        Use only the supplied context.

        Return JSON:
        - shortSummary
        - knownFacts
        - relationships
        - openQuestions
        - sources

        Do not mention or imply anything beyond the supplied context.
        If very little is known, say that only briefly.
        """;

    public const string SelectionHint =
        """
        The reader has selected: "{selectedText}"

        They are at section {currentSection}, location {currentLocation}.

        Using only the supplied context (which is filtered to content the reader has already seen),
        write 1-2 sentences that help the reader understand what they just selected.

        If the context is empty or unrelated, reply with exactly: "Nothing on this yet."

        Do not foreshadow. Do not reveal future events.

        Context:
        {context}
        """;

    public const string BookMemory =
        """
        Update the book-so-far memory using the existing memory and the latest section summary.

        Return JSON:
        - summarySoFar
        - timeline
        - openThreads
        - resolvedThreads
        - keyEntities
        - importantObjects
        - readerReminders

        Do not include anything beyond the supplied section summaries.
        """;
}
