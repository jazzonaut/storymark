# Storymark — Implementation Plan

## 1. Product Summary

**Storymark** is an offline-first cross-platform reading app for people who enjoy books but sometimes forget character names, past events, places, relationships, and plot threads.

The app should feel like a clean, general-purpose book reader first, with smart memory features built in:

- Read books comfortably on phone, tablet, or web — fully offline once a book is opened.
- Import books into a homelab backend.
- Cache and index each book once.
- Track reading progress, synced across devices.
- Ask free-text questions about the book.
- Get spoiler-safe reminders about characters, places, factions, objects, and events.
- Select any passage to get an instant cached hint or a fast 1B-model gloss.
- Generate chapter summaries, "previously in this book" recaps, character/entity cards, a visual **timeline of events**, and a visual **relationship graph**.
- Show a dismissible recap card at every section boundary.
- Avoid spoilers by only using content the reader has already reached.
- Manage the whole thing from an **operator-grade admin panel**: LLM providers, per-task model selection, editable prompts, job dashboard, LLM call log, cost meter, per-book ops.

The MVP starts with **EPUB**, but the architecture supports other formats later.

Potential future import formats: EPUB, PDF, TXT, HTML, Markdown, DOCX, MOBI/AZW (where legal), web articles, audiobook transcripts.

Storymark is positioned as a **reader with memory**, not an AI chatbot.

It is also intended to **replace Kindle/Readera** as the user's daily reading app, so reader polish (Readera-like UI, paginated reading, bookmarks, typography control, offline reliability) is a first-class concern.

---

## 2. Product Positioning

### App Name

**Storymark**

### Possible Taglines

- "Never lose the thread."
- "A smarter way to keep your place."
- "Your memory for every story."
- "Read, remember, continue."
- "A book reader that remembers with you."

### Core User Problem

The reader forgets:

- who a character is
- why a character matters
- what happened in previous chapters
- why someone was arrested, exiled, angry, missing, or important
- where a place was introduced
- what an object, faction, or event refers to
- which plot threads are unresolved
- who is connected to whom

### Core Product Promise

Storymark helps the reader stay immersed by answering:

- "Who is this?"
- "Where are they?"
- "What happened last time?"
- "Why does this matter?"
- "What do I know about this so far?"
- "What happened that led to this?"
- "How are these people connected?"

without revealing anything beyond the current reading position.

---

## 3. Chosen Stack

### Frontend

- Vue 3
- TypeScript — **strict mode**, no `any`, no implicit fallbacks
- Vite
- pnpm workspaces with a **catalog of exact-pinned versions** (no `^`/`~`)
- Tailwind CSS (reader)
- PrimeVue (admin panel)
- **oxlint** for linting and **oxfmt** for formatting (no ESLint, no Prettier — see [`adr/0002-oxlint-only.md`](adr/0002-oxlint-only.md))
- Capacitor for Android/iOS (Phase 7)
- Composables for state management (no Pinia)
- **Dexie / IndexedDB** for offline book storage and AI artifact cache
- **ECharts** for timeline and relationship graph
- Service Worker for installable PWA + offline shell

> Tests are deferred until after Phase 6. See [`adr/0012-tests-deferred.md`](adr/0012-tests-deferred.md).

### Backend

- .NET / ASP.NET Core (latest LTS)
- `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`
- **Central Package Management** (`Directory.Packages.props`) with exact-pinned versions
- **Meziantou.Analyzer** for code-quality gates
- `dotnet format --verify-no-changes` enforced in CI
- FastEndpoints for API endpoints
- Entity Framework Core
- **PostgreSQL with `pgvector`** for app data AND vector search
- **Hangfire on Postgres** for background job processing
- `IChatModelClient` abstraction with two adapters out of the box:
  - `OllamaClient` (local CPU inference)
  - `OpenAiCompatibleClient` (covers OpenAI, Groq, OpenRouter, vLLM, LM Studio, Anthropic via litellm)
- VersOne.Epub for initial EPUB parsing
- AngleSharp for HTML sanitization
- Per-task model selection (extraction / summarization / Q&A / embeddings) configured in DB
- Docker Compose deployed through Portainer (or any other Compose-capable host)

### Deployment Target

- Linux host (the reference design assumes a modest homelab: i5-class CPU, ~24 GB RAM)
- Container orchestration via Docker Compose (Portainer-friendly, but any Compose-capable host works)
- CPU-only AI inference (local Ollama); optional hosted providers via admin panel
- Reverse-proxy and auth layer are **pluggable** — see [`deployment/03-reverse-proxy-options.md`](../deployment/03-reverse-proxy-options.md) and [`deployment/04-auth-options.md`](../deployment/04-auth-options.md)

Recommended local models:

- `llama3.2:3b`
- `qwen2.5:3b`
- `llama3.2:1b` for fast extraction and selection hints
- `nomic-embed-text` for embeddings

---

## 4. High-Level Architecture

```text
Storymark Client (PWA, Capacitor later)
  Vue 3 + Vite + Tailwind + PrimeVue + ECharts
  Dexie/IndexedDB
    - sections (offline reading)
    - cached summaries, entity cards
    - cached timeline + graph snapshots
    - pending progress writes
        |
        | REST API (bearer token over Tailscale/LAN)
        v
Storymark API
  ASP.NET Core + FastEndpoints
  Hangfire job runner
  IChatModelClient (Ollama / OpenAI-compatible)
        |
        |---------------- PostgreSQL (+ pgvector)
        |                 books, sections, chunks (+ embeddings),
        |                 entities, events, relationships,
        |                 summaries, memory snapshots,
        |                 progress, bookmarks, settings, llm_calls
        |
        |---------------- Ollama / Hosted LLM
        |                 chat + embeddings via abstraction
        |
        |---------------- File Volume (/data)
                          original books, covers, extracted content
```

The backend is the canonical source of truth for everything. The client is a fully offline-capable replica that caches whatever it has seen.

When the client is offline:

- already-downloaded books are fully readable
- already-generated summaries, entity cards, timeline and graph snapshots up to current position remain viewable
- new Q&A, new section processing, new visualisations are unavailable until reconnect
- progress writes are queued locally and flushed on reconnect

---

## 5. Core Product Concepts

### 5.1 Library

The user's imported books.

Each book has:

- title
- author
- cover
- format
- processing status
- reading progress
- last opened date
- summary availability
- Story Guide availability
- offline-cached status (client side)

### 5.2 Reader

The main reading experience. Paginated via CSS columns, swipe between virtual pages; matches Readera-style page-flip feel.

Features:

- readable paginated section view
- typography controls (font, size, line height, width, paragraph spacing)
- theme: light, sepia, dark
- progress tracking by (sectionIndex, paragraphIndex, charOffsetInParagraph)
- section navigation
- responsive mobile-first layout
- optional entity highlights (off / subtle / always)
- bookmarks (MVP)
- selection popover with tiered hint flow
- "Memory" button: tabbed panel with Previously / Summary / Timeline / Graph / Story Guide / Assistant
- automatic dismissible **section-boundary recap card** when entering a new section
- "Previously" recap on book reopen after >24h

### 5.3 Story Guide

The user-facing name for the entity system.

It includes:

- characters
- places
- groups/factions
- objects
- events
- concepts
- titles/roles
- creatures, if relevant

Internally these are called `BookEntity`.

User-facing labels:

- "Story Guide"
- "People & Places"
- "Mentioned here"
- "What you know so far"

### 5.4 Entity Popups

When the reader taps a known name/place/object/concept, Storymark shows a short spoiler-safe popup.

Example:

```text
Paul Atreides
Person

Duke Leto's son and Jessica's child. He has received unusual training and is being tested by the Bene Gesserit.

Known relationships:
- Son of Jessica
- Son of Duke Leto

First seen: Chapter 1
Sources: Chapter 1, Chapter 2
```

The popup must only use content up to the reader's current location.

### 5.5 Selection Hint (Tiered)

