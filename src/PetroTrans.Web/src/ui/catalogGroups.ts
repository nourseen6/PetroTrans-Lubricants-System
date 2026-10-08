export function catalogCategoryTitle(category: string | null | undefined): string {
  const key = (category ?? '').replaceAll('أ', 'ا').replaceAll('إ', 'ا').trim()
  if (key.includes('بنزين')) return 'زيوت محركات البنزين'
  if (key.includes('ديزل')) return 'زيوت محركات الديزل'
  if (key.includes('خاص')) return 'منتجات خاصة'
  return key || 'أصناف أخرى'
}

export function groupByCatalogCategory<T>(
  rows: T[],
  categoryOf: (row: T) => string | null | undefined,
): Array<{ title: string; rows: T[] }> {
  const groups: Array<{ title: string; rows: T[] }> = []
  for (const row of rows) {
    const title = catalogCategoryTitle(categoryOf(row))
    const last = groups[groups.length - 1]
    if (!last || last.title !== title) {
      groups.push({ title, rows: [row] })
    } else {
      last.rows.push(row)
    }
  }
  return groups
}
