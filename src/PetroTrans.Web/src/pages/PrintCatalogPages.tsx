import { useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { fetchCustomerPrices, fetchCustomers, fetchPricingMatrix, type PricingMatrixRow } from '../api/catalog'
import { fetchCompany, fetchOnHand, fetchWarehouses, type CompanySettings, type StockRow } from '../api/operations'
import { groupByCatalogCategory } from '../ui/catalogGroups'
import { formatDate, formatMoney, todayIso } from '../ui/format'
import { requestPdf, requestPrint } from '../ui/print'

// A4 content height at 96 dpi once the @page margins are taken off (297mm - 32mm ≈ 1000px), with a little slack.
const ONE_PAGE_PX = 960

function CatalogSheet({
  title,
  company,
  children,
  fitKey,
}: {
  title: string
  company: CompanySettings
  children: ReactNode
  /** When set, the sheet shrinks to end on one page; change it whenever the rows change. */
  fitKey?: number
}) {
  const sheet = useRef<HTMLElement>(null)

  // Shrinks the whole sheet just enough to end on a single page, however many products there are.
  // Only the printed content is measured (header + body), not the on-screen padding or buttons.
  useLayoutEffect(() => {
    const el = sheet.current
    if (fitKey === undefined || !el) return
    el.style.zoom = ''
    const header = el.querySelector<HTMLElement>('.print-header')
    const body = el.querySelector<HTMLElement>('.print-body')
    const height = (header?.offsetHeight ?? 0) + 16 + (body?.scrollHeight ?? 0)
    if (height > ONE_PAGE_PX) {
      el.style.zoom = String(Math.max(0.35, ONE_PAGE_PX / height))
    }
  }, [fitKey])

  return (
    <article ref={sheet} className="print-sheet print-compact" dir="rtl">
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>{title}</h2>
        </div>
      </header>
      <div className="print-body">{children}</div>
      <div className="print-actions">
        <button type="button" className="btn-secondary" onClick={() => window.history.back()}>رجوع</button>
        <button type="button" className="btn-secondary" onClick={() => requestPrint(title)}>طباعة</button>
        <button type="button" className="btn-secondary" onClick={() => requestPdf(`${title}.pdf`)}>PDF</button>
      </div>
    </article>
  )
}

/** Price list the sales rep takes to a customer and fills in by hand: quantity and total are left blank. */
export function PrintPriceListPage() {
  const customerId = new URLSearchParams(window.location.search).get('customer') ?? ''
  const [rows, setRows] = useState<PricingMatrixRow[] | null>(null)
  const [special, setSpecial] = useState<Map<string, number>>(new Map())
  const [customerName, setCustomerName] = useState('')
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)

  useEffect(() => {
    void Promise.all([
      fetchPricingMatrix(''),
      fetchCompany(),
      customerId ? fetchCustomerPrices(customerId) : Promise.resolve([]),
      customerId ? fetchCustomers('', true) : Promise.resolve([]),
    ])
      .then(([matrix, nextCompany, prices, customers]) => {
        setRows(matrix.filter((row) => row.isActive))
        setCompany(nextCompany)
        setSpecial(new Map(prices.map((price) => [price.variantId, price.unitPrice])))
        setCustomerName(customers.find((customer) => customer.id === customerId)?.name ?? '')
      })
      .catch((err: unknown) => setError(err))
  }, [customerId])

  if (error) return <p className="banner error">تعذر تحميل قائمة الأسعار.</p>
  if (!rows || !company) return <p className="muted">جاري التحميل…</p>
  let line = 0
  return (
    <CatalogSheet title="قائمة أسعار / طلبية" company={company}>
      <p className="print-date-note">أسعار يوم {formatDate(todayIso())}</p>
      <div className="print-fill-lines">
        <span>العميل: {customerName}</span>
        <span>التاريخ:</span>
        <span>المندوب:</span>
      </div>
      <table>
        <thead>
          <tr>
            <th className="col-no">م</th>
            <th>الصنف</th>
            <th>العبوة</th>
            <th>السعر</th>
            <th className="col-blank">الكمية</th>
            <th className="col-blank">الإجمالي</th>
          </tr>
        </thead>
        <tbody>
          {groupByCatalogCategory(rows, (row) => row.category).flatMap((group) => [
            <tr key={`g-${group.title}`} className="print-group-row">
              <td colSpan={6}>{group.title}</td>
            </tr>,
            ...group.rows.map((row) => {
              line += 1
              return (
                <tr key={row.variantId}>
                  <td className="col-no">{line}</td>
                  <td>{row.productName}{row.brand ? ` — ${row.brand}` : ''}</td>
                  <td>{row.packagingType} {row.packagingSize}</td>
                  <td>{formatMoney(special.get(row.variantId) ?? row.basePrice)}</td>
                  <td className="col-blank" />
                  <td className="col-blank" />
                </tr>
              )
            }),
          ])}
          <tr className="print-total-row">
            <td colSpan={5}>الإجمالي</td>
            <td className="col-blank" />
          </tr>
        </tbody>
      </table>
      <div className="print-fill-lines print-signatures">
        <span>توقيع العميل:</span>
        <span>توقيع المندوب:</span>
        <span>ملاحظات:</span>
      </div>
    </CatalogSheet>
  )
}

type StockLine = { variantId: string; productName: string; pack: string; category: string | null | undefined; onHand: number; cost: number | null; value: number | null }

