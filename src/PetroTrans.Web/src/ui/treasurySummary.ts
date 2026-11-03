import type { TreasuryEntry } from '../api/operations'

export type ItemSummaryRow = { code: string; name: string; count: number; totalIn: number; totalOut: number }

/**
 * Totals per treasury item for the movements given (one book at a time, so a cash-to-bank
 * transfer is never counted twice). Biggest spending first, then the incoming items.
 */
export function summarizeByItem(entries: TreasuryEntry[], nameOf: (code: string | null) => string): ItemSummaryRow[] {
  const rows = new Map<string, ItemSummaryRow>()
  for (const entry of entries) {
    const code = entry.category ?? ''
    const row = rows.get(code) ?? { code, name: nameOf(entry.category), count: 0, totalIn: 0, totalOut: 0 }
    row.count += 1
    if (entry.direction === 'in') row.totalIn += entry.amount
    else row.totalOut += entry.amount
    rows.set(code, row)
  }
  return [...rows.values()].sort((a, b) => b.totalOut - a.totalOut || b.totalIn - a.totalIn)
}
