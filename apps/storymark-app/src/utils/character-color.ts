export function isCharacter(type: string): boolean {
  return type === "Person" || type === "Creature";
}

export function characterColor(id: string): string {
  return `var(--sm-character-${id}, var(--sm-accent))`;
}

function distance(a: number, b: number): number {
  const delta = Math.abs(a - b);
  return Math.min(delta, 360 - delta);
}

// Each midpoint is unused. Prefer gaps far from co-occurring characters while
// reserving some separation from the rest of the cast, even for a large book.
export function nextHue(used: number[], peers: number[]): number {
  if (used.length === 0) return 255;
  used.sort((a, b) => a - b);
  let best = 0;
  let bestScore = -1;
  for (const [i, hue] of used.entries()) {
    const end = used[i + 1] ?? (used[0] ?? 0) + 360;
    const gap = (end - hue) / 2;
    const candidate = (hue + gap) % 360;
    const near = peers.reduce((min, peer) => Math.min(min, distance(candidate, peer)), 180);
    const score = gap + 2 * near;
    if (score > bestScore && gap > 0 && candidate !== hue && candidate !== end % 360) {
      best = candidate;
      bestScore = score;
    }
  }
  return best;
}
