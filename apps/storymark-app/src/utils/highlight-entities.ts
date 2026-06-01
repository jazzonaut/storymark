import type { SectionEntity } from "@storymark/shared-types";
import type { EntityHighlightMode } from "@/db/storymark-db";

interface Term {
  readonly term: string;
  readonly entityId: string;
  readonly name: string;
}

const MIN_TERM_LENGTH = 2;

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// Canonical names + aliases, longest first so a multi-word name ("Captain Maren")
// wins over one of its parts ("Maren").
function buildTerms(entities: readonly SectionEntity[]): Term[] {
  const terms: Term[] = [];
  for (const entity of entities) {
    for (const raw of [entity.canonicalName, ...entity.aliases]) {
      const term = raw.trim();
      if (term.length >= MIN_TERM_LENGTH) {
        terms.push({ term, entityId: entity.entityId, name: entity.canonicalName });
      }
    }
  }
  return terms.toSorted((a, b) => b.term.length - a.term.length);
}

/** Wraps every visible entity name/alias occurrence in `html` in a tappable
 * `<button class="sm-entity" data-entity-id data-entity-name>`. Input is already
 * sanitised server-side and we only add buttons, so output stays safe. Matching is
 * whole-word, case-insensitive, within a single text node (won't span existing inline
 * tags — fine for flat-`<p>` prose). Returns `html` unchanged for mode `off` or when
 * there is nothing to match. */
export function highlightEntities(
  html: string,
  entities: readonly SectionEntity[] | undefined,
  mode: EntityHighlightMode,
): string {
  // `entities` may be undefined for a section cached before this field existed
  // (Dexie holds the old shape until the book is re-cached) — treat as no entities.
  if (mode === "off" || entities === undefined || entities.length === 0) {
    return html;
  }

  const terms = buildTerms(entities);
  if (terms.length === 0) {
    return html;
  }

  const byTerm = new Map<string, Term>();
  for (const term of terms) {
    const key = term.term.toLowerCase();
    if (!byTerm.has(key)) {
      byTerm.set(key, term);
    }
  }
  const pattern = new RegExp(`\\b(?:${terms.map((t) => escapeRegExp(t.term)).join("|")})\\b`, "gi");

  const doc = new DOMParser().parseFromString(html, "text/html");
  const walker = doc.createTreeWalker(doc.body, NodeFilter.SHOW_TEXT);
  const textNodes: Text[] = [];
  for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
    textNodes.push(node as Text);
  }

  for (const text of textNodes) {
    const value = text.nodeValue ?? "";
    const parent = text.parentElement;
    if (parent === null || parent.closest("a") !== null) {
      continue;
    }
    pattern.lastIndex = 0;
    if (!pattern.test(value)) {
      continue;
    }

    pattern.lastIndex = 0;
    const fragment = doc.createDocumentFragment();
    let last = 0;
    for (let match = pattern.exec(value); match !== null; match = pattern.exec(value)) {
      const entry = byTerm.get(match[0].toLowerCase());
      if (entry === undefined) {
        continue;
      }
      if (match.index > last) {
        fragment.appendChild(doc.createTextNode(value.slice(last, match.index)));
      }
      const button = doc.createElement("button");
      button.type = "button";
      button.className = "sm-entity";
      button.dataset.entityId = entry.entityId;
      button.dataset.entityName = entry.name;
      button.textContent = match[0];
      fragment.appendChild(button);
      last = match.index + match[0].length;
    }
    if (last < value.length) {
      fragment.appendChild(doc.createTextNode(value.slice(last)));
    }
    parent.replaceChild(fragment, text);
  }

  return doc.body.innerHTML;
}