/** What is in the warehouse right now, on one page: every product, its quantity, and the totals. */
export function PrintStockPage() {
  const warehouseId = new URLSearchParams(window.location.search).get('warehouse') ?? ''
  const [stock, setStock] = useState<StockRow[] | null>(null)
  const [catalog, setCatalog] = useState<PricingMatrixRow[]>([])
  const [warehouseName, setWarehouseName] = useState('')
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)

  useEffect(() => {
    void Promise.all([fetchOnHand(warehouseId || undefined), fetchPricingMatrix(''), fetchWarehouses(), fetchCompany()])
      .then(([rows, matrix, warehouses, nextCompany]) => {
        setStock(rows)
        setCatalog(matrix)
        setWarehouseName(warehouses.find((item) => item.id === warehouseId)?.name ?? '')
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [warehouseId])

  if (error) return <p className="banner error">تعذر تحميل المخزن.</p>
  if (!stock || !company) return <p className="muted">جاري التحميل…</p>

  // Every active product is listed, even at zero, in catalogue order; "all warehouses" adds
  // each product up across warehouses so it appears once.
  const byVariant = new Map<string, StockLine>()
  for (const row of catalog.filter((item) => item.isActive)) {
    byVariant.set(row.variantId, {
      variantId: row.variantId,
      productName: row.productName,
      pack: `${row.packagingType} ${row.packagingSize}`,
      category: row.category,
      onHand: 0,
      cost: row.purchasePrice ?? null,
      value: null,
    })
  }
  for (const row of stock) {
    const current = byVariant.get(row.variantId)
    if (current) {
      current.onHand += row.onHand
      current.cost = row.companyCost ?? current.cost
      if (row.stockValue != null) current.value = (current.value ?? 0) + row.stockValue
    } else {
      byVariant.set(row.variantId, {
        variantId: row.variantId,
        productName: row.productName,
        pack: `${row.packagingType} ${row.packagingSize}`,
        category: row.category,
        onHand: row.onHand,
        cost: row.companyCost ?? null,
        value: row.stockValue ?? null,
      })
    }
  }
  const lines = [...byVariant.values()]
  const totalQty = lines.reduce((sum, row) => sum + row.onHand, 0)
  const totalValue = lines.reduce((sum, row) => sum + (row.value ?? 0), 0)
  const hasValue = lines.some((row) => row.value != null)
  const inStock = lines.filter((row) => row.onHand > 0).length

  // Long lists go into two side-by-side columns, so the page shrinks far less to fit.
  type Entry = { kind: 'group'; title: string } | { kind: 'item'; no: number; row: StockLine }
  const entries: Entry[] = []
  let no = 0
  for (const group of groupByCatalogCategory(lines, (row) => row.category)) {
    entries.push({ kind: 'group', title: group.title })
    for (const row of group.rows) {
      no += 1
      entries.push({ kind: 'item', no, row })
    }
  }
  const twoColumns = lines.length > 30
  const half = Math.ceil(entries.length / 2)
  const columns = twoColumns ? [entries.slice(0, half), entries.slice(half)] : [entries]
  const showCost = hasValue && !twoColumns
  const span = 4 + (showCost ? 1 : 0) + (hasValue ? 1 : 0)

  function table(rows: Entry[], withTotal: boolean, key: number) {
    return (
      <table key={key}>
        <thead>
          <tr>
            <th className="col-no">م</th>
            <th>الصنف</th>
            <th>العبوة</th>
            <th>الكمية</th>
            {showCost ? <th>سعر الشركة</th> : null}
            {hasValue ? <th>القيمة</th> : null}
          </tr>
        </thead>
        <tbody>
          {rows.map((entry) =>
            entry.kind === 'group' ? (
              <tr key={`g-${entry.title}`} className="print-group-row">
                <td colSpan={span}>{entry.title}</td>
              </tr>
            ) : (
              <tr key={entry.row.variantId} className={entry.row.onHand <= 0 ? 'print-empty-stock' : undefined}>
                <td className="col-no">{entry.no}</td>
                <td>{entry.row.productName}</td>
                <td>{entry.row.pack}</td>
                <td>{entry.row.onHand.toLocaleString('ar-EG')}</td>
                {showCost ? <td>{entry.row.cost == null ? '—' : formatMoney(entry.row.cost)}</td> : null}
                {hasValue ? <td>{entry.row.value == null ? '—' : formatMoney(entry.row.value)}</td> : null}
              </tr>
            ),
          )}
          {withTotal ? (
            <tr className="print-total-row">
              <td colSpan={3}>الإجمالي</td>
              <td>{totalQty.toLocaleString('ar-EG')}</td>
              {showCost ? <td /> : null}
              {hasValue ? <td>{formatMoney(totalValue)}</td> : null}
            </tr>
          ) : null}
        </tbody>
      </table>
    )
  }

  return (
    <CatalogSheet title={warehouseName ? `جرد المخزن — ${warehouseName}` : 'جرد المخزن — كل المخازن'} company={company} fitKey={lines.length}>
      <div className="print-meta">
        <p><span>التاريخ</span>{formatDate(todayIso())}</p>
        <p><span>عدد الأصناف</span>{lines.length} (الموجود منها {inStock})</p>
        <p><span>إجمالي الكمية</span>{totalQty.toLocaleString('ar-EG')}</p>
        {hasValue ? <p><span>إجمالي القيمة بسعر الشركة</span>{formatMoney(totalValue)}</p> : null}
      </div>
      <div className={twoColumns ? 'print-two-columns' : undefined}>
        {columns.map((rows, index) => table(rows, index === columns.length - 1, index))}
      </div>
    </CatalogSheet>
  )
}
