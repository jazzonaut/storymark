import { characterColor, isCharacter } from "@/utils/character-color";
import type { SectionEntity } from "@storymark/shared-types";
import type { EntityHighlightMode } from "@/db/storymark-db";

interface Term {
  readonly character: boolean;
  readonly term: string;
  readonly entityId: string;
  readonly name: string;
}

const MIN_TERM_LENGTH = 2;

// Honorifics, pronouns, and generic role words: an alias built solely from these is a role
// label, not a name, so it must not highlight on its own (it would turn every "doctor" into a
// link to a named character). Mirrors GenericAliasWords in SectionExtractionJob.cs so the
// reader's highlights and the server's first-seen backfill agree on which aliases count.
const GENERIC_ALIAS_WORDS = new Set<string>([
  "i",
  "you",
  "he",
  "she",
  "it",
  "we",
  "they",
  "him",
  "her",
  "them",
  "his",
  "hers",
  "its",
  "their",
  "theirs",
  "the",
  "a",
  "an",
  "this",
  "that",
  "these",
  "those",
  "mr",
  "mrs",
  "ms",
  "miss",
  "mister",
  "mistress",
  "sir",
  "madam",
  "madame",
  "lady",
  "lord",
  "dr",
  "doctor",
  "prof",
  "professor",
  "reverend",
  "saint",
  "st",
  "captain",
  "commander",
  "general",
  "major",
  "colonel",
  "sergeant",
  "lieutenant",
  "admiral",
  "king",
  "queen",
  "prince",
  "princess",
  "duke",
  "duchess",
  "count",
  "countess",
  "baron",
  "baroness",
  "father",
  "mother",
  "brother",
  "sister",
  "son",
  "daughter",
  "uncle",
  "aunt",
  "cousin",
  "master",
  "boy",
  "girl",
  "man",
  "woman",
  "child",
  "kid",
  "lad",
  "lass",
  "guard",
  "soldier",
  "servant",
  "maid",
  "stranger",
  "figure",
  "old",
  "young",
]);

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

// A proper-noun occurrence carries at least one capital; a lowercase match of an entity
// name is a common word ("the rose"), not the character "Rose". Mirrors `.Any(char.IsUpper)`
// in SectionExtractionJob.cs so highlighting and the server's first-seen backfill agree.
function hasCapital(value: string): boolean {
  return /\p{Lu}/u.test(value);
}

