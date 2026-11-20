namespace Storymark.Api.Prompts;

/// <summary>Seeded defaults for the <c>prompts.*</c> settings. Startup updates defaults that are
/// still seed-authored. Operator edits take precedence.
/// Covers the prompt set described in docs/plan.md §19.</summary>
internal static class PromptDefaults
{
    public static readonly IReadOnlyDictionary<string, string> ByKey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["prompts.qa"] = Qa,
        ["prompts.sectionExtraction"] = SectionExtraction,
        ["prompts.entitySummary"] = EntitySummary,
        ["prompts.selectionHint"] = SelectionHint,
        ["prompts.bookMemory"] = BookMemory,
        ["prompts.currentScene"] = CurrentScene,
    };

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

        Use SECTION TEXT as your only source. The result feeds a timeline, an entity index, and a relationship graph. Check all three independently, from the beginning through the end of the section.

        Timeline: one short sentence per distinct consequential action, choice, discovery, promise, arrival, departure, or change in stakes. Group the movements leading to one outcome; keep distinct outcomes even when close together. Attribute memories, suspicions and reported events to the person remembering, suspecting or reporting. Use "the narrator" when their name is not given. Do not guess anyone's gender.
        Entity index: cover every named person, place, organization, creature, and other named referent, including those only mentioned. Include distinctive unnamed objects and roles relevant to the story. One entry per referent. Keep the fullest attested name and only other attested names, nicknames or distinctive titles as aliases. A pronoun or generic noun is not an alias.
        Relationships: look for family, authority, trust, allegiance, hostility, debt, service, rivalry, secrecy, or cooperation, including what dialogue and actions show. A link can matter even if already covered by an event. Record each supported link once per change. A shared location alone is insufficient. Use consistent names across arrays.
        Evidence (events, entities and relationships): select 3–8 adjacent words from inside one source sentence. Pick words that identify the specific event, first mention, or link. Copy the exact characters, preserving case and punctuation. Omit surrounding dialogue quotation marks; never reconstruct a sentence, add an ellipsis, or join fragments. Example: for She said, “I left the key under the loose stone,” copy "key under the loose stone". Use a shorter quote for a short utterance.
        Use a brief shortSummary and only unresolved threads raised in the text. Empty arrays are valid.

        Allowed eventType: arrival, departure, death, betrayal, reveal, conflict, reconciliation, discovery, promise, other.
        Allowed entityType: Person, Place, Organization, Object, Event, Concept, Creature, Title, Unknown.
        Allowed relationship type: parent, sibling, friend, enemy, lover, ally, rival, mentor, servant, other.
        Allowed polarity: positive, negative, neutral, complex.
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
        Use the text's degree of certainty. Do not turn a general duty into something the entity has personally done to a particular character. Each list item must be about ENTITY. Cover the connections the text shows between ENTITY and other named characters. In each relationship item name both parties and what connects them; a title, a greeting by itself, or a fact about a third party is not a relationship. Being mentioned near someone is insufficient. Include openQuestions only for an uncertainty about ENTITY raised by the text. If the only evidence is a passing mention, give that observation in shortSummary and return [] for all three lists.
        """;

    public const string SelectionHint =
        """
        Explain the selected phrase in one or two sentences for a reader at section {currentSection}, location {currentLocation}. Use only the Context passages. If they contain no passage that explains the selection, reply exactly: Nothing on this yet.

        Example: selection "missing bell", context about a bakery with no bell mentioned → Nothing on this yet.
        When evidence exists, describe what the phrase means for the character in that passage. Do not speculate or foreshadow.

        Selected phrase: "{selectedText}"
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
          "summarySoFar": "a concise cumulative account distinguishing what happened from what was remembered",
          "timeline": ["major events in reading order"],
          "openThreads": ["unresolved questions, mysteries, promises, or tensions"]
        }

        Preserve important prior facts from the existing memory.
        Follow the section summary's narrative frame. When the summary moves from a memory to current action, use separate clauses: what the character recalls, then what they do now. The Events list gives supporting details, not a new frame. Preserve the distinction in both summarySoFar and timeline. Carry forward only unresolved threads already raised in the inputs. Remove resolved threads. If no unresolved thread is stated, return an empty openThreads array; do not invent reader questions.
        Do not include anything beyond the supplied summaries.
        Do not use markdown fences, commentary, or bullet lists outside the JSON.
        """;

    public const string CurrentScene =
        """
        Create a spoiler-safe current-scene card for a distracted reader.

        The reader is currently at:
        Section: {currentSection}
        Location: {currentLocation}

        Return one JSON object only — no markdown fences, no commentary:
        {
          "summary": "2-4 plain sentences about what is happening right now",
          "presentEntities": [
            { "name": "...", "role": "what they are doing or why they matter in this scene" }
          ],
          "place": "where the scene appears to be happening, or null",
          "immediateTension": "the immediate question, risk, or conflict, or null",
          "relevantThreads": ["a supplied thread actively pursued in the last scene"],
          "recentChange": "what just changed in the recent passage, or null",
          "suggestedQuestions": ["Who is ...?", "Why are they ...?", "What did I miss?"]
        }

        Rules:
        - Use only the supplied context.
        - Do not use outside knowledge of the book.
        - Do not reveal, imply, foreshadow, or hint at future events.
        - Anchor the card at the end of Recent passage. A memory, a story someone tells, or an earlier location is background.
        - In presentEntities list only participants physically present in the current scene. Exclude people who are merely remembered, mentioned or expected.
        - Include a thread only if the recent passage connects it to the current action. A thread appearing in memory alone is insufficient.
        - For recentChange give the latest supported change in this scene. Avoid guesses about identities, motives or unnamed objects.
        - Prefer concrete character goals, location, conflict, and recent changes.
        - If a field is unknown from the context, use null or an empty array.
        - Test each relevantThreads item against the last scene: is someone there pursuing or discussing it now? If not, omit it. A possible thematic connection is insufficient.
        - Use null for recentChange when the passage only describes a state; waiting alone is a state, someone arriving is a change.
        - Keep suggested questions short and useful for a reader who lost focus.

        Recent passage:
        {recentPassage}

        Recent visible events:
        {recentEvents}

        Story-so-far memory:
        {memory}

        Earlier open threads (background):
        {openThreads}

        Before answering, check each field against the END of Recent passage. Copy a thread into relevantThreads only if a specific current action or line of dialogue addresses that thread. Otherwise return []. For example, a character searching a drawer for missing letters can connect to a thread about those letters; packing a bag with no mention of the letters cannot.
        """;
}
