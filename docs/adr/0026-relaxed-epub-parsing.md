# 0026 — Relaxed EPUB parsing for uploads

**Status:** accepted
**Date:** 2026-06-04

## Context

A real commercial EPUB (Hachette production) failed upload with a 500: its EPUB 2 NCX
references `OEBPS/cover.xhtml`, but the publisher swapped that page out of the manifest
without updating the NCX. VersOne.Epub defaults to the STRICT preset and throws
`Epub2NcxException` on the dangling reference, which aborted both metadata sniffing and
parsing.

Publisher defects like this are common in commercial EPUBs, and the person uploading the
book cannot fix the file. Strict parsing therefore doesn't protect anything — it just
makes legitimately purchased books unimportable. Mainstream readers (Kindle, Apple Books)
tolerate these defects.

## Decision

`EpubImportHandler` passes `EpubReaderOptions(EpubReaderOptionsPreset.RELAXED)` to every
`EpubReader.ReadBookAsync` call. RELAXED skips invalid or dangling pieces (navigation
entries pointing at missing content, missing spine/cover/manifest references) while still
failing on archives that are genuinely unreadable.

## Consequences

- Books with publisher production defects import and read normally; the defective piece
  (e.g. a broken TOC entry) is silently dropped rather than failing the whole book.
- A book with real missing content imports with that content absent instead of erroring.
  Acceptable: the import is best-effort on what the archive actually contains, and a
  partial book is more useful than no book.
- Truly corrupt archives still fail the upload with an error, as before.

## Alternatives considered

- **Stay STRICT, enable only `NavigationReaderOptions.SkipNavigationItemsReferencingMissingContent`.**
  Fixes exactly this defect class; the next different publisher defect 500s again and
  needs another flag. Rejected: whack-a-mole with no user-visible benefit to strictness.
- **`IGNORE_ALL_ERRORS` preset.** Swallows even structural errors, risking silently
  importing garbage. Rejected: RELAXED already covers the realistic defect space.
