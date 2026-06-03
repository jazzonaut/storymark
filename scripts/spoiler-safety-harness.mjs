import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const RUN = process.env.STORYMARK_RUN_SPOILER_HARNESS === "1";
const API_BASE_URL = process.env.STORYMARK_API_BASE_URL ?? "http://localhost:8088";
const CASES_PATH =
  process.env.STORYMARK_SPOILER_CASES ?? "docs/testing/spoiler-safety-cases.example.json";

// Classification fields whose values come from a fixed enum vocabulary
// (eventType is literally allowed to be "betrayal" or "death"). A past event of
// type betrayal is spoiler-safe; only prose fields can leak future content.
const CLASSIFICATION_KEYS = new Set([
  "eventType",
  "relationshipType",
  "entityType",
  "polarity",
  "processingStatus",
]);

function collectProse(value, out) {
  if (typeof value === "string") {
    out.push(value);
  } else if (Array.isArray(value)) {
    for (const item of value) {
      collectProse(item, out);
    }
  } else if (value !== null && typeof value === "object") {
    for (const [key, child] of Object.entries(value)) {
      if (!CLASSIFICATION_KEYS.has(key)) {
        collectProse(child, out);
      }
    }
  }
}

function escapeRegExp(term) {
  return term.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Whole-word match over prose string values only: substring matching over the raw
// JSON also hits key names and enum values, failing on spoiler-safe responses.
function includesForbidden(value, forbidden) {
  const prose = [];
  collectProse(value, prose);
  const haystack = prose.join("\n").toLowerCase();
  return (
    forbidden.find((term) =>
      new RegExp(`\\b${escapeRegExp(term.toLowerCase())}\\b`).test(haystack),
    ) ?? null
  );
}

async function readCases() {
  const parsed = JSON.parse(await readFile(CASES_PATH, "utf8"));
  assert.equal(typeof parsed.bookId, "string", "top-level bookId is required");
  assert.ok(Array.isArray(parsed.cases), "top-level cases array is required");
  return parsed;
}

async function requestJson(path, { body, allowNotFound = false } = {}) {
  const init =
    body === undefined
      ? undefined
      : {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(body),
        };
  const response = await fetch(new URL(path, API_BASE_URL), init);

  if (response.status === 204 || (allowNotFound && response.status === 404)) {
    return null;
  }

  assert.ok(response.ok, `${path} returned ${response.status}`);
  return response.json();
}

function assertNoForbidden(surface, value, forbidden) {
  const found = includesForbidden(value, forbidden);
  assert.equal(found, null, `${surface} leaked forbidden term "${found}"`);
}

async function checkCase(bookId, scenario) {
  assert.equal(typeof scenario.name, "string", "case name is required");
  assert.equal(typeof scenario.section, "number", `${scenario.name}: section is required`);
  assert.equal(typeof scenario.location, "number", `${scenario.name}: location is required`);
  assert.ok(Array.isArray(scenario.forbidden), `${scenario.name}: forbidden array is required`);

  const encodedBookId = encodeURIComponent(bookId);
  const boundary = `upToSection=${scenario.section}&upToLocation=${scenario.location}`;
  const forbidden = scenario.forbidden;

  if (scenario.question) {
    const answer = await requestJson(`/api/books/${encodedBookId}/ask`, {
      body: {
        question: scenario.question,
        currentSection: scenario.section,
        currentLocation: scenario.location,
      },
    });
    assert.equal(answer.usedFutureContent, false, `${scenario.name}: /ask used future content`);
    assertNoForbidden(`${scenario.name}: /ask`, answer, forbidden);
  }

  const memory = await requestJson(`/api/books/${encodedBookId}/memory?${boundary}`);
  if (memory !== null) {
    assertNoForbidden(`${scenario.name}: /memory`, memory, forbidden);
  }

  const events = await requestJson(`/api/books/${encodedBookId}/events?${boundary}`);
  assertNoForbidden(`${scenario.name}: /events`, events, forbidden);

  const relationships = await requestJson(`/api/books/${encodedBookId}/relationships?${boundary}`);
  assertNoForbidden(`${scenario.name}: /relationships`, relationships, forbidden);

  const entities = await requestJson(`/api/books/${encodedBookId}/entities?${boundary}`);
  assertNoForbidden(`${scenario.name}: /entities`, entities, forbidden);

  for (const entityId of scenario.entityIds ?? []) {
    // 404 means the entity is not yet visible at this position — the spoiler-safe
    // answer for a future entity, so it passes. A visible entity's summary is
    // checked for leaks like every other surface.
    // eslint-disable-next-line no-await-in-loop
    const summary = await requestJson(
      `/api/books/${encodedBookId}/entities/${encodeURIComponent(entityId)}/summary` +
        `?section=${scenario.section}&location=${scenario.location}`,
      { allowNotFound: true },
    );
    if (summary !== null) {
      assertNoForbidden(`${scenario.name}: entity ${entityId}`, summary, forbidden);
    }
  }
}

test(
  "spoiler-safe reader surfaces do not expose forbidden future terms",
  { skip: !RUN },
  async (t) => {
    const config = await readCases();
    // Sequential on purpose: each case fires an /ask generation, and concurrent
    // generations against a single local Ollama instance queue up and time out.
    for (const scenario of config.cases) {
      // eslint-disable-next-line no-await-in-loop
      await t.test(scenario.name, async () => checkCase(config.bookId, scenario));
    }
  },
);
