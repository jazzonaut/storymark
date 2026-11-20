import { liveQuery } from "dexie";
import { computed, inject, onScopeDispose, provide, shallowRef } from "vue";
import type { InjectionKey } from "vue";
import { db } from "@/db/storymark-db";

import { nextHue } from "@/utils/character-color";

type Groups = readonly (readonly string[])[];
interface CharacterPalette {
  register: (source: "text" | "braid", groups: Groups) => Promise<void>;
  regenerate: (id: string) => Promise<void>;
}
const key: InjectionKey<CharacterPalette> = Symbol("character palette");

export function provideCharacterPalette(bookId: string) {
  const hues = shallowRef<Record<string, number>>({});
  const groups: Record<"text" | "braid", Groups> = { text: [], braid: [] };
  const subscription = liveQuery(() => db.characterPalettes.get(bookId)).subscribe((row) => {
    hues.value = row?.hues ?? {};
  });
  onScopeDispose(() => subscription.unsubscribe());

  async function assign(ids: Set<string>, regenerate = false): Promise<void> {
    if (!ids.size) return;
    await db.transaction("rw", db.characterPalettes, async () => {
      const assigned = (await db.characterPalettes.get(bookId))?.hues ?? {};
      let changed = false;
      for (const id of ids) {
        if (!regenerate && assigned[id] !== undefined) continue;
        const neighbors = new Set(
          [...groups.text, ...groups.braid].filter((group) => group.includes(id)).flat(),
        );
        const peers = Array.from(neighbors, (other) => assigned[other]).filter(
          (h) => h !== undefined,
        );
        const previous = assigned[id];
        if (previous !== undefined) peers.push(previous);
        assigned[id] = nextHue(Object.values(assigned), peers);
        changed = true;
      }
      if (changed) await db.characterPalettes.put({ bookId, hues: assigned });
    });
  }

  provide(key, {
    register(source, cast) {
      groups[source] = cast;
      return assign(new Set(cast.flat()));
    },
    regenerate(id) {
      return assign(new Set([id]), true);
    },
  });
  return computed(() =>
    Object.fromEntries(
      Object.entries(hues.value).map(([id, hue]) => [
        `--sm-character-${id}`,
        `oklch(var(--sm-character-lightness) 0.15 ${hue})`,
      ]),
    ),
  );
}

export function useCharacterPalette(): CharacterPalette {
  const palette = inject(key);
  if (!palette) throw new Error("Character colours require an open reader.");
  return palette;
}