// Typographic quotes and dashes folded to their ASCII forms so a name stored with a
// straight apostrophe ("Harlan's World") still matches the curly apostrophe the EPUB
// renders ("Harlan's World"). Strictly one char per char, so match offsets in the folded
// text map back onto the original for faithful display. Mirrors EntityNameNormalizer.Fold
// on the server, which gates the same entities for the Story Guide and selection lookup.
function fold(value: string): string {
  return value.replace(/[‘’‛`´ʼ]/g, "'").replace(/[‐-―−]/g, "-");
}

// A separator-insensitive key: folded, with any run of whitespace and abbreviation periods
// collapsed to one space, lowercased. Lets a stored name and a matched span be compared
// regardless of spacing — "Mrs. Bancroft", "Mrs.Bancroft" and "Mrs Bancroft" share a key.
// Mirrors EntityNameNormalizer.MatchKey, which resolves the same selections server-side.
function matchKey(value: string): string {
  return fold(value)
    .replace(/[.\s]+/g, " ")
    .trim()
    .toLowerCase();
}

// A term's regex source with flexible internal separators: the parts of a name are split
// on whitespace/abbreviation periods and rejoined with "[.\s]+", so "Mrs. Bancroft" also
// matches the book's "Mrs.Bancroft" and "Mrs Bancroft". Parts stay anchored, so a name with
// the separator dropped entirely ("MrsBancroft") still won't match. Only the pattern flexes
// — match offsets stay real spans in the length-preserving folded text, so display is exact.
function termPattern(term: string): string {
  return fold(term)
    .split(/[.\s]+/)
    .filter((part) => part.length > 0)
    .map(escapeRegExp)
    .join("[.\\s]+");
}

// An alias may anchor a highlight only if it reads like a proper name: it must contain an
// uppercase letter (generic role nouns like "doctor" are lowercase in prose) and not be built
// solely from honorifics, pronouns, or role words ("Captain", "The Doctor", "Mother"). Keeps
// real aliases ("Frodo", "Captain Maren") while rejecting generics that would mis-link an
// unrelated occurrence. Mirrors IsStrongAlias in SectionExtractionJob.cs.
function isStrongAlias(alias: string): boolean {
  if (!hasCapital(fold(alias))) {
    return false;
  }
  const words = matchKey(alias)
    .split(/[.\s]+/)
    .filter((word) => word.length > 0);
  return words.some((word) => !GENERIC_ALIAS_WORDS.has(word));
}

// Canonical names + strong aliases, longest first so a multi-word name ("Captain Maren")
// wins over one of its parts ("Maren"). The canonical name is always eligible; aliases pass
// only isStrongAlias, matching the server's first-seen backfill.
function buildTerms(entities: readonly SectionEntity[]): Term[] {
  const terms: Term[] = [];
  for (const entity of entities) {
    const eligible = [entity.canonicalName, ...entity.aliases.filter(isStrongAlias)];
    for (const raw of eligible) {
      const term = raw.trim();
      if (term.length >= MIN_TERM_LENGTH) {
        terms.push({
          character: isCharacter(entity.entityType),
          term,
          entityId: entity.entityId,
          name: entity.canonicalName,
        });
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
  characters: Set<string>,
): string {
  // `entities` may be undefined for a section cached before this field existed
  // (Dexie holds the old shape until the book is re-cached) — treat as no entities.
  if (mode === "off" || entities === undefined || entities.length === 0) {
    return html;
  }

  const terms = buildTerms(entities);
  const byTerm = new Map<string, Term>();
  const sources: string[] = [];
  for (const term of terms) {
    const key = matchKey(term.term);
    const source = termPattern(term.term);
    if (key.length > 0 && source.length > 0 && !byTerm.has(key)) {
      byTerm.set(key, term);
      sources.push(source);
    }
  }
  if (sources.length === 0) {
    return html;
  }
  const pattern = new RegExp(`\\b(?:${sources.join("|")})\\b`, "gi");

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
    // Match against the folded text but emit slices of the original, so display keeps the
    // book's own punctuation while matching ignores straight-vs-curly differences. fold()
    // is length-preserving, so a match's index/length apply unchanged to `value`.
    const haystack = fold(value);
    pattern.lastIndex = 0;
    if (!pattern.test(haystack)) {
      continue;
    }

    pattern.lastIndex = 0;
    const fragment = doc.createDocumentFragment();
    let last = 0;
    for (let match = pattern.exec(haystack); match !== null; match = pattern.exec(haystack)) {
      const entry = byTerm.get(matchKey(match[0]));
      if (entry === undefined) {
        continue;
      }
      const end = match.index + match[0].length;
      // For a proper-noun term ("Rose") skip a lowercase occurrence ("the rose") — it's a
      // common word, not the entity; leaving it unwrapped folds it into the surrounding text.
      // A lowercase common-noun name (an Object/Concept like "the ring") has no such collision
      // and matches as-is. Mirrors the server's first-seen backfill (SectionExtractionJob.cs).
      if (hasCapital(entry.term) && !hasCapital(value.slice(match.index, end))) {
        continue;
      }
      if (match.index > last) {
        fragment.appendChild(doc.createTextNode(value.slice(last, match.index)));
      }
      const button = doc.createElement("button");
      button.type = "button";
      button.className = "sm-entity";
      if (entry.character) {
        characters.add(entry.entityId);
        button.style.setProperty("--sm-underline", characterColor(entry.entityId));
        button.classList.add("sm-character");
      }
      button.dataset.entityId = entry.entityId;
      button.dataset.entityName = entry.name;
      button.textContent = value.slice(match.index, end);
      fragment.appendChild(button);
      last = end;
    }
    if (last < value.length) {
      fragment.appendChild(doc.createTextNode(value.slice(last)));
    }
    parent.replaceChild(fragment, text);
  }

  return doc.body.innerHTML;
}
