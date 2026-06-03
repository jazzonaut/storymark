# Testing

Automated tests were deferred through Phase 6. The first reintroduced harness is a live spoiler-safety check that exercises the public reader API against an indexed book and asserts configured future terms do not appear.

## Spoiler-Safety Harness

Create a case file like `docs/testing/spoiler-safety-cases.example.json`, then run:

```bash
$env:STORYMARK_RUN_SPOILER_HARNESS = "1"
$env:STORYMARK_API_BASE_URL = "http://localhost:8088"
$env:STORYMARK_SPOILER_CASES = "docs/testing/my-spoiler-cases.json"
pnpm test:spoiler
```

The harness checks `/ask`, `/memory`, `/events`, `/relationships`, `/entities`, and any configured entity-summary IDs at each reading boundary. It also asserts `/ask` reports `usedFutureContent: false`.

How forbidden terms are matched:

- Terms are matched **whole-word, case-insensitive**, against **prose string values only** — never against JSON key names or classification fields (`eventType`, `relationshipType`, `entityType`, `polarity`, `processingStatus`). A spoiler-safe *past* event whose `eventType` is `betrayal` does not trip a forbidden `"betrayal"`.
- An entity-summary `404` means the entity is **not yet visible** at that position — the spoiler-correct answer — so it passes. List an entity's id in `entityIds` with a boundary *before* its first appearance to assert it stays hidden.
- Cases run sequentially: each `/ask` is an LLM generation, and concurrent generations against a single local Ollama instance queue up and time out.

Without `STORYMARK_RUN_SPOILER_HARNESS=1`, the test is skipped so normal CI remains independent of local book fixtures.