When the reader selects arbitrary text, a popover appears with quick chips: **Who/What is this? · Ask…**

The interaction is tiered for latency:

1. If the selected text matches a known entity canonical name or alias → return the **cached** entity card instantly. Zero LLM call.
2. Otherwise the popover shows an inline 1B-model "hint" (1-2 sentences) generated against a tight spoiler-safe context window. ~1-3s on CPU.
3. The user can escalate to full Q&A from the same popover.

### 5.6 Previously Panel + Section Recap Card

A short recap shown:

- automatically as a dismissible card when entering a new section
- automatically on book reopen after a >24h gap
- on demand from the Memory panel

Example:

```text
Previously in this book:
- Paul and Jessica escaped danger.
- Yueh betrayed the Atreides but tried to help Duke Leto.
- The Harkonnens believe they are regaining control.
- Jessica suspects deeper political forces are involved.
```

The card is precomputed at section processing time from the `book_memory_snapshots` for that section boundary, so entering the section never blocks on the LLM.

### 5.7 Visual Timeline

A chronological event view rendered with ECharts.

Each event is `{description, sectionIndex, locationStart, eventType, involvedEntityIds}` derived from per-section structured extraction (§19.2). The timeline is filtered to events whose position ≤ current reading position. Tapping an event navigates to its source location.

### 5.8 Relationship Graph

A force-directed graph rendered with ECharts.

Nodes are entities (people, places, factions, objects). Edges are `book_relationships` rows with type, polarity (positive/negative/neutral/complex), and evidence snippet. Filtered to relationships first seen ≤ current position. Tapping a node opens its entity card.

### 5.9 Ask Anything

The reader can ask free-text questions.

Examples:

- "What did character X do that he got put in jail?"
- "Why is everyone angry at Y?"
- "What is the deal with the necklace?"
- "Who are the Bene Gesserit?"
- "Where did we first hear about Arrakis?"
- "What happened to the letter?"
- "Why does she distrust him?"

The backend uses hybrid retrieval (semantic + keyword + summary + entity facts) and spoiler-safe filtering before calling the LLM.

### 5.10 Bookmarks

Lightweight, MVP. One row per `(book, section, locationStart, label)`. Bookmarks panel groups by chapter and jumps on tap. Reuses progress offset machinery.

### 5.11 Admin Panel

An operator-grade dashboard at `/admin` (PrimeVue):

