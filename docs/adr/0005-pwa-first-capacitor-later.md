# 0005 — PWA-first, Capacitor wrap-up in Phase 7

**Status:** accepted

## Context

Storymark's frontend is Vue 3 and is intended to run on phone (primary), tablet, and laptop. The realistic distribution paths are:

- Capacitor → Android APK (sideload via ADB or distribute via release builds; iOS later if needed).
- Browser-installable PWA (Chrome's "Install app") with a service worker + IndexedDB.

Capacitor gives better "open with .epub" intents and slightly more reliable storage on iOS. PWA gives a faster iteration loop because every change is a refresh, not an APK rebuild + adb install. Storage on Android Chrome is effectively unlimited for IndexedDB and the install flow is one tap.

## Decision

Ship as a **PWA first**. Capacitor wrap-up is scheduled for Phase 7, after the reader and AI surfaces are stable.

The build is structured so the Capacitor wrap is additive: no API change, no storage migration. The PWA codebase IS the Capacitor codebase.

## Consequences

- Iteration loop is "save → refresh phone" rather than "rebuild → install → restart."
- "Open with Storymark" file intent isn't available until Phase 7; importing EPUBs is via the in-app file picker.
- iOS users get the weaker iOS PWA experience until Capacitor ships; not a v1 blocker.
- Bearer token lives in `localStorage` in PWA mode. Capacitor Secure Storage substitutes when that mode ships.

## Alternatives considered

- **Capacitor-first** — adds 30s+ per dev iteration on real device, multiplied across hundreds of cycles during MVP; not justified by the marginal native gains.
- **Browser only, no Capacitor** — leaves iOS permanently weak and rules out share-target intents.
- **Tauri / Electron desktop wrap** — out of scope; the user lives on the phone.
