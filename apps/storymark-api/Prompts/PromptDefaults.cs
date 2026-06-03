namespace Storymark.Api.Prompts;

/// <summary>Seeded defaults for the <c>prompts.*</c> settings. Once seeded, the
/// live values are edited from the admin panel; these are only the first-run text.
/// Covers the prompt set described in docs/plan.md §19.</summary>
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
        - Answer from the context when any provided passage supports a useful answer.
        - If the provided context supports only a partial answer, state only what is
          known so far.
        - If no provided passage supports an answer, say exactly:
          "That has not been revealed yet."
        - Keep the answer concise and helpful.
        - The context passages are numbered. When a statement is supported by a
          passage, cite it inline using its number in square brackets, e.g. [1].
          Cite only the passages you actually relied on.
        - If a conversation so far is shown, read the question as a follow-up:
          resolve references like "she" or "that scene" against it, but support
          every statement from the context passages, not from earlier answers.
        - If the reader selected a passage, the question is about that passage.

        {selection}
        {history}
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
              "eventType": "arrival",
              "evidence": "...",
              "involvedEntityNames": ["..."]
            }
          ],
          "entitiesIntroduced": [
            {
              "canonicalName": "...",
              "entityType": "Person",
              "aliases": ["..."],
              "firstSeenEvidence": "..."
            }
          ],
          "relationshipDeltas": [
            {
              "fromName": "...",
              "toName": "...",
              "type": "friend",
              "polarity": "positive",
              "evidence": "..."
            }
          ],
          "openThreads": ["..."]
        }

        Use only this section.
        Do not infer future events.
        Allowed eventType values: arrival, departure, death, betrayal, reveal,
        conflict, reconciliation, discovery, promise, other.
        Allowed entityType values: Person, Place, Organization, Object, Event,
        Concept, Creature, Title, Unknown.
        Allowed relationship type values: parent, sibling, friend, enemy, lover,
        ally, rival, mentor, servant, other.
        Allowed polarity values: positive, negative, neutral, complex.
        For each entity, set canonicalName to the fullest form of its name that appears
        in this section — prefer a character's full name over a bare surname or an
        honorific form. List every other form this section uses for that same entity —
        shorter names, surnames, honorific forms ("Mr. Kovacs"), nicknames, and epithets
        — in aliases, so one person, place, or thing is never split across several
        entries. Include only forms that actually appear in this section; do not invent
        names or draw on knowledge from outside the section.
        Each event's evidence must be a short verbatim quote copied exactly from SECTION
        TEXT at the point the event occurs — it is used to locate the event in the text.
        Do not paraphrase, summarise, or invent the quote; copy it character for character.
        """;

    public const string EntitySummary =
        """
        Create a spoiler-safe summary of the named ENTITY for a reader, using only the
        supplied MENTIONS and CONTEXT (which are filtered to content the reader has
        already seen).

        Return a single JSON object only — no markdown fences, no commentary:
        {
          "shortSummary": "one or two sentences",
          "knownFacts": ["..."],
          "relationships": ["how they relate to other characters, one per item"],
          "openQuestions": ["what the reader might still be wondering"]
        }

        Do not mention or imply anything beyond the supplied context.
        Do not foreshadow or reveal future events.
        If very little is known, say that briefly in shortSummary and leave the lists empty.
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
        Update the book-so-far memory using the existing memory summary and the latest
        section summary. This memory is shown to a reader as "the story so far", so it
        should be concise, chronological, and spoiler-safe.

        Return one JSON object only, with this shape:
        {
          "summarySoFar": "a concise cumulative summary of the story so far",
          "timeline": ["major events in reading order"],
          "openThreads": ["unresolved questions, mysteries, promises, or tensions"]
        }

        Preserve important prior facts from the existing memory.
        Add only information supported by the latest section summary.
        Do not include anything beyond the supplied summaries.
        Do not use markdown fences, commentary, or bullet lists outside the JSON.
        """;
}