- **Settings**: providers, per-task models, prompts (editable + diff'd), feature flags, upload limits, embedding model with warn-and-reindex.
- **Job dashboard**: Hangfire's own `/hangfire` UI embedded or linked.
- **LLM call log**: last N prompts / responses / latencies / token counts per task. Critical for debugging spoiler leaks, bad summaries, slow models.
- **Cost meter**: when a hosted provider is selected, show estimated tokens and $ per book per task.
- **Per-book ops**: re-index, regenerate summaries, view entity list, delete book.

---

## 6. MVP Requirements

### Must Have

- Import EPUB into backend
- Store original EPUB on backend
- Deduplicate books by SHA-256 hash
- Extract metadata, cover, spine, table of contents, sections
- Serve sanitized section HTML to frontend
- Paginated reader (CSS columns)
- Whole-book download to client IndexedDB on first open
- Fully offline reading after download
- Track reading position by section + paragraph + char offset
- Backend-authoritative last-write-wins progress sync; queue offline writes
- Bookmarks (CRUD + panel)
- Split section text into chunks
- Generate embeddings for chunks (versioned with embedding model id)
- Store vectors in `pgvector` with spoiler-safe metadata
- Per-section structured extraction: summary + events + relationship deltas + entities
- Spoiler-safe Q&A endpoint with hybrid retrieval and sources
- Section-boundary recap card (auto, dismissible)
- Tap entity → spoiler-safe summary popup
- **Selection popover with tiered hint flow** (cached card → 1B hint → escalate)
- **Visual timeline** of events up to current position
- **Visual relationship graph** of entities up to current position
- **Admin panel**: settings, prompts editor, LLM call log, cost meter, per-book ops
- Settings persisted in DB; hot-reloaded; per-task model selection
- Single bearer-token auth
- Hangfire-backed job processing with priority queues
- Enforce spoiler safety before any LLM call

### Should Have

- Section entity highlight toggle: off / subtle / always
- "Previously" recap on reopen after >24h
- Embedding-model swap UX with warn-and-reindex flow
- Full-text keyword search over chunks and summaries
- Jump from answer/event source to section location

### Not for MVP

- DRM support
- PDF, MOBI/AZW formats
- Highlights, notes (Phase 7)
- Multi-user accounts
- Cloud sync beyond LAN/Tailscale
- Native desktop app
- Running the LLM on Android
- Fine-tuning
- TTS

---

## 7. Format Strategy

EPUB is the MVP format, but the backend should not hard-code EPUB everywhere.

Use a format abstraction.

```csharp
public interface IBookImportHandler
{
    string FormatName { get; }
    bool CanHandle(BookImportFile file);
    Task<BookImportResult> ImportAsync(BookImportFile file, CancellationToken ct);
}
```

Initial implementation: `EpubImportHandler`.

Future: `PdfImportHandler`, `TextImportHandler`, `HtmlImportHandler`, `MarkdownImportHandler`, `DocxImportHandler`.

Normalized internal model:

```text
Book
  Sections
    Paragraphs
      Text offsets
      Optional sanitized HTML
```

For EPUB, a section is usually a chapter. For PDF, a section might be a page or heading-derived. For TXT, sections may be heuristic.

This keeps the reader, AI pipeline, summaries, entities, events, relationships, progress tracking, and retrieval format-agnostic.

---

## 8. Repository Structure

```text
storymark/
  README.md
  STORYMARK_PLAN.md
  docker-compose.yml

  apps/
    storymark-app/
      package.json
      vite.config.ts
      tailwind.config.ts
      capacitor.config.ts          # Phase 7
      src/
        main.ts
        App.vue
        router/
        assets/
        styles/
        components/                # reader (Tailwind)
        admin/                     # PrimeVue
        views/
        composables/
        api/
        db/                        # Dexie schemas
        sw/                        # service worker
        types/
        utils/

  backend/
    Storymark.Api/
      Storymark.Api.csproj
      Program.cs
      appsettings.json
      appsettings.Development.json
      Dockerfile
      Endpoints/
      Domain/
      Infrastructure/
      Services/
      Jobs/
      Prompts/
      Storage/
      Data/
      Migrations/

  packages/
    shared-types/
      package.json
      src/
```

Frontend state lives in composables, not Pinia.

```text
src/composables/
  useBooks.ts
  useReader.ts
  useReadingProgress.ts
  useAssistant.ts
  useStoryGuide.ts
  useTimeline.ts
  useRelationshipGraph.ts
  useSelectionHint.ts
  useBookmarks.ts
  useUpload.ts
  useSettings.ts
  useOfflineSync.ts
  useAdmin.ts
```

---

## 9. Frontend Plan

### 9.1 Views

```text
views/
  LibraryView.vue
  BookUploadView.vue
  ReaderView.vue
  AssistantView.vue
  StoryGuideView.vue
  TimelineView.vue
  RelationshipGraphView.vue
  BookmarksView.vue
  SettingsView.vue
  admin/
    AdminShell.vue
    SettingsEditorView.vue
    PromptsEditorView.vue
    LlmCallLogView.vue
    CostMeterView.vue
    BookOpsView.vue
    JobDashboardView.vue
```

### 9.2 Components

```text
components/
  library/
    BookCard.vue
    UploadBookButton.vue
    ProcessingStatusBadge.vue
    OfflineBadge.vue

  reader/
    ReaderShell.vue
    PaginatedSection.vue
    ReaderToolbar.vue
    ProgressSlider.vue
    TableOfContents.vue
    EntityHighlight.vue
    EntityPopup.vue
    SelectionPopover.vue
    SectionRecapCard.vue
    PreviouslyCard.vue
    MemoryPanel.vue
    BookmarkButton.vue
    BookmarksPanel.vue

  assistant/
    AssistantPanel.vue
    QuestionInput.vue
    AnswerCard.vue
    SourceList.vue

  story-guide/
    StoryGuidePanel.vue
    EntityCard.vue
    EntitySearch.vue
    EntityTypeBadge.vue

  timeline/
    TimelineChart.vue          # ECharts
    EventDetailCard.vue

  graph/
    RelationshipGraphChart.vue # ECharts
    NodeDetailCard.vue
```

### 9.3 State via Composables

No Pinia. Use module-level refs where state should be shared.

```ts
// useBooks.ts
import { ref, computed } from 'vue'
import { booksApi } from '@/api/booksApi'
import type { BookSummary } from '@/types/books'

const books = ref<BookSummary[]>([])
const isLoading = ref(false)

export function useBooks() {
  async function loadBooks() {
    isLoading.value = true
    try {
      books.value = await booksApi.list()
    } finally {
      isLoading.value = false
    }
  }

  const sortedBooks = computed(() =>
    [...books.value].sort((a, b) => a.title.localeCompare(b.title))
  )

  return { books, sortedBooks, isLoading, loadBooks }
}
```

### 9.4 API Client Layer

```text
src/api/
  http.ts                  # base URL, bearer token, retry, cancel
  booksApi.ts
  readerApi.ts
  assistantApi.ts
  storyGuideApi.ts
  timelineApi.ts
  graphApi.ts
  selectionApi.ts
  bookmarksApi.ts
  progressApi.ts
  adminApi.ts
  settingsApi.ts
```

### 9.5 Frontend Storage (Dexie / IndexedDB)

Schema lives in `src/db/storymarkDb.ts`. Mirrors the backend at a coarse grain:

```ts
class StorymarkDb extends Dexie {
  books!: Table<CachedBook, string>
  sections!: Table<CachedSection, [string, number]>      // (bookId, sectionIndex)
  paragraphs!: Table<CachedParagraph, string>
  sectionSummaries!: Table<CachedSectionSummary, [string, number]>
  memorySnapshots!: Table<CachedMemorySnapshot, [string, number, number]>
  entityCards!: Table<CachedEntityCard, string>
  events!: Table<CachedEvent, string>
  relationships!: Table<CachedRelationship, string>
  bookmarks!: Table<Bookmark, string>
  pendingProgress!: Table<PendingProgress, string>       // offline write queue
  settings!: Table<UiSetting, string>                    // theme, font etc
}
```

Rules:

- Whole book is downloaded on first open: sections + already-generated AI artifacts.
- Subsequent opens: reconcile delta only.
- Pending progress writes are flushed on reconnect (with last-write-wins on the server).
- IndexedDB is the offline replica; backend remains source of truth.

### 9.6 Reader UX

Reader settings:

- font size, line height, content width, paragraph spacing
- theme: light, sepia, dark
- entity highlights: off / subtle / always
- recap card on section entry: on / off

Mobile-first layout:

```text
Top:    book title / toolbar
Middle: paginated reading area (CSS columns, swipe)
Bottom: progress + quick actions (memory, bookmark, settings)

Section entry: dismissible recap card overlays middle
Memory panel: bottom sheet on mobile, side panel on wider screens
                                                                       
                                                                       
Tabs in memory panel:                                                  
  Previously · Summary · Timeline · Graph · Story Guide · Assistant
```

Selection popover:

- Long-press on mobile, mouseup on desktop
- Chips: Who/What is this? · Ask…
- Auto-hint renders inline (cached entity card OR 1B-model gloss)
- "Tell me more" escalates to full Q&A panel

### 9.7 PWA / Capacitor

PWA-first:

- Installable via Chrome's "Install app"
- Service worker caches app shell + book sections
- IndexedDB unlimited on Android Chrome
- Import EPUB via standard `<input type="file" accept=".epub">`

Capacitor wrap-up in Phase 7:

- Android APK with "Share to / Open with Storymark" intent
- File picker via Capacitor Filesystem
- Safe-area handling
- Capacitor Secure Storage for bearer token
- iOS later if needed

---

## 10. Backend Plan

### 10.1 .NET Project Structure

```text
Storymark.Api/
  Program.cs

  Endpoints/
    Books/
    Progress/
    Bookmarks/
    Assistant/
    StoryGuide/
    Timeline/
    Graph/
    Selection/
    Admin/
      Settings/
      Prompts/
      LlmCalls/
      Books/        # ops
    Health/

  Domain/
    Books/
    Sections/
    Entities/
    Events/
    Relationships/
    Summaries/
    Progress/
    Bookmarks/
    Settings/
    Processing/
    LlmCalls/

  Infrastructure/
    Database/
      Migrations/
    Vector/                # pgvector helpers
    Llm/
      IChatModelClient.cs
      OllamaClient.cs
      OpenAiCompatibleClient.cs
      EmbeddingService.cs
      LlmCallRecorder.cs   # writes llm_calls
    Import/
      Epub/
      Pdf/
      Text/
    FileStorage/
    Search/                # postgres FTS
    Auth/                  # bearer token

  Services/
    BookImportService.cs
    EpubParsingService.cs
    ContentSanitizationService.cs
    SectionChunkingService.cs
    EmbeddingService.cs
    AssistantService.cs
    SpoilerSafeRetrievalService.cs
    EntityDetectionService.cs
    EntitySummaryService.cs
    SummaryService.cs
    EventExtractionService.cs
    RelationshipExtractionService.cs
    BookMemoryService.cs
    TimelineQueryService.cs
    RelationshipGraphService.cs
    SelectionHintService.cs
    SettingsService.cs       # DB-backed config + hot reload
    PromptRegistry.cs        # prompts pulled from settings table

  Jobs/                      # Hangfire-scheduled
    ProcessBookJob.cs
    SectionExtractionJob.cs  # summary + events + relationships in one pass
    EmbeddingJob.cs
    EntitySummaryJob.cs
    ReindexBookJob.cs        # used on embedding-model swap

  Prompts/                   # default prompt text seeded into settings
    SpoilerSafeQuestionPrompt.cs
    SectionExtractionPrompt.cs
    EntitySummaryPrompt.cs
    SelectionHintPrompt.cs
    BookMemoryPrompt.cs
```

### 10.2 FastEndpoints

Endpoint groups:

```text
/api/books
/api/books/{bookId}
/api/books/{bookId}/sections
/api/books/{bookId}/progress
/api/books/{bookId}/bookmarks
/api/books/{bookId}/ask
/api/books/{bookId}/memory
/api/books/{bookId}/entities
/api/books/{bookId}/events
/api/books/{bookId}/relationships
/api/books/{bookId}/selection/hint

/api/admin/settings
/api/admin/prompts
/api/admin/llm-calls
/api/admin/books/{bookId}/reindex
/api/admin/books/{bookId}/regenerate-summaries
```

All `/api/admin/*` endpoints require the admin bearer token.

---

## 11. API Endpoints

### 11.1 Upload Book

```http
POST /api/books
Content-Type: multipart/form-data
```

Request: `file: .epub`

Response:

```json
{
  "bookId": "uuid",
  "title": "Book Title",
  "author": "Author",
  "format": "epub",
  "status": "QueuedForProcessing"
}
```

Backend actions: validate → SHA-256 → store → dedupe → create/reuse book → enqueue `ProcessBookJob` → return.

### 11.2 List Books

```http
GET /api/books
```

Response:

```json
[
  {
    "bookId": "uuid",
    "title": "Book Title",
    "author": "Author",
    "format": "epub",
    "coverUrl": "/api/books/{id}/cover",
    "processingStatus": "Indexed",
    "lastReadSection": 3,
    "lastOpenedAt": "2026-05-31T12:00:00Z"
  }
]
```

### 11.3 Get Sections / Section

```http
GET /api/books/{bookId}/sections
GET /api/books/{bookId}/sections/{sectionIndex}
```

Returns paragraph-level offsets, sanitized HTML, and visible entity annotations.

### 11.4 Update Progress

```http
POST /api/books/{bookId}/progress
```

Request:

```json
{
  "sectionIndex": 3,
  "paragraphIndex": 12,
  "locationStart": 1200,
  "locationEnd": 1840,
  "clientTimestamp": "2026-05-31T12:00:01Z"
}
```

Backend stores per-device row; latest timestamp wins on read.

### 11.5 Bookmarks

```http
GET    /api/books/{bookId}/bookmarks
POST   /api/books/{bookId}/bookmarks      { sectionIndex, locationStart, label }
DELETE /api/books/{bookId}/bookmarks/{id}
```

### 11.6 Ask Question

```http
POST /api/books/{bookId}/ask
```

Request:

```json
{
  "question": "What did character X do that he got put in jail?",
  "currentSection": 7,
  "currentLocation": 1840
}
```

Response:

```json
{
  "answer": "Character X was jailed because...",
  "confidence": "medium",
  "sources": [
    { "sectionIndex": 6, "locationStart": 1200, "locationEnd": 1600, "snippet": "..." }
  ],
  "usedFutureContent": false
}
```

### 11.7 Selection Hint

```http
POST /api/books/{bookId}/selection/hint
```

Request:

```json
{
  "selectedText": "Bene Gesserit",
  "sectionIndex": 7,
  "locationStart": 1820,
  "locationEnd": 1834,
  "currentLocation": 1840
}
```

Response (entity hit):

```json
{
  "kind": "entity",
  "entityId": "uuid",
  "card": { "...EntitySummary..." }
}
```

Response (fast hint):

```json
{
  "kind": "hint",
  "hint": "A mysterious order with political and mystical influence; introduced briefly in Chapter 1.",
  "sources": [ { "sectionIndex": 1, "locationStart": 200, "locationEnd": 380 } ]
}
```

Routes the cheap path: entity lookup is a single SQL query, no LLM call. The fallback uses the fast model.

### 11.8 Memory Surfaces

```http
GET /api/books/{bookId}/memory?upToSection=7&upToLocation=1840
GET /api/books/{bookId}/entities?upToSection=7&upToLocation=1840
GET /api/books/{bookId}/entities/{entityId}/summary?section=7&location=1840
GET /api/books/{bookId}/events?upToSection=7&upToLocation=1840
GET /api/books/{bookId}/relationships?upToSection=7&upToLocation=1840
```

Each returns only content whose source position ≤ current position.

### 11.9 Admin

```http
GET    /api/admin/settings
PATCH  /api/admin/settings                  # { key, value, type }
GET    /api/admin/prompts
PATCH  /api/admin/prompts/{key}             # { text }
GET    /api/admin/llm-calls?task=&limit=
POST   /api/admin/books/{bookId}/reindex
POST   /api/admin/books/{bookId}/regenerate-summaries
```

---

## 12. Database Schema

### 12.1 Books

```sql
create table books (
  id uuid primary key,
  content_hash text not null unique,
  title text not null,
  author text,
  language text,
  format text not null,
  file_path text not null,
  cover_path text,
  processing_status text not null,
  created_at timestamptz not null,
  updated_at timestamptz not null
);
```

### 12.2 Sections

```sql
create table book_sections (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_index int not null,
  title text,
  source_href text,
  html_path text,
  text_path text,
  text_hash text,
  word_count int not null default 0,
  created_at timestamptz not null,
  unique(book_id, section_index)
);
```

### 12.3 Paragraphs

```sql
create table book_paragraphs (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_id uuid not null references book_sections(id) on delete cascade,
  section_index int not null,
  paragraph_index int not null,
  location_start int not null,
  location_end int not null,
  html text,
  plain_text text not null,
  unique(book_id, section_index, paragraph_index)
);
```

### 12.4 Chunks (with pgvector)

```sql
create extension if not exists vector;

create table book_chunks (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_id uuid not null references book_sections(id) on delete cascade,
  section_index int not null,
  chunk_index int not null,
  location_start int not null,
  location_end int not null,
  text text not null,
  token_estimate int not null,
  embedding vector,                            -- dimensionless: model is swappable (ADR-0014)
  embedding_model_id text,                     -- e.g. "ollama:nomic-embed-text"
  embedding_model_version text,                -- model build/tag
  created_at timestamptz not null,
  unique(book_id, section_index, chunk_index)
);

-- No HNSW index (ADR-0014): a dimensionless column can't carry one, and an exact
-- <=> scan is sub-ms at single-operator scale. Reintroduce per-dimension if it grows.

create index book_chunks_fts on book_chunks
  using gin (to_tsvector('english', text));

create index book_chunks_pos on book_chunks(book_id, section_index, location_start);
```

Search only considers vectors whose `embedding_model_id` matches the currently-selected model in settings — so vectors of different dimensions (different models) never meet in a `<=>` comparison.

### 12.5 Progress

```sql
create table reading_progress (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  device_id text not null,
  section_index int not null,
  paragraph_index int,
  location_start int not null,
  location_end int not null,
  client_timestamp timestamptz not null,
  updated_at timestamptz not null,
  unique(book_id, device_id)
);
```

The canonical position is `MAX(client_timestamp)` across all devices for that book.

### 12.6 Bookmarks

```sql
create table book_bookmarks (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_index int not null,
  paragraph_index int,
  location_start int not null,
  label text,
  created_at timestamptz not null
);
```

### 12.7 Section Summaries

```sql
create table section_summaries (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_index int not null,
  summary text not null,
  important_events jsonb not null default '[]',
  entities_seen jsonb not null default '[]',
  open_threads jsonb not null default '[]',
  generated_at timestamptz not null,
  unique(book_id, section_index)
);
```

### 12.8 Story Guide Entities

```sql
create table book_entities (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  canonical_name text not null,
  entity_type text not null,
  aliases jsonb not null default '[]',
  first_seen_section int,
  first_seen_location int,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  unique(book_id, canonical_name)
);
```

Entity types: `Person, Place, Organization, Object, Event, Concept, Creature, Title, Unknown`.

### 12.9 Entity Mentions

```sql
create table book_entity_mentions (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  entity_id uuid not null references book_entities(id) on delete cascade,
  section_index int not null,
  location_start int not null,
  location_end int not null,
  mention_text text not null,
  surrounding_text text,
  confidence numeric not null default 1.0
);
```

### 12.10 Entity Summaries (cached, position-scoped)

```sql
create table book_entity_summaries (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  entity_id uuid not null references book_entities(id) on delete cascade,
  up_to_section int not null,
  up_to_location int not null,
  short_summary text not null,
  known_facts jsonb not null default '[]',
  relationships jsonb not null default '[]',
  open_questions jsonb not null default '[]',
  generated_at timestamptz not null,
  unique(entity_id, up_to_section, up_to_location)
);
```

### 12.11 Book Memory Snapshots

```sql
create table book_memory_snapshots (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  up_to_section int not null,
  up_to_location int not null,
  summary_so_far text not null,
  timeline jsonb not null default '[]',
  open_threads jsonb not null default '[]',
  generated_at timestamptz not null,
  unique(book_id, up_to_section, up_to_location)
);
```

A snapshot is generated at every section boundary; this powers the section-entry recap card with zero LLM latency at read time.

### 12.12 Events

```sql
create table book_events (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  section_index int not null,
  location_start int not null,
  location_end int not null,
  event_type text,                       -- e.g. arrival, death, betrayal, reveal
  description text not null,
  involved_entity_ids jsonb not null default '[]',
  created_at timestamptz not null
);

create index book_events_pos on book_events(book_id, section_index, location_start);
```

### 12.13 Relationships

```sql
create table book_relationships (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  from_entity_id uuid not null references book_entities(id) on delete cascade,
  to_entity_id uuid not null references book_entities(id) on delete cascade,
  relationship_type text not null,       -- parent, sibling, friend, enemy, lover, ally...
  polarity text,                         -- positive, negative, neutral, complex
  evidence text,                         -- short quote/snippet
  first_seen_section int not null,
  first_seen_location int not null,
  last_seen_section int,
  last_seen_location int,
  created_at timestamptz not null
);

create index book_relationships_pos on book_relationships(book_id, first_seen_section);
```

### 12.14 Q&A History

```sql
create table question_answers (
  id uuid primary key,
  book_id uuid not null references books(id) on delete cascade,
  question text not null,
  answer text not null,
  current_section int not null,
  current_location int not null,
  sources jsonb not null default '[]',
  created_at timestamptz not null
);
```

### 12.15 Settings (DB-backed configuration)

```sql
create table settings (
  key text primary key,
  value jsonb not null,
  value_type text not null,             -- string, int, bool, json, secret
  is_secret bool not null default false,
  updated_at timestamptz not null,
  updated_by text
);
```

Secret values stored encrypted (AES-GCM with a key from a single env var). On startup the backend seeds defaults from `appsettings.json` for any missing keys. Changes broadcast via `LISTEN/NOTIFY 'settings_changed'`; `SettingsService` invalidates its in-memory cache.

Key examples:

```text
llm.chat.taskMap                # { "qa": "ollama:llama3.2:3b", "extract": "ollama:llama3.2:1b", ... }
llm.embedding.modelId           # "ollama:nomic-embed-text"
llm.providers                   # array of provider configs
prompts.qa                      # text
prompts.sectionExtraction       # text
prompts.entitySummary           # text
prompts.selectionHint           # text
prompts.bookMemory              # text
upload.maxBytes                 # 209715200
auth.bearerToken                # secret
features.recapCardOnSectionEntry# bool
```

### 12.16 LLM Call Log

```sql
create table llm_calls (
  id uuid primary key,
  task text not null,                   -- qa | extract | summarize | entity_summary | selection_hint | memory
  provider text not null,               -- ollama | openai_compatible | ...
  model text not null,
  prompt text not null,
  response text,
  tokens_in int,
  tokens_out int,
  latency_ms int,
  cost_estimate numeric,
  book_id uuid,
  section_index int,
  used_future_content bool not null default false,
  created_at timestamptz not null
);

create index llm_calls_task_time on llm_calls(task, created_at desc);
```

Powers the admin call log and cost meter. Rolling 30-day retention.

---

## 13. Vector Search (pgvector)

A unified hybrid retrieval query becomes a single SQL statement:

```sql
with allowed as (
  select * from book_chunks
  where book_id = :bookId
    and embedding_model_id = :embeddingModelId
    and (section_index < :currentSection
         or (section_index = :currentSection and location_end <= :currentLocation))
),
semantic as (
  select id, section_index, location_start, location_end, text,
         1 - (embedding <=> :queryEmbedding) as score
  from allowed
  order by embedding <=> :queryEmbedding
  limit 20
),
keyword as (
  select id, section_index, location_start, location_end, text,
         ts_rank_cd(to_tsvector('english', text), plainto_tsquery(:queryText)) as score
  from allowed
  where to_tsvector('english', text) @@ plainto_tsquery(:queryText)
  limit 20
)
select * from semantic
union all
select * from keyword;
```

The .NET layer reranks the merged set, adds entity summaries and section summaries as additional evidence, then constructs the prompt.

---

## 14. File Storage

Use a backend volume mounted at `/data`.

```text
/data/
  books/
    {contentHash}/
      original.epub
      cover.jpg
      metadata.json
      sections/
        0001.xhtml
        0001.txt
        0002.xhtml
        0002.txt
      cache/
        summary-0001.json
        summary-0002.json
        entity-index.json
```

Rules:

- Never trust original filenames.
- Store originals by content hash.
- Store extracted section text separately.
- Sanitize HTML (AngleSharp) before serving it to the client.
- Keep original file for future reprocessing.
- Files are optional artifacts; PostgreSQL is the queryable source of truth.

---

## 15. Import and Processing Pipeline

### 15.1 Upload-Time Work

```text
1. Validate upload.
2. Calculate SHA-256.
3. Store original file.
4. Create or reuse book record.
5. Enqueue ProcessBookJob.
6. Return response.
```

### 15.2 Background Processing Job (Hangfire)

```text
ProcessBookJob
  1. Choose import handler by file type.
  2. Use EpubImportHandler (MVP).
  3. Extract metadata.
  4. Extract cover image.
  5. Extract reading order / sections.
  6. Extract section XHTML.
  7. Convert XHTML to sanitized renderable HTML (AngleSharp).
  8. Convert XHTML to clean plain text.
  9. Store section files.
  10. Create section records.
  11. Create paragraph records with offsets.
  12. Split text into chunks.
  13. Store chunk records.
  14. Detect entity candidates.
  15. Store entity mentions.
  16. Enqueue EmbeddingJob (per chunk batch, priority normal).
  17. Enqueue SectionExtractionJob (per section, priority normal).
  18. Mark book as Indexed when all child jobs complete.

SectionExtractionJob (per section)
  - Single LLM call: summary + events + relationship deltas + entitiesIntroduced.
  - Persist into section_summaries, book_events, book_relationships, book_entities.
  - Then enqueue BookMemorySnapshotJob for this section boundary.

BookMemorySnapshotJob
  - Compose previous snapshot + new section summary.
  - Single LLM call → updated summarySoFar/timeline/openThreads.
  - Persist book_memory_snapshots row for this position.
  - This snapshot powers the section-entry recap card.

EntitySummaryJob
  - Lazy, on-demand (priority high) or generated at coarse checkpoints.
```

### 15.3 Chunking Strategy

Avoid arbitrary fixed-size chunks only. Fiction benefits from paragraph-aware chunking.

```text
1. Split section into paragraphs.
2. Merge paragraphs until target size is reached.
3. Target chunk size: 500–900 words.
4. Add overlap: 100–150 words.
5. Preserve section index and character offsets.
```

Each chunk knows: section index, chunk index, start/end offset, paragraph range, token estimate.

### 15.4 Processing Statuses

```text
Uploaded
Parsing
Parsed
Chunking
DetectingEntities
ExtractingPerSection      # summary + events + relationships
Embedding
BuildingMemorySnapshots
Indexed
Failed
```

Exposed to the frontend.

### 15.5 Embedding Model Lifecycle

- `book_chunks.embedding_model_id` is set when the row is written.
- Search filters by the currently-selected `llm.embedding.modelId`.
- Changing the embedding model in admin (ADR-0014):
  - shows a confirmation dialog with an impact preview ("3 books, 16 chunks will be reindexed, ~8s")
  - persists the new model id, then enqueues a per-book reindex (the existing `EmbeddingJob`, since "embed chunks tagged with a different model id" is exactly a reindex — no separate `ReindexBookJob`)
  - each chunk is re-embedded and overwritten in place; a chunk not yet reindexed keeps the old model id and is filtered out of search, so a mid-reindex book returns fewer (never wrong-model) results until it completes

---

## 16. Story Guide Entity System

### 16.1 Preindexing

Storymark can scan the whole book to detect entity mentions, but it must only reveal or summarise entities up to the reader's current position.

```text
Backend knows:
  "Valjean" appears in sections 1, 2, 4, 9, 18, 42.

Reader is at section 6:
  UI and summaries can only use sections 1, 2, 4.
```

Never show: future mention counts, future relationships, future status, future summary facts, future event references.

### 16.2 Entity Detection

Layered:

```text
1. Rule-based candidate extraction.
2. Frequency and casing heuristics.
3. LLM cleanup/classification during SectionExtractionJob.
4. Alias merging.
5. Store mentions.
```

Candidate heuristics: capitalised words, multi-word capitalised phrases, titles+names (Mr., Mrs., Dr., Lord, Lady, Captain), repeated named phrases, "called/named/known as" patterns, italicised EPUB terms.

### 16.3 Entity Summary Generation (lazy)

When the user taps an entity:

```text
1. Check cached entity summary for current section/location.
2. If fresh enough, return it.
3. Otherwise retrieve visible mentions only.
4. Retrieve visible summaries and chunks related to the entity.
5. Generate a short spoiler-safe summary (EntitySummaryJob, priority high).
6. Cache it.
7. Return it.
```

Cache at coarse checkpoints: end of section, every N paragraphs, or on explicit open.

### 16.4 Entity Highlight UX

Modes: `off`, `subtle`, `always`. Default `subtle`. The `Mentioned in this section` list in the Story Guide panel is always available regardless of highlight mode.

---

## 17. Spoiler-Safe Retrieval

This is the most important app rule.

For any assistant request, the backend MUST filter source material before sending anything to the LLM.

Allowed content:

```text
sectionIndex < currentSection
OR
sectionIndex = currentSection AND locationEnd <= currentLocation
```

Disallowed content:

```text
sectionIndex > currentSection
OR
sectionIndex = currentSection AND locationStart > currentLocation
```

Do not rely only on the prompt. The model must never receive future book content.

Every LLM call goes through `LlmCallRecorder`, which logs the prompt and a `used_future_content` flag derived from the retrieved sources. The admin call log lets you audit this.

---

## 18. Free-Text Q&A Pipeline

For a question such as: "What did character X do that he got put in jail?"

```text
1. Receive question, book ID, current section and location.
2. Extract likely entities from the question.
3. Generate retrieval queries: original + entity name + synonyms (jail, prison, arrested, ...).
4. Run the unified hybrid SQL query (§13).
5. Pull matching entity summaries + section summaries + memory snapshots.
6. Apply spoiler-safe filter.
7. Rerank evidence.
8. Build prompt from top evidence only.
9. Call current Q&A model via IChatModelClient.
10. Record llm_call.
11. Return answer with source snippets and `usedFutureContent: false`.
```

### 18.1 Hybrid Retrieval Sources

```text
pgvector:    semantic search over chunks (filtered by embedding_model_id)
Postgres FTS: keyword search over chunks, summaries, entities
Story Guide: entity facts, aliases, status, relationships
Section summaries: compressed plot events
Memory snapshots: summary-so-far, timeline, unresolved threads
```

### 18.2 Query Expansion

Start with a small handcrafted dictionary (also stored in `settings`):

```text
jail:         jail, prison, imprisoned, arrested, accused, sentenced, constable, guard, locked up, dungeon
death:        died, killed, murdered, slain, executed, passed away
relationship: father, mother, brother, sister, friend, enemy, lover, married, betrayed
travel:       left, arrived, journeyed, escaped, fled, returned
```

---

## 19. Prompts

All prompt text is stored in the `settings` table (`prompts.*` keys) and editable from the admin panel. The values below are the seeded defaults.

### 19.1 Spoiler-Safe Q&A Prompt

```text
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
- Include section references when useful.

Question:
{question}

Context:
{context}
```

### 19.2 Section Extraction Prompt (combined)

```text
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
```

### 19.3 Entity Summary Prompt

```text
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
```

### 19.4 Selection Hint Prompt (fast model)

```text
The reader has selected: "{selectedText}"

They are at section {currentSection}, location {currentLocation}.

Using only the supplied context (which is filtered to content the reader has already seen),
write 1-2 sentences that help the reader understand what they just selected.

If the context is empty or unrelated, reply with exactly: "Nothing on this yet."

Do not foreshadow. Do not reveal future events.

Context:
{context}
```

### 19.5 Book Memory Prompt

```text
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
```

---

## 20. LLM Abstraction

### 20.1 Interface

```csharp
public interface IChatModelClient
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
    string ProviderName { get; }
}
```

Implementations: `OllamaClient`, `OpenAiCompatibleClient`.

Routing is driven by `llm.chat.taskMap` and `llm.embedding.modelId` in settings. Each task ("qa", "extract", "entity_summary", "selection_hint", "memory") resolves to a `provider:model` string at call time.

### 20.2 LLM Call Recording

Every call passes through `LlmCallRecorder` which writes to `llm_calls` with prompt, response, latency, tokens, and a derived `used_future_content` flag from the calling site. The admin panel reads from here.

### 20.3 Cost Meter

For hosted providers, `LlmCallRecorder` multiplies tokens by configured per-million rates (from `settings`) to produce a cost estimate. The admin panel aggregates by task and by book.

### 20.4 Default Local Models

```text
llm.chat.taskMap.qa              = ollama:llama3.2:3b
llm.chat.taskMap.extract         = ollama:llama3.2:1b
llm.chat.taskMap.entity_summary  = ollama:llama3.2:3b
llm.chat.taskMap.selection_hint  = ollama:llama3.2:1b
llm.chat.taskMap.memory          = ollama:llama3.2:3b
llm.embedding.modelId            = ollama:nomic-embed-text
```

Given the i5-9500, default to 3B or smaller for chat tasks.

---

## 21. Docker / Portainer Deployment

### 21.1 Compose Stack

```yaml
services:
  storymark-api:
    build:
      context: ./backend/Storymark.Api
      dockerfile: Dockerfile
    container_name: storymark-api
    restart: unless-stopped
    ports:
      - "8088:8080"
    environment:
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__Postgres: Host=postgres;Port=5432;Database=storymark;Username=storymark;Password=storymarkpass
      Ollama__Url: http://ollama:11434
      Storage__RootPath: /data
      Auth__BearerTokenSeed: change-me-on-first-run
      Settings__EncryptionKey: 32-byte-base64-here
    volumes:
      - storymark_files:/data
    depends_on:
      - postgres
      - ollama

  postgres:
    image: pgvector/pgvector:pg16
    container_name: storymark-postgres
    restart: unless-stopped
    environment:
      POSTGRES_DB: storymark
      POSTGRES_USER: storymark
      POSTGRES_PASSWORD: storymarkpass
    volumes:
      - storymark_postgres:/var/lib/postgresql/data

  ollama:
    image: ollama/ollama:latest
    container_name: storymark-ollama
    restart: unless-stopped
    volumes:
      - storymark_ollama:/root/.ollama
    # No public port once api-only access is desired

volumes:
  storymark_files:
  storymark_postgres:
  storymark_ollama:
```

After first deploy, pull models inside the Ollama container:

```bash
docker exec -it storymark-ollama ollama pull llama3.2:3b
docker exec -it storymark-ollama ollama pull llama3.2:1b
docker exec -it storymark-ollama ollama pull nomic-embed-text
```

Hangfire runs in-process inside `storymark-api`; its storage is the same Postgres database. Dashboard mounted at `/hangfire`, gated by the admin bearer token.

---

## 22. Upload Security

EPUB files are ZIP files, so treat uploads carefully.

```text
- Allow only supported extensions.
- For MVP, only allow .epub.
- Enforce max upload size (default 200 MB).
- Verify file signatures.
- Store outside web root.
- Generate storage name from SHA-256 hash.
- Never use original filename as path.
- Limit decompressed size to reduce zip-bomb risk.
- Limit number of extracted files.
- Sanitize XHTML (AngleSharp) before serving it to frontend.
- Strip scripts, event handlers, external resources, unsafe links.
```

Suggested limits:

```text
Max upload size:       200 MB
Max decompressed size: 1 GB
Max files in EPUB:     10,000
Allowed content types: application/epub+zip, application/zip
```

Do not trust MIME type alone.

---

## 23. Testing Strategy — Deferred

Automated tests are intentionally deferred until after Phase 6 (see [`adr/0012-tests-deferred.md`](adr/0012-tests-deferred.md)). The plan is to ship vertical slices manually-verifiable from the demo path described in each issue's Acceptance Criteria, then layer a test pyramid on top once the surface is stable.

When tests do land, the intended shape is:

- Frontend: composables, API clients, Dexie layer, key components.
- Backend: services with deterministic behaviour (chunking, spoiler-safe filtering, structured-JSON parsing, settings/secret encryption, LLM call recording).
- Integration: Testcontainers (Postgres + pgvector), fake `IChatModelClient`, golden EPUB fixtures (no copyrighted material).
- A **spoiler-safety regression pack** that asserts no future content reaches any AI surface.

Until then, every PR must include manual verification steps in the description that reproduce the slice's Acceptance Criteria.

---

## 24. Development Phases

### Phase 0 — Project Setup

```text
- pnpm workspace
- Vue 3 + Vite + TypeScript app, Tailwind, PrimeVue, oxlint, oxfmt
- .NET backend project, FastEndpoints, EF Core, Hangfire
- Dexie + service worker scaffold
- Docker Compose: api, postgres (pgvector), ollama
```

Acceptance: frontend runs, backend runs, compose starts all services, health endpoint OK.

### Phase 0.5 — Admin Shell + Settings Table

```text
- settings + llm_calls tables and migrations
- SettingsService with LISTEN/NOTIFY hot reload
- PromptRegistry pulling from settings
- IChatModelClient + OllamaClient + OpenAiCompatibleClient
- LlmCallRecorder
- /api/admin/* endpoints (bearer-auth)
- Admin shell with Settings editor + Prompts editor + LLM call log + Cost meter
```

Acceptance: changing a setting in admin updates runtime behaviour without restart; every LLM call lands in `llm_calls`.

### Phase 1 — Book Upload and Library

```text
- Upload endpoint, EPUB validation
- File storage by SHA-256, duplicate detection
- books table, processing status
- Library + upload screens
```

Acceptance: upload an EPUB; backend stores it at `/data/books/{hash}/original.epub`; duplicate upload reuses record; library shows the book.

### Phase 2 — EPUB Parsing and Paginated Reading

```text
- EPUB metadata + cover extraction
- Section extraction
- Sanitized HTML with offsets
- Paginated CSS-columns reader
- Section navigation, typography controls, themes
- Progress tracking (backend-authoritative LWW)
- Whole-book Dexie download on first open
- Offline reading from Dexie when API unreachable
- Bookmarks CRUD + panel
```

Acceptance: read the book offline after first open; progress saves and resumes; bookmarks work; whole book in IndexedDB.

### Phase 3 — Chunking, Indexing, and Hybrid Search

```text
- Paragraph-aware chunking
- book_chunks with embedding column
- EmbeddingJob via Hangfire
- HNSW + FTS indexes
- Unified hybrid retrieval query
- Spoiler-safe filter helper
- Background processing status surfaced in UI
```

Acceptance: uploaded book is chunked and embedded; status moves to Indexed; embedding-model swap triggers reindex with confirmation.

### Phase 4 — Spoiler-Safe Q&A and Selection Hint

```text
- /api/books/{id}/ask with hybrid retrieval, sources, used_future_content
- AssistantPanel + answer/sources UI
- /api/books/{id}/selection/hint
- SelectionPopover with tiered flow (entity hit → 1B hint → escalate)
- LLM call log shows each call with prompt, response, latency
```

Acceptance: ask a free-text question from the reader, answer cites only reader-visible content, selection on a known entity is instant, selection on novel text returns a fast hint.

### Phase 5 — Per-Section Extraction, Memory, Recap Card

```text
- SectionExtractionJob: summary + events + relationship deltas + entitiesIntroduced (one LLM call per section)
- BookMemorySnapshotJob: rolling memory updated at each section boundary
- SectionRecapCard auto-appears on entering a new section (precomputed snapshot)
- PreviouslyCard on book reopen >24h
```

Acceptance: section entry shows a recap with zero LLM latency at read time; recap reflects only reader-visible content.

### Phase 6 — Story Guide, Timeline, Relationship Graph

```text
- book_entities, book_entity_mentions, book_entity_summaries
- Lazy EntitySummaryJob
- Entity popup + StoryGuidePanel
- /api/books/{id}/events + TimelineChart (ECharts)
- /api/books/{id}/relationships + RelationshipGraphChart (ECharts)
- Memory panel tabs: Previously · Summary · Timeline · Graph · Story Guide · Assistant
```

Acceptance: timeline and graph render only visible-position content; tapping a node or event navigates to source location.

### Phase 7 — Mobile Polish + Capacitor + Highlights/Notes

```text
- Capacitor Android build (file picker, safe area, secure-storage token)
- "Open with Storymark" intent for EPUB
- Highlights with colour picker
- Notes anchored to ranges
- Bookmark / highlight / note sync follows progress sync rules
```

Acceptance: Android APK installs; share-to-Storymark works; highlight a passage, add a note, see it on the laptop PWA.

### Phase 8 — Format Abstraction and Future Imports

```text
- IBookImportHandler abstraction in place from Phase 1, but exercise it
- TXT import prototype
- PDF research spike
```

Acceptance: EPUB still works, second format handler registers and parses a TXT file end-to-end.

### Phase 9 — Hardening and Optimisation

```text
- Upload security hardening pass
- HTML sanitization improvements
- Retry failed processing jobs
- Better status reporting
- Prompt tuning sessions using LLM call log
- Query expansion expansion
- Performance profiling on i5-9500
- Entity curation UI (rename, merge, delete) in admin
```

Acceptance: large books do not block the API, failed jobs retry, Q&A latency acceptable for local use, retrieval quality good across character/entity/plot questions.

---

## 25. Key Implementation Details

### 25.1 Character/Text Offsets

When converting section HTML to plain text, preserve offsets.

```json
{
  "paragraphIndex": 4,
  "startOffset": 1200,
  "endOffset": 1540,
  "html": "<p>...</p>",
  "plainText": "..."
}
```

Frontend reports current reading position using `(sectionIndex, paragraphIndex, charOffsetInParagraph)`. These map cleanly onto the spoiler-safe filter.

### 25.2 Source Links

Each answer/event source includes:

```json
{
  "sectionIndex": 6,
  "sectionTitle": "Chapter Six",
  "locationStart": 1200,
  "locationEnd": 1600,
  "snippet": "..."
}
```

Frontend offers "jump to source".

### 25.3 Confidence

```text
high:      multiple relevant sources agree
medium:    one strong source
low:       weak or indirect evidence
not_found: answer unavailable from reader-visible context
```

The model can express confidence; the backend also infers it from retrieval score and source count.

### 25.4 No-Spoiler Contract

Every assistant, entity summary, and selection-hint response carries `usedFutureContent: false`. True only on bugs; logged in `llm_calls.used_future_content`.

### 25.5 Offline Sync Rules

- Reading progress: write to Dexie immediately; POST to backend with `clientTimestamp`; on failure, queue in `pendingProgress` table; flush on reconnect; server resolves by `MAX(client_timestamp)`.
- Bookmarks: same pattern.
- AI artifacts: read-only on client; backend pushes by version. No client-side generation.

### 25.6 Bearer Token

Single secret, stored in `settings.auth.bearerToken`. Client persists it in localStorage (PWA) or Capacitor Secure Storage (mobile). Sent as `Authorization: Bearer …` on every request. Rotated from the admin panel.

---

## 26. Performance Notes for i5-9500 / 24 GB RAM

```text
- Prefer 1B–3B models for local tasks.
- Generate embeddings in background.
- Do not summarise the whole book during upload.
- Cache summaries, memory snapshots, entity cards.
- Generate entity summaries lazily.
- Limit Q&A context size.
- Use top 5–12 evidence chunks, not entire sections.
- Queue work instead of doing it in request handlers.
- Section extraction does ONE LLM call per section (summary + events + relationships combined).
- Selection hint uses the 1B model.
```

Expected:

```text
EPUB parsing:                 fast
Chunking:                     fast
Entity candidate extraction:  moderate, background
Embeddings:                   moderate, background
Section extraction (3B):      slow per section, background
3B Q&A:                       usable, not instant
1B selection hint:            ~1-3s
Image generation:             out of scope
```

---

## 27. Initial Backlog

### Backend

- [ ] Create ASP.NET Core project
- [ ] Add FastEndpoints
- [ ] Add EF Core + PostgreSQL + pgvector
- [ ] Add Hangfire (Postgres storage)
- [ ] Add Dockerfile
- [ ] Add health endpoint
- [ ] **Add settings table + SettingsService + LISTEN/NOTIFY**
- [ ] **Add llm_calls table + LlmCallRecorder**
- [ ] **Add IChatModelClient + OllamaClient + OpenAiCompatibleClient**
- [ ] **Add PromptRegistry seeded defaults**
- [ ] **Add admin endpoints (bearer-token guarded)**
- [ ] Add book upload endpoint
- [ ] Add file storage service
- [ ] Add SHA-256 hashing
- [ ] Add EPUB validation
- [ ] Add IBookImportHandler + EpubImportHandler
- [ ] Add VersOne.Epub parsing service
- [ ] Add AngleSharp HTML sanitization
- [ ] Add section extraction
- [ ] Add paragraph-aware chunking
- [ ] Add entity candidate extraction
- [ ] Add entity mention storage
- [ ] Add EmbeddingJob
- [ ] Add SectionExtractionJob (summary + events + relationships)
- [ ] Add BookMemorySnapshotJob
- [ ] Add ReindexBookJob (embedding swap)
- [ ] Add SpoilerSafeRetrievalService
- [ ] Add unified hybrid retrieval query
- [ ] Add ask endpoint
- [ ] Add selection hint endpoint
- [ ] Add timeline endpoint
- [ ] Add relationships endpoint
- [ ] Add memory endpoint
- [ ] Add entities endpoints
- [ ] Add bookmarks endpoints
- [ ] Add progress endpoints (LWW)

### Frontend

- [ ] Create Vite Vue app
- [ ] Configure pnpm workspaces + catalog, Tailwind, PrimeVue, oxlint, oxfmt
- [ ] Configure service worker (vite-plugin-pwa)
- [ ] Set up Dexie schema + offline sync layer
- [ ] Build API client layer with bearer token
- [ ] Build useBooks, useUpload, useReader, useReadingProgress
- [ ] Build useAssistant, useStoryGuide
- [ ] Build useTimeline, useRelationshipGraph, useSelectionHint
- [ ] Build useBookmarks, useOfflineSync, useSettings, useAdmin
- [ ] Build library + upload screens
- [ ] Build paginated reader screen (CSS columns)
- [ ] Build memory panel with tabs (Previously / Summary / Timeline / Graph / Story Guide / Assistant)
- [ ] Build section recap card + previously card
- [ ] Build selection popover (tiered)
- [ ] Build entity popup + Story Guide panel
- [ ] Build timeline chart (ECharts)
- [ ] Build relationship graph chart (ECharts)
- [ ] Build bookmarks panel
- [ ] Build settings screen
- [ ] Build admin shell (PrimeVue): settings editor, prompts editor, LLM call log, cost meter, per-book ops

### DevOps

- [ ] Create Docker Compose stack (api, pgvector postgres, ollama)
- [ ] Configure Portainer deployment
- [ ] Add persistent volumes
- [ ] Pull Ollama models
- [ ] Configure backend env vars
- [ ] Add backup strategy for PostgreSQL and /data
- [ ] Tailscale on phone + homelab
- [ ] Restrict exposed ports
- [ ] Reverse proxy + TLS later if needed (Caddy)

---

## 28. Recommended First Sprint

Smallest useful vertical slice:

```text
1. Admin shell + settings table running (Phase 0.5 essentials).
2. Upload EPUB.
3. Backend extracts sections.
4. Frontend lists books.
5. Frontend reads section HTML in paginated mode.
6. Whole-book Dexie download.
7. Progress saves (LWW) and resumes offline.
8. Bookmarks work.
```

Do not start with AI features. First prove the reader, offline cache, and admin foundation work.

Second sprint:

```text
1. Chunk sections.
2. Generate embeddings (Hangfire job).
3. Unified hybrid retrieval query.
4. Ask a basic question with sources.
5. Selection hint (entity hit + 1B fallback).
6. Enforce spoiler filtering, prove it via llm_calls log.
```

Third sprint:

```text
1. SectionExtractionJob (summary + events + relationships).
2. Memory snapshots.
3. Section recap card.
4. Entity popups + Story Guide panel.
5. Timeline chart.
6. Relationship graph chart.
```

---

## 29. Biggest Risks

### Risk: EPUB parsing edge cases

Mitigation: start with DRM-free EPUBs; store original for reprocessing; keep parser isolated behind `IBookImportHandler`; add test EPUBs.

### Risk: Poor Q&A quality

Mitigation: hybrid retrieval; summaries + entity cards as additional evidence; tune chunking; show sources; tune prompts via admin editor; avoid relying on model memory.

### Risk: Spoilers

Mitigation: filter before prompt construction; tests for spoiler-safe retrieval; log source chunks via `llm_calls`; never send future sections to the LLM; never use future mentions in entity summaries; admin call log lets you audit every call.

### Risk: Slow CPU inference

Mitigation: 1B–3B models; cache everything; precompute memory snapshots at section boundaries; selection hints use the 1B model; per-task model routing lets you switch heavy tasks to a hosted provider later without code changes.

### Risk: Mobile network access to homelab

Mitigation: Tailscale on phone + homelab; configurable API base URL; offline reading works fully; AI features degrade gracefully when API unreachable.

### Risk: Entity over-highlighting

Mitigation: highlight modes; default `subtle`; prioritise repeated/important entities; user can disable.

### Risk: Embedding model swap surprises

Mitigation: version-tagged vectors; admin shows reindex impact before applying; ReindexBookJob runs in background; old vectors deleted only as new ones land.

### Risk: IndexedDB quota

Mitigation: one-book whole-download keeps payloads bounded; admin shows per-book size; eviction policy: oldest-opened cached book first; manual "Remove offline copy" per book in library.

---

## 30. Definition of MVP Done

The MVP is done when:

```text
- I can upload a DRM-free EPUB.
- The backend parses, chunks, embeds, and extracts events/relationships.
- I can read the book on my phone, fully offline after first open, paginated like Readera.
- My progress is saved and resumes on my laptop with last-write-wins.
- I can ask a natural question about what I have already read; answer cites only reader-visible content.
- I can select any passage and get an instant entity card or a fast 1B-model hint.
- I can see a section recap card when I enter a new chapter.
- I can tap a known character/place/entity and see a short spoiler-safe popup.
- I can see a timeline of events up to my current position.
- I can see a relationship graph of entities up to my current position.
- I can bookmark a position and jump back to it.
- I can switch the LLM provider and per-task model from the admin panel without restarting anything.
- I can edit prompts from the admin panel.
- I can see every LLM call (prompt, response, latency, cost) in the admin call log.
- Spoiler-safe filtering is verifiable from the call log.
```

Everything else is iteration.

---

## 31. Longer-Term Ideas

### Additional Formats

- TXT, Markdown, HTML, PDF, DOCX, audiobook transcript import.

### Better Reading Features

- highlights with colour
- notes
- collections / tags
- reading streaks
- quote export
- offline TTS
- multi-window reading on tablet

### Better Story Intelligence

- unresolved mystery tracker
- object tracker
- faction tracker
- "what changed since last chapter?"
- "remind me who everyone in this scene is"
- "explain this scene using only what I know so far"
- comparative graphs across books in a series

### Better Deployment

- reverse proxy (Caddy) with HTTPS
- Tailscale/WireGuard access (default)
- automated backups
- update script
- health dashboards

### Optional Hosted Model Support

Already supported via `OpenAiCompatibleClient`. Spoiler-safety logic remains in Storymark, regardless of provider. Future native adapters (Anthropic, Google) can be added behind the same `IChatModelClient` interface.
