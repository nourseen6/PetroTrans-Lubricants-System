import { useEffect, useMemo, useState } from 'react'
import { fetchCustomers, searchVariants, type CustomerListItem, type VariantPick } from '../api/catalog'
import {
  createAdjustment,
  createTransfer,
  deleteAdjustment,
  fetchActiveWarehouses,
  fetchAdjustments,
  fetchMovements,
  fetchOnHand,
  fetchReturnableInvoice,
  fetchReturns,
  postReturn,
  saveReturn,
  updateAdjustment,
  type AdjustmentRow,
  type MovementRow,
  type NamedLookup,
  type ReturnableInvoice,
  type StockRow,
} from '../api/operations'
import { fetchInvoices, type InvoiceListItem } from '../api/sales'
import { useLocale } from '../i18n/LocaleContext'
import { EmptyState, ErrorBanner, StatusBadge, ConfirmDialog } from '../ui/Feedback'
import { formatDate, formatMoney, todayIso, movementLabel, parseAmount } from '../ui/format'
import { Typeahead } from '../ui/Typeahead'
import { groupByCatalogCategory } from '../ui/catalogGroups'

export function InventoryPage() {
  const { t } = useLocale()
  const [error, setError] = useState<unknown>(null)
  const [stock, setStock] = useState<StockRow[]>([])
  const [moves, setMoves] = useState<MovementRow[]>([])
  const [warehouses, setWarehouses] = useState<NamedLookup[]>([])
  const [variants, setVariants] = useState<VariantPick[]>([])
  const [warehouseId, setWarehouseId] = useState('')
  const [variantId, setVariantId] = useState('')
  const [productQuery, setProductQuery] = useState('')
  const [qty, setQty] = useState('1')
  const [reason, setReason] = useState('')
  const [direction, setDirection] = useState<'in' | 'out'>('in')
  const [toWarehouseId, setToWarehouseId] = useState('')
  const [stockTick, setStockTick] = useState(0)
  const [adjustments, setAdjustments] = useState<AdjustmentRow[]>([])
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editOccurredAt, setEditOccurredAt] = useState('')
  const [deleteId, setDeleteId] = useState<string | null>(null)

  async function refresh() {
    const [onHand, movements, warehouseList, variantList, adjustmentList] = await Promise.all([
      fetchOnHand(),
      fetchMovements(),
      fetchActiveWarehouses(),
      searchVariants(''),
      fetchAdjustments(),
    ])
    setStock(onHand)
    setMoves(movements)
    setWarehouses(warehouseList)
    setVariants(variantList)
    setAdjustments(adjustmentList)
    setWarehouseId((current) => current || warehouseList[0]?.id || '')
    if (variantList.length === 1) {
      setVariantId(variantList[0].id)
      setProductQuery(`${variantList[0].productName} — ${variantList[0].packagingType} ${variantList[0].packagingSize}`)
    }
    setToWarehouseId((current) => current || warehouseList[1]?.id || '')
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [stockTick])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void searchVariants(productQuery).then(setVariants).catch(() => setVariants([]))
    }, 180)
    return () => window.clearTimeout(handle)
  }, [productQuery])

  async function run<T>(action: () => Promise<T>) {
    setError(null)
    try {
      await action()
      await refresh()
    } catch (err) {
      setError(err)
    }
  }

  function resetAdjustForm() {
    setEditingId(null)
    setEditOccurredAt('')
    setQty('1')
    setReason('')
    setDirection('in')
    setVariantId('')
    setProductQuery('')
  }

  function startEdit(row: AdjustmentRow) {
    const line = row.lines[0]
    setEditingId(row.id)
    setEditOccurredAt(row.occurredAt)
    setWarehouseId(row.warehouseId)
    setDirection(row.direction === 'out' ? 'out' : 'in')
    setReason(row.reason)
    if (line) {
      setVariantId(line.variantId)
      setQty(String(line.quantity))
      setProductQuery(`${line.productName} — ${line.packagingType} ${line.packagingSize}`)
    }
  }

  async function submitAdjust() {
    const quantity = parseAmount(qty)
    if (!warehouseId || !variantId || quantity == null || quantity <= 0) {
      return
    }
    const body = {
      warehouseId,
      direction,
      reason,
      notes: null,
      occurredAt: editingId ? (editOccurredAt || new Date().toISOString()) : new Date().toISOString(),
      lines: [{ variantId, quantity }],
    }
    if (editingId) {
      await updateAdjustment(editingId, body)
    } else {
      await createAdjustment(body)
    }
    resetAdjustForm()
  }

  const stockTotals = useMemo(() => {
    const qty = stock.reduce((sum, row) => sum + row.onHand, 0)
    const value = stock.reduce((sum, row) => sum + (row.stockValue ?? ((row.companyCost ?? 0) * row.onHand)), 0)
    return { qty, value }
  }, [stock])

  return (
    <section>
      <h1 className="page-title">{t.nav.inventory}</h1>
      <ErrorBanner error={error} onRetry={() => void refresh()} />

      <h2 className="editor-title">{t.ops.stock}</h2>
      {stock.length === 0 ? (
        <EmptyState title={t.ops.emptyStock} />
      ) : (
        <>
          <p className="muted">{t.ops.stockTotalHint}</p>
          <div className="home-grid profit-kpis">
            <div className="kpi-card">
              <span className="kpi-label">{t.ops.stockQtyTotal}</span>
              <span className="kpi-value">{stockTotals.qty.toLocaleString('ar-EG', { maximumFractionDigits: 2 })}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.home.inventoryValue}</span>
              <span className="kpi-value">{formatMoney(stockTotals.value)}</span>
            </div>
          </div>
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.sales.warehouse}</th>
                <th>{t.sales.product}</th>
                <th>{t.ops.stock}</th>
                <th>{t.ops.stockValue}</th>
              </tr>
            </thead>
            <tbody>
              {groupByCatalogCategory(stock, (row) => row.category).flatMap((group) => [
                <tr key={`cat-${group.title}`} className="category-row">
                  <td colSpan={4}>{group.title}</td>
                </tr>,
                ...group.rows.map((row) => (
                  <tr key={`${row.warehouseId}-${row.variantId}`}>
                    <td>{row.warehouseName}</td>
                    <td>{row.productName} — {row.packagingType} {row.packagingSize}</td>
                    <td>
                      {row.onHand}
                      {row.belowMin ? <StatusBadge kind="warn">{t.home.lowStock}</StatusBadge> : null}
                    </td>
                    <td>{row.stockValue != null ? formatMoney(row.stockValue) : (row.companyCost != null ? formatMoney(row.companyCost * row.onHand) : '—')}</td>
                  </tr>
                )),
              ])}
              <tr className="category-row">
                <td colSpan={2}>{t.ops.stockQtyTotal}</td>
                <td>{stockTotals.qty.toLocaleString('ar-EG', { maximumFractionDigits: 2 })}</td>
                <td>{formatMoney(stockTotals.value)}</td>
              </tr>
            </tbody>
          </table>
        </>
      )}

      <section className="settings-block">
        <h2 className="editor-title">{editingId ? t.ops.editAdjust : t.ops.adjust}</h2>
        <form className="toolbar" onSubmit={(event) => { event.preventDefault(); void run(() => submitAdjust()) }}>
          {warehouses.length === 1 ? <span className="muted">{warehouses[0].name}</span> : (
            <select value={warehouseId} onChange={(event) => setWarehouseId(event.target.value)}>{warehouses.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}</select>
          )}
          <Typeahead
            items={variants.filter((row) => row.isActive)}
            query={productQuery}
            onQuery={(value) => {
              setProductQuery(value)
              setVariantId('')
            }}
            placeholder={t.sales.searchProduct}
            itemKey={(item) => item.id}
            onSelect={(item) => {
              setVariantId(item.id)
              setProductQuery(`${item.productName} — ${item.packagingType} ${item.packagingSize}`)
            }}
            renderItem={(item) => (
              <span>
                <strong>{item.productName}</strong>
                <small>{item.packagingType} {item.packagingSize}</small>
              </span>
            )}
          />
          <select value={direction} onChange={(event) => setDirection(event.target.value as 'in' | 'out')}>
            <option value="in">{t.ops.directionIn}</option>
            <option value="out">{t.ops.directionOut}</option>
          </select>
          <input className="amount-input" value={qty} onChange={(event) => setQty(event.target.value)} />
          <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder={t.ops.reason} required />
          <button className="primary-btn toolbar-btn" type="submit">{editingId ? t.save : t.ops.adjust}</button>
          {editingId ? (
            <button type="button" className="btn-secondary" onClick={resetAdjustForm}>{t.cancel}</button>
          ) : null}
        </form>
        {adjustments.length === 0 ? (
          <EmptyState title={t.ops.emptyAdjustments} />
        ) : (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.sales.date}</th>
                <th>{t.sales.warehouse}</th>
                <th>{t.sales.product}</th>
                <th>{t.ops.adjust}</th>
                <th>{t.sales.quantity}</th>
                <th>{t.ops.reason}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {adjustments.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.occurredAt)}</td>
                  <td>{row.warehouseName}</td>
                  <td>{row.lines.map((line) => `${line.productName} — ${line.packagingType} ${line.packagingSize}`).join('، ')}</td>
                  <td>{row.direction === 'out' ? t.ops.directionOut : t.ops.directionIn}</td>
                  <td>{row.lines.map((line) => String(line.quantity)).join(' + ')}</td>
                  <td>{row.reason}</td>
                  <td>
                    <button type="button" className="link-btn" onClick={() => startEdit(row)}>{t.ops.editAdjust}</button>
                    <button type="button" className="link-btn" onClick={() => setDeleteId(row.id)}>{t.ops.deleteAdjust}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {warehouses.length >= 2 ? (
        <section className="settings-block">
          <h2 className="editor-title">{t.ops.transfer}</h2>
          <form className="toolbar" onSubmit={(event) => { event.preventDefault(); void run(() => createTransfer({ fromWarehouseId: warehouseId, toWarehouseId, notes: null, occurredAt: new Date().toISOString(), lines: [{ variantId, quantity: Number(qty) }] })) }}>
            <select value={warehouseId} onChange={(event) => setWarehouseId(event.target.value)}>{warehouses.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}</select>
            <select value={toWarehouseId} onChange={(event) => setToWarehouseId(event.target.value)}>{warehouses.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}</select>
            <button className="primary-btn toolbar-btn" type="submit">{t.ops.transfer}</button>
          </form>
        </section>
      ) : null}

      <SalesReturnsSection onPosted={() => setStockTick((value) => value + 1)} />

      <h2 className="editor-title">{t.ops.movements}</h2>
      {moves.length === 0 ? <p className="muted">{t.ops.emptyStock}</p> : (
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.sales.date}</th>
              <th>{t.sales.warehouse}</th>
              <th>{t.sales.product}</th>
              <th>{t.ops.movements}</th>
              <th>{t.sales.quantity}</th>
            </tr>
          </thead>
          <tbody>
            {moves.map((row) => (
              <tr key={row.id}>
                <td>{formatDate(row.occurredAt)}</td>
                <td>{row.warehouseName}</td>
                <td>{row.productName} — {row.packagingType} {row.packagingSize}</td>
                <td>{movementLabel(row.movementType, row.direction)}</td>
                <td>{row.quantity}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {deleteId ? (
        <ConfirmDialog
          title={t.ops.deleteAdjust}
          body={t.ops.deleteAdjustBody}
          confirmLabel={t.ops.deleteAdjust}
          onConfirm={() => void run(async () => {
            await deleteAdjustment(deleteId)
            if (editingId === deleteId) resetAdjustForm()
            setDeleteId(null)
          })}
          onCancel={() => setDeleteId(null)}
        />
      ) : null}
    </section>
  )
}

function SalesReturnsSection({ onPosted }: { onPosted: () => void }) {
  const { t } = useLocale()
  const [error, setError] = useState<unknown>(null)
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([])
  const [returns, setReturns] = useState<Array<{ id: string; returnDate: string; customerName: string; originalNumber: string | null; status: string }>>([])
  const [customerId, setCustomerId] = useState('')
  const [invoiceId, setInvoiceId] = useState('')
  const [invoice, setInvoice] = useState<ReturnableInvoice | null>(null)
  const [qtyByVariant, setQtyByVariant] = useState<Record<string, string>>({})
  const [busy, setBusy] = useState(false)

  async function refreshReturns() {
    const rows = await fetchReturns()
    setReturns(rows)
  }

  useEffect(() => {
    void Promise.all([fetchCustomers(''), refreshReturns()]).then(([customerList]) => {
      setCustomers(customerList)
    }).catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    if (!customerId) {
      setInvoices([])
      setInvoiceId('')
      setInvoice(null)
      setQtyByVariant({})
      return
    }
    const handle = window.setTimeout(() => {
      void fetchInvoices('', customerId)
        .then((rows) => setInvoices(rows.filter((row) => row.status === 'posted')))
        .catch((err: unknown) => setError(err))
    }, 80)
    return () => window.clearTimeout(handle)
  }, [customerId])

  useEffect(() => {
    if (!invoiceId) {
      setInvoice(null)
      setQtyByVariant({})
      return
    }
    void fetchReturnableInvoice(invoiceId)
      .then((row) => {
        setInvoice(row)
        setQtyByVariant({})
      })
      .catch((err: unknown) => setError(err))
  }, [invoiceId])

  const selectedLines = useMemo(() => {
    if (!invoice) {
      return []
    }
    return invoice.lines
      .map((line) => {
        const qty = Number(qtyByVariant[line.variantId] ?? '')
        return { line, qty: Number.isFinite(qty) ? qty : 0 }
      })
      .filter((row) => row.qty > 0)
  }, [invoice, qtyByVariant])

  function fillAll() {
    if (!invoice) {
      return
    }
    const next: Record<string, string> = {}
    for (const line of invoice.lines) {
      if (line.returnableQty > 0) {
        next[line.variantId] = String(line.returnableQty)
      }
    }
    setQtyByVariant(next)
  }

  async function submit() {
    if (!customerId || !invoice) {
      return
    }
    const lines = selectedLines.map((row) => ({ variantId: row.line.variantId, quantity: row.qty }))
    if (lines.length === 0) {
      setError(new Error(t.ops.noReturnable))
      return
    }
    const over = selectedLines.find((row) => row.qty > row.line.returnableQty)
    if (over) {
      setError(new Error('كمية المرتجع أكبر من الكمية القابلة للإرجاع.'))
      return
    }
    setBusy(true)
    setError(null)
    try {
      const created = await saveReturn(null, {
        customerId,
        originalSalesInvoiceId: invoice.invoiceId,
        warehouseId: invoice.warehouseId,
        returnDate: todayIso(),
        notes: null,
        lines,
      })
      await postReturn(created.id)
      const refreshed = await fetchReturnableInvoice(invoice.invoiceId)
      setInvoice(refreshed)
      setQtyByVariant({})
      await refreshReturns()
      onPosted()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="settings-block">
      <h2 className="editor-title">{t.ops.returns}</h2>
      <p className="muted">{t.ops.returnHint}</p>
      <ErrorBanner error={error} />
      <form className="toolbar" onSubmit={(event) => { event.preventDefault(); void submit() }}>
        <select
          value={customerId}
          onChange={(event) => {
            setCustomerId(event.target.value)
            setInvoiceId('')
          }}
        >
          <option value="">{t.sales.customer}</option>
          {customers.map((row) => <option key={row.id} value={row.id}>{row.name} — {row.code}</option>)}
        </select>
        <select value={invoiceId} onChange={(event) => setInvoiceId(event.target.value)} disabled={!customerId}>
          <option value="">{t.ops.chooseInvoice}</option>
          {invoices.map((row) => (
            <option key={row.id} value={row.id}>{row.number ?? row.id} — {formatMoney(row.goodsTotal)}</option>
          ))}
        </select>
        <button className="btn-secondary toolbar-btn" type="button" disabled={!invoice || busy} title={!invoice ? t.ops.chooseInvoice : t.ops.returnAll} onClick={fillAll}>{t.ops.returnAll}</button>
        <button className="primary-btn toolbar-btn" type="submit" disabled={!invoice || busy || selectedLines.length === 0} title={!invoice ? t.ops.chooseInvoice : t.ops.postReturn}>{t.ops.postReturn}</button>
      </form>

      {invoice && invoice.lines.every((line) => line.returnableQty <= 0) ? (
        <p className="muted">{t.ops.noReturnable}</p>
      ) : null}

      {invoice && invoice.lines.length > 0 ? (
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.sales.product}</th>
              <th>{t.ops.soldQty}</th>
              <th>{t.ops.alreadyReturned}</th>
              <th>{t.ops.returnableQty}</th>
              <th>{t.sales.unitPrice}</th>
              <th>{t.ops.returnQty}</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lines.map((line) => (
              <tr key={line.variantId}>
                <td>{line.productName} — {line.packagingType} {line.packagingSize}</td>
                <td>{line.soldQty}</td>
                <td>{line.alreadyReturnedQty}</td>
                <td>{line.returnableQty}</td>
                <td>{formatMoney(line.unitPrice)}</td>
                <td>
                  <input
                    inputMode="decimal"
                    value={qtyByVariant[line.variantId] ?? ''}
                    disabled={line.returnableQty <= 0 || busy}
                    onChange={(event) => setQtyByVariant((current) => ({ ...current, [line.variantId]: event.target.value }))}
                    placeholder="0"
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}

      {returns.length === 0 ? (
        <p className="muted">{t.ops.emptyReturns}</p>
      ) : (
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.sales.date}</th>
              <th>{t.sales.customer}</th>
              <th>{t.sales.number}</th>
              <th>{t.sales.documentStatus}</th>
            </tr>
          </thead>
          <tbody>
            {returns.map((row) => (
              <tr key={row.id}>
                <td>{formatDate(row.returnDate)}</td>
                <td>{row.customerName}</td>
                <td>{row.originalNumber ?? '—'}</td>
                <td><StatusBadge kind={row.status === 'posted' ? 'posted' : 'draft'}>{row.status === 'posted' ? t.sales.posted : t.sales.draft}</StatusBadge></td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  )
}
