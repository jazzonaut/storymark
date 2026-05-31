# Naming Conventions

## Files

| What                        | Case           | Example                          |
| --------------------------- | -------------- | -------------------------------- |
| TypeScript modules          | `kebab-case`   | `use-books.ts`, `books-api.ts`   |
| Vue SFCs                    | `PascalCase`   | `BookCard.vue`, `MemoryPanel.vue`|
| Vue view modules            | `PascalCase`   | `LibraryView.vue`                |
| Test fixtures (when tests return) | `kebab-case` | `pride-and-prejudice.epub`   |
| C# files                    | `PascalCase`   | `UploadBookEndpoint.cs`          |
| Markdown                    | `kebab-case`   | `vision-and-architecture.md`     |
| ADRs                        | `NNNN-kebab`   | `0003-pgvector-not-qdrant.md`    |
| Config / dotfiles           | as-is          | `.oxlintrc.json`, `tsconfig.json`|

## Identifiers

| Kind                       | Case           | Notes                          |
| -------------------------- | -------------- | ------------------------------ |
| TypeScript types/interfaces| `PascalCase`   | No `I` prefix                  |
| TS variables, functions    | `camelCase`    |                                |
| TS constants (module-level, immutable, non-trivial) | `SCREAMING_SNAKE_CASE` | Reserve for truly constant config |
| C# types                   | `PascalCase`   |                                |
| C# interfaces              | `IPascalCase`  | .NET convention                |
| C# methods, properties     | `PascalCase`   |                                |
| C# private fields          | `_camelCase`   | underscore prefix              |
| C# parameters, locals      | `camelCase`    |                                |
| C# constants               | `PascalCase`   | .NET convention, not SCREAMING |

## Booleans

Always a question:

- `isLoading`, `hasCachedSummary`, `canEdit`, `shouldRetry`
- Never `loading: boolean` or `edit: boolean`.

Negative names are forbidden:

- ❌ `isNotReady`, `noAccess`
- ✅ `isReady` (and use `!isReady`), `hasAccess`

## Async

- Promise-returning TS functions: no suffix. The return type tells you.
- C# async methods: `Async` suffix. Convention; `dotnet build` warns without it.

## Domain words

These are the canonical terms — use them consistently across code, docs, prompts, and UI:

| Term                     | Meaning                                                                 |
| ------------------------ | ----------------------------------------------------------------------- |
| **Section**              | A unit of reading-order content. For EPUB this is a chapter; for other formats may be a logical sub-unit. Never "chapter" in code or schemas. |
| **Book entity / entity** | A character, place, organisation, object, event, concept, creature, or title. Internally `BookEntity`; user-facing "Story Guide entry." |
| **Position**             | `(sectionIndex, locationStart, locationEnd)`. The unit of progress and the substrate of spoiler-safe filtering. |
| **Memory snapshot**      | A rolling "summary so far" at a section boundary. Powers the recap card. |
| **Hint**                 | Output of the tiered selection popover. Cached entity card OR fast-model gloss. |
| **Q&A** / **Ask**        | Full-retrieval free-text question. Distinct from "hint." |
| **Spoiler-safe**         | Filtered so that no content at a position later than the reader's current position is used. |
| **Task** (in LLM context)| One of `qa`, `extract`, `entity_summary`, `selection_hint`, `memory`, `embed`. The unit of per-task model routing. |
| **Provider**             | A vendor of LLM inference (Ollama, OpenAI, Groq, …). |
| **Model**                | A specific deployed model (`llama3.2:3b`, `gpt-4o-mini`). |

When the UI and code disagree on a name, the code wins and the UI updates.

## Forbidden patterns

- **Hungarian notation** beyond C#'s `I` prefix for interfaces. No `strFoo`, `arrBars`.
- **Pluralizing types**: `Books` for a collection type. Use `Book[]` / `IReadOnlyList<Book>` and name the *variable* plural (`books`).
- **`Manager`, `Helper`, `Util`** in class names. They almost always indicate a class with no real responsibility. Find the actual concept and name it.
- **`Data`, `Info`** suffixes on types. `Book`, not `BookData`.
- **Numeric suffixes** to disambiguate (`BookEntity2`). Rename one of them.
