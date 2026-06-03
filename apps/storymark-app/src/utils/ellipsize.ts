/** Cuts `text` to at most `maxChars` UTF-16 units with a trailing ellipsis,
 * never splitting a surrogate pair — an emoji straddling the cut would
 * otherwise render as a broken glyph. */
export function ellipsize(text: string, maxChars: number): string {
  if (text.length <= maxChars) {
    return text;
  }
  const last = text.charCodeAt(maxChars - 1);
  const isHighSurrogate = last >= 0xd800 && last <= 0xdbff;
  return text.slice(0, isHighSurrogate ? maxChars - 1 : maxChars).trimEnd() + "…";
}
