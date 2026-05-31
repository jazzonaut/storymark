// Shared display formatters for LLM call cost/token columns, used by both the
// LLM Calls log and the Cost Meter views.

export function formatCost(value: number | null): string {
  return value === null ? "—" : `$${value.toFixed(6)}`;
}

export function formatTokens(tokensIn: number | null, tokensOut: number | null): string {
  if (tokensIn === null && tokensOut === null) {
    return "—";
  }
  return `${tokensIn ?? 0} / ${tokensOut ?? 0}`;
}
