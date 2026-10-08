import { useEffect, useMemo, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import type { AuthUser } from '../api/auth'
import { fetchCustomers, searchVariants, type CustomerListItem, type VariantPick } from '../api/catalog'
import {
  createInvoice,
  deleteInvoice,
  fetchInvoice,
  fetchInvoices,
  postInvoice,
  unpostInvoice,
  updateInvoice,
  type InvoiceListItem,
} from '../api/sales'
import {
  createPayment,
  fetchActiveWarehouses,
  fetchOnHand,
  fetchPaymentMethods,
  type NamedLookup,
  type PaymentMethod,
  type StockRow,
} from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner, StatusBadge } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, todayIso, toDateInput } from '../ui/format'
import { Typeahead } from '../ui/Typeahead'
import { Icon } from '../ui/Icons'

type SalesPageProps = {
  user: AuthUser
  onNavigate: (path: string) => void
}

type DraftLine = {
  key: string
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  quantity: string
  unitPrice: string
  resolvedUnitPrice: number | null
  overrideReason: string
}

function newKey(): string {
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function lineFromVariant(variant: VariantPick): DraftLine {
  const price = variant.customerUnitPrice ?? variant.standardWholesalePrice
  return {
    key: newKey(),
    variantId: variant.id,
    productName: variant.productName,
    packagingType: variant.packagingType,
    packagingSize: variant.packagingSize,
    quantity: '1',
    unitPrice: price === null || price === undefined ? '' : String(price),
    resolvedUnitPrice: price ?? null,
    overrideReason: '',
  }
}

function paymentLabel(status: string, t: { sales: { paid: string; partial: string; unpaid: string } }) {
  if (status === 'paid') return t.sales.paid
  if (status === 'partial') return t.sales.partial
  return t.sales.unpaid
}

export function SalesPage({ user, onNavigate }: SalesPageProps) {
  const { t } = useLocale()
  const canOverride = user.permissions.includes('pricing.override')
  const canUnpost = user.permissions.includes('sales.edit_posted')
  const canDelete = user.permissions.includes('sales.create')
  const qtyRef = useRef<HTMLInputElement>(null)
  const productSearchRef = useRef<HTMLInputElement>(null)
  const unpaidOnly = typeof window !== 'undefined' && new URLSearchParams(window.location.search).get('unpaid') === '1'

  const [query, setQuery] = useState('')
  const [rows, setRows] = useState<InvoiceListItem[]>([])
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [customerQuery, setCustomerQuery] = useState('')
  const [warehouses, setWarehouses] = useState<NamedLookup[]>([])
  const [methods, setMethods] = useState<PaymentMethod[]>([])
  const [stock, setStock] = useState<StockRow[]>([])
  const [productQuery, setProductQuery] = useState('')
  const [productHits, setProductHits] = useState<VariantPick[]>([])
  const [customerHits, setCustomerHits] = useState<CustomerListItem[]>([])
  const [open, setOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [customerId, setCustomerId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [status, setStatus] = useState('draft')
  const [number, setNumber] = useState<string | null>(null)
  const [paidTotal, setPaidTotal] = useState(0)
  const [remainingTotal, setRemainingTotal] = useState(0)
  const [paymentStatus, setPaymentStatus] = useState('unpaid')
  const [showPay, setShowPay] = useState(false)
  const [payAmount, setPayAmount] = useState('')
  const [payMethodId, setPayMethodId] = useState('')
  const [invoiceDate, setInvoiceDate] = useState(todayIso)
  const [dueDate, setDueDate] = useState('')
  const [notes, setNotes] = useState('')
  const [discountAmount, setDiscountAmount] = useState('')
  const [manualTotal, setManualTotal] = useState('')
  const [manualTotalReason, setManualTotalReason] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([])
  const [error, setError] = useState<unknown>(null)
  const [ok, setOk] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [confirmPost, setConfirmPost] = useState(false)
  const [confirmUnpost, setConfirmUnpost] = useState(false)
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null)
  const [pendingQty, setPendingQty] = useState('1')
  const [pendingVariant, setPendingVariant] = useState<VariantPick | null>(null)

  const selectedCustomer = customers.find((row) => row.id === customerId)
  const posted = status === 'posted'
  const locked = posted
  const linesSubtotal = useMemo(
    () =>
      lines.reduce((sum, line) => {
        const quantity = parseAmount(line.quantity)
        const price = parseAmount(line.unitPrice)
        if (quantity === null || price === null) {
          return sum
        }
        return sum + quantity * price
      }, 0),
    [lines],
  )
  const discountValue = parseAmount(discountAmount) ?? 0
  const manualTotalValue = parseAmount(manualTotal)
  const goodsTotal = manualTotalValue ?? Math.max(linesSubtotal - discountValue, 0)

  const visibleRows = rows.filter((row) => {
    if (unpaidOnly && !(row.status === 'posted' && row.remainingTotal > 0)) {
      return false
    }
    const term = query.trim()
    if (!term) {
      return true
    }
    return (
      row.customerName.includes(term) ||
      row.customerCode.includes(term) ||
      (row.number != null && row.number.includes(term))
    )
  })

  async function loadLookups() {
    const [list, customerList, warehouseList, methodList] = await Promise.all([
      fetchInvoices(''),
      fetchCustomers(''),
      fetchActiveWarehouses(),
      fetchPaymentMethods(true),
    ])
    setRows(list)
    setCustomers(customerList)
    setWarehouses(warehouseList)
    setMethods(methodList)
    return { customerList, warehouseList, methodList }
  }

  useEffect(() => {
    void (async () => {
      const lookups = await loadLookups()
      applyDefaults(lookups.warehouseList, lookups.customerList, lookups.methodList)
      const params = new URLSearchParams(window.location.search)
      const id = params.get('id')
      const presetCustomer = params.get('customerId')
      if (id) {
        await openEdit(id)
        return
      }
      if (presetCustomer) {
        const found = lookups.customerList.find((row) => row.id === presetCustomer)
        setOpen(true)
        setEditingId(null)
        setCustomerId(found?.id ?? presetCustomer)
        setCustomerQuery(found ? `${found.name} — ${found.code}` : '')
        setWarehouseId(
          lookups.warehouseList.length === 1
            ? lookups.warehouseList[0].id
            : lookups.warehouseList[0]?.id ?? '',
        )
        setStatus('draft')
        setNumber(null)
        setPaidTotal(0)
        setRemainingTotal(0)
        setPaymentStatus('unpaid')
        setShowPay(false)
        setInvoiceDate(todayIso())
        setDueDate('')
        setNotes('')
        setDiscountAmount('')
        setManualTotal('')
        setManualTotalReason('')
        setLines([])
        setError(null)
        setOk(null)
        setProductQuery('')
        setPendingVariant(null)
        setPendingQty('1')
        if (lookups.methodList.length === 1) {
          setPayMethodId(lookups.methodList[0].id)
        }
      }
    })().catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    if (!warehouseId) {
      setStock([])
      return
    }
    void fetchOnHand(warehouseId).then(setStock).catch(() => setStock([]))
  }, [warehouseId])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void searchVariants(productQuery, customerId || undefined).then(setProductHits).catch(() => setProductHits([]))
    }, 180)
    return () => window.clearTimeout(handle)
  }, [productQuery, customerId])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void fetchCustomers(customerQuery).then(setCustomerHits).catch(() => setCustomerHits([]))
    }, 180)
    return () => window.clearTimeout(handle)
  }, [customerQuery])

  function applyDefaults(warehouseList: NamedLookup[], customerList: CustomerListItem[], methodList: PaymentMethod[]) {
    if (warehouseList.length === 1) {
      setWarehouseId(warehouseList[0].id)
    }
    if (customerList.length === 1) {
      setCustomerId(customerList[0].id)
      setCustomerQuery(`${customerList[0].name} — ${customerList[0].code}`)
    }
    if (methodList.length === 1) {
      setPayMethodId(methodList[0].id)
    }
  }

  function startCreate() {
    setOpen(true)
    setEditingId(null)
    setCustomerId(customers.length === 1 ? customers[0].id : '')
    setCustomerQuery(customers.length === 1 ? `${customers[0].name} — ${customers[0].code}` : '')
    setWarehouseId(warehouses.length === 1 ? warehouses[0].id : warehouseId || warehouses[0]?.id || '')
    setStatus('draft')
    setNumber(null)
    setPaidTotal(0)
    setRemainingTotal(0)
    setPaymentStatus('unpaid')
    setShowPay(false)
    setInvoiceDate(todayIso())
    setDueDate('')
    setNotes('')
    setDiscountAmount('')
    setManualTotal('')
    setManualTotalReason('')
    setLines([])
    setError(null)
    setOk(null)
    setProductQuery('')
    setPendingVariant(null)
    setPendingQty('1')
    applyDefaults(warehouses, customers, methods)
  }

  async function openEdit(id: string) {
    setError(null)
    setOk(null)
    const invoice = await fetchInvoice(id)
    setOpen(true)
    setEditingId(invoice.id)
    setCustomerId(invoice.customerId)
    const known = customers.find((row) => row.id === invoice.customerId)
    setCustomerQuery(known ? `${known.name} — ${known.code}` : invoice.customerName)
    setWarehouseId(invoice.warehouseId ?? (warehouses.length === 1 ? warehouses[0].id : ''))
    setStatus(invoice.status)
    setNumber(invoice.number)
    setPaidTotal(invoice.paidTotal)
    setRemainingTotal(invoice.remainingTotal)
    setPaymentStatus(invoice.paymentStatus)
    setInvoiceDate(toDateInput(invoice.invoiceDate))
    setDueDate(toDateInput(invoice.dueDate))
    setNotes(invoice.notes ?? '')
    setDiscountAmount(invoice.discountAmount ? String(invoice.discountAmount) : '')
    setManualTotal(invoice.hasManualTotal ? String(invoice.goodsTotal) : '')
    setManualTotalReason(invoice.manualTotalReason ?? '')
    setLines(
      invoice.lines.map((line) => ({
        key: line.id,
        variantId: line.variantId,
        productName: line.productName,
        packagingType: line.packagingType,
        packagingSize: line.packagingSize,
        quantity: String(line.quantity),
        unitPrice: line.unitPrice === null ? '' : String(line.unitPrice),
        resolvedUnitPrice: line.resolvedUnitPrice,
        overrideReason: line.overrideReason ?? '',
      })),
    )
    setShowPay(false)
  }

  function addPendingLine() {
    if (!pendingVariant || locked) {
      return
    }
    const qty = parseAmount(pendingQty) ?? 1
    const line = lineFromVariant(pendingVariant)
    line.quantity = String(qty)
    setLines((current) => [...current, line])
    setPendingVariant(null)
    setProductQuery('')
    setPendingQty('1')
    window.setTimeout(() => productSearchRef.current?.focus(), 0)
  }

  function invoiceBody() {
    return {
      customerId,
      warehouseId: warehouseId || null,
      invoiceDate,
      dueDate: dueDate || null,
      notes: notes.trim() || null,
      discountAmount: parseAmount(discountAmount) ?? 0,
      manualTotal: parseAmount(manualTotal),
      manualTotalReason: manualTotalReason.trim() || null,
      lines: lines.map((line) => ({
        variantId: line.variantId,
        quantity: parseAmount(line.quantity) ?? 0,
        unitPrice: parseAmount(line.unitPrice),
        overrideReason: canOverride ? line.overrideReason.trim() || null : null,
      })),
    }
  }

  function ensureCustomerSelected(): boolean {
    if (customerId) {
      return true
    }
    setError(t.sales.pickCustomer)
    return false
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    if (!ensureCustomerSelected()) {
      return
    }
    setBusy(true)
    setError(null)
    try {
      if (editingId) {
        await updateInvoice(editingId, invoiceBody())
      } else {
        await createInvoice(invoiceBody())
      }
      setOpen(false)
      await loadLookups()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function doPost() {
    if (!ensureCustomerSelected()) {
      return
    }
    setBusy(true)
    setError(null)
    setConfirmPost(false)
    try {
      const saved = editingId ? await updateInvoice(editingId, invoiceBody()) : await createInvoice(invoiceBody())
      const postedInvoice = await postInvoice(saved.id)
      setOk(t.sales.postSuccess)
      await openEdit(postedInvoice.id)
      await loadLookups()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function doUnpost() {
    if (!editingId) return
    setBusy(true)
    setError(null)
    setConfirmUnpost(false)
    try {
      const draft = await unpostInvoice(editingId)
      setOk(t.sales.unpostSuccess)
      await openEdit(draft.id)
      await loadLookups()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function doDelete() {
    const id = confirmDeleteId
    if (!id) return
    setBusy(true)
    setError(null)
    setConfirmDeleteId(null)
    try {
      await deleteInvoice(id)
      setOk(t.sales.deleteSuccess)
      if (editingId === id) {
        setOpen(false)
        setEditingId(null)
      }
      await loadLookups()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onPay(event: FormEvent) {
    event.preventDefault()
    if (!editingId) return
    const amount = parseAmount(payAmount) ?? 0
    if (amount > remainingTotal) {
      setError(t.sales.overpay)
      return
    }
    setBusy(true)
    setError(null)
    try {
      await createPayment({
        invoiceId: editingId,
        paymentMethodId: payMethodId,
        amount,
        paidOn: todayIso(),
        reference: null,
        notes: null,
      })
      setShowPay(false)
      await openEdit(editingId)
      await loadLookups()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function onQtyKey(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      event.preventDefault()
      addPendingLine()
    }
  }

  const payAmountValue = parseAmount(payAmount) ?? 0
  const overpay = showPay && payAmountValue > remainingTotal

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <p className="breadcrumb">{t.nav.home} / {t.nav.sales}</p>
          <h1 className="page-title">{t.nav.sales}</h1>
          <p className="muted">{t.sales.hint}</p>
        </div>
        <button type="button" className="primary-btn toolbar-btn icon-btn" onClick={startCreate}>
          <Icon name="add" />
          {t.sales.add}
        </button>
      </div>
      <ErrorBanner error={error} onRetry={() => void loadLookups()} />
      {ok ? <p className="banner ok">{ok}</p> : null}

      {open ? (
        <form className="editor" onSubmit={onSave}>
          <div className="page-head">
            <h2 className="editor-title">{posted ? t.sales.posted : editingId ? t.sales.edit : t.sales.add}</h2>
            <button type="button" className="btn-secondary" onClick={() => setOpen(false)}>
              {t.close}
            </button>
          </div>
          <div className="status-row">
            <StatusBadge kind={posted ? 'posted' : 'draft'}>{posted ? t.sales.posted : t.sales.draft}</StatusBadge>
            {posted ? (
              <StatusBadge kind={paymentStatus === 'paid' ? 'paid' : paymentStatus === 'partial' ? 'partial' : 'unpaid'}>
                {paymentLabel(paymentStatus, t)}
              </StatusBadge>
            ) : (
              <span className="muted">{t.sales.draftStockHint}</span>
            )}
            <span className="muted">
              {t.sales.number}: {number ?? t.sales.numberPending}
              {editingId ? ` · ${formatDate(invoiceDate)}` : ''}
            </span>
          </div>

          <div className="form-grid">
            <label className="field">
              <span>{t.sales.customer}</span>
              {locked ? (
                <input value={selectedCustomer ? `${selectedCustomer.name} — ${selectedCustomer.code}` : customerQuery} readOnly />
              ) : (
                <Typeahead
                  items={customerHits}
                  query={customerQuery}
                  onQuery={(value) => {
                    setCustomerQuery(value)
                    const selected = customers.find((row) => row.id === customerId)
                    const keep =
                      (selected != null && (value === `${selected.name} — ${selected.code}` || value === selected.name)) ||
                      (!selected && customerId !== '' && value === customerQuery)
                    if (!keep) {
                      setCustomerId('')
                    }
                  }}
                  placeholder={t.sales.chooseCustomer}
                  autoFocus={!customerId}
                  itemKey={(item) => item.id}
                  onSelect={(item) => {
                    setCustomerId(item.id)
                    setCustomerQuery(`${item.name} — ${item.code}`)
                  }}
                  renderItem={(item) => (
                    <span>
                      <strong>{item.name}</strong>
                      <small>
                        {item.code}
                        {item.phone ? ` · ${item.phone}` : ''}
                      </small>
                    </span>
                  )}
                />
              )}
            </label>
            <label className="field">
              <span>{t.sales.date}</span>
              <input type="date" value={invoiceDate} onChange={(event) => setInvoiceDate(event.target.value)} required disabled={locked} />
            </label>
            {warehouses.length === 1 ? (
              <label className="field">
                <span>{t.sales.warehouse}</span>
                <input value={warehouses[0].name} readOnly />
              </label>
            ) : (
              <label className="field">
                <span>{t.sales.warehouse}</span>
                <select value={warehouseId} onChange={(event) => setWarehouseId(event.target.value)} disabled={locked}>
                  <option value="">{t.sales.chooseWarehouse}</option>
                  {warehouses.map((warehouse) => (
                    <option key={warehouse.id} value={warehouse.id}>
                      {warehouse.name}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <label className="field">
              <span>{t.sales.dueDate}</span>
              <input type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} disabled={locked} />
            </label>
          </div>

          {locked ? null : (
            <div className="line-entry">
              <Typeahead
                items={productHits.filter((item) => item.isActive)}
                query={productQuery}
                inputRef={productSearchRef}
                onQuery={(value) => {
                  setProductQuery(value)
                  setPendingVariant(null)
                }}
                placeholder={t.sales.searchProduct}
                itemKey={(item) => item.id}
                emptyText={t.sales.noVariants}
                onSelect={(item) => {
                  setPendingVariant(item)
                  setProductQuery(`${item.productName} — ${item.packagingType} ${item.packagingSize}`)
                  window.setTimeout(() => qtyRef.current?.focus(), 0)
                }}
                renderItem={(item) => {
                  const onHand = stock.find((row) => row.variantId === item.id)?.onHand
                  const hasCustomerPrice = item.customerUnitPrice != null
                  const price = hasCustomerPrice ? item.customerUnitPrice : item.standardWholesalePrice
                  return (
                    <span>
                      <strong>{item.productName}</strong>
                      <small>
                        {item.packagingType} {item.packagingSize}
                        {price != null ? ` · ${formatMoney(price)}` : ''}
                        {hasCustomerPrice ? ` · ${t.sales.customerPrice}` : item.standardWholesalePrice != null ? ` · ${t.sales.basePrice}` : ''}
                        {onHand != null ? ` · ${t.sales.available}: ${onHand}` : ''}
                      </small>
                    </span>
                  )
                }}
              />
              <input
                ref={qtyRef}
                className="qty-input"
                value={pendingQty}
                onChange={(event) => setPendingQty(event.target.value)}
                onKeyDown={onQtyKey}
                inputMode="decimal"
                placeholder={t.sales.quantity}
              />
              <button type="button" className="primary-btn toolbar-btn" onClick={addPendingLine} disabled={!pendingVariant}>
                {t.sales.addLine}
              </button>
            </div>
          )}

          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.sales.product}</th>
                <th>{t.sales.quantity}</th>
                <th>{t.sales.unitPrice}</th>
                {canOverride ? <th>{t.sales.priceNote}</th> : null}
                <th>{t.sales.lineTotal}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {lines.length === 0 ? (
                <tr>
                  <td colSpan={canOverride ? 6 : 5}>{t.sales.noLines}</td>
                </tr>
              ) : (
                lines.map((line) => {
                  const quantity = parseAmount(line.quantity)
                  const price = parseAmount(line.unitPrice)
                  const total = quantity !== null && price !== null ? quantity * price : null
                  return (
                    <tr key={line.key}>
                      <td>
                        {line.productName}
                        <div className="muted">
                          {line.packagingType} {line.packagingSize}
                        </div>
                      </td>
                      <td>
                        <input
                          value={line.quantity}
                          onChange={(event) =>
                            setLines((current) => current.map((row) => (row.key === line.key ? { ...row, quantity: event.target.value } : row)))
                          }
                          inputMode="decimal"
                          required
                          disabled={locked}
                        />
                      </td>
                      <td>
                        {canOverride ? (
                          <input
                            value={line.unitPrice}
                            onChange={(event) =>
                              setLines((current) => current.map((row) => (row.key === line.key ? { ...row, unitPrice: event.target.value } : row)))
                            }
                            inputMode="decimal"
                            disabled={locked}
                          />
                        ) : line.unitPrice ? (
                          formatMoney(Number(line.unitPrice))
                        ) : (
                          <span className="muted">{t.sales.noPrice}</span>
                        )}
                      </td>
                      {canOverride ? (
                        <td>
                          <input
                            value={line.overrideReason}
                            onChange={(event) =>
                              setLines((current) =>
                                current.map((row) => (row.key === line.key ? { ...row, overrideReason: event.target.value } : row)),
                              )
                            }
                            placeholder={t.sales.priceNoteOptional}
                            disabled={locked}
                          />
                        </td>
                      ) : null}
                      <td>{formatMoney(total)}</td>
                      <td>
                        {locked ? null : (
                          <button type="button" className="btn-ghost" onClick={() => setLines((current) => current.filter((row) => row.key !== line.key))}>
                            {t.sales.remove}
                          </button>
                        )}
                      </td>
                    </tr>
                  )
                })
              )}
            </tbody>
          </table>

          <label className="field">
            <span>{t.sales.notes}</span>
            <textarea value={notes} onChange={(event) => setNotes(event.target.value)} rows={2} disabled={locked} />
          </label>

          <div className="form-grid">
            <label className="field">
              <span>{t.sales.discount}</span>
              <input value={discountAmount} onChange={(event) => setDiscountAmount(event.target.value)} disabled={locked} />
            </label>
            <label className="field">
              <span title={t.sales.manualTotalHint}>{t.sales.manualTotal}</span>
              <input value={manualTotal} onChange={(event) => setManualTotal(event.target.value)} disabled={locked || !canOverride} placeholder={canOverride ? '' : '—'} />
            </label>
            {manualTotal ? (
              <label className="field">
                <span>{t.sales.manualTotalReason}</span>
                <input value={manualTotalReason} onChange={(event) => setManualTotalReason(event.target.value)} disabled={locked || !canOverride} required={!!manualTotal} />
              </label>
            ) : null}
          </div>
          {manualTotal ? <p className="banner warn">{t.sales.manualTotalHint}</p> : null}

          <div className="totals">
            <div>
              <span>{t.sales.linesSubtotal}</span>
              <strong>{formatMoney(linesSubtotal)}</strong>
            </div>
            <div>
              <span>{t.sales.goodsTotal}</span>
              <strong>{formatMoney(posted ? remainingTotal + paidTotal : goodsTotal)}</strong>
            </div>
            <div>
              <span>{t.sales.paidTotal}</span>
              <strong>{formatMoney(posted ? paidTotal : 0)}</strong>
            </div>
            <div>
              <span>{t.sales.remainingTotal}</span>
              <strong>{formatMoney(posted ? remainingTotal : goodsTotal)}</strong>
            </div>
          </div>

          <div className="action-row">
            {locked ? null : (
              <button className="btn-secondary" type="submit" disabled={busy}>
                {t.sales.saveDraft}
              </button>
            )}
            {locked ? null : (
              <button className="primary-btn toolbar-btn" type="button" disabled={busy} onClick={() => setConfirmPost(true)}>
                {t.sales.post}
              </button>
            )}
            {posted && remainingTotal > 0 ? (
              <button
                className="primary-btn toolbar-btn"
                type="button"
                onClick={() => {
                  setPayAmount(String(remainingTotal))
                  setPayMethodId(methods[0]?.id ?? '')
                  setShowPay(true)
                }}
              >
                {t.sales.pay}
              </button>
            ) : null}
            {posted ? (
              <button type="button" className="btn-secondary" onClick={() => editingId && onNavigate(`/print/invoice/${editingId}`)}>
                {t.sales.print}
              </button>
            ) : editingId ? (
              <button type="button" className="btn-secondary" onClick={() => editingId && onNavigate(`/print/invoice/${editingId}`)}>
                {t.sales.print}
              </button>
            ) : null}
            {posted && canUnpost ? (
              <button type="button" className="btn-secondary" disabled={busy} onClick={() => setConfirmUnpost(true)}>
                {t.sales.unpost}
              </button>
            ) : null}
            {editingId && canDelete ? (
              <button type="button" className="btn-danger" disabled={busy} onClick={() => setConfirmDeleteId(editingId)}>
                {t.sales.delete}
              </button>
            ) : null}
          </div>
        </form>
      ) : null}

      {showPay ? (
        <form className="editor" onSubmit={(event) => void onPay(event)}>
          <h2 className="editor-title">{t.sales.pay}</h2>
          <div className="totals">
            <div>
              <span>{t.sales.goodsTotal}</span>
              <strong>{formatMoney(paidTotal + remainingTotal)}</strong>
            </div>
            <div>
              <span>{t.sales.paidTotal}</span>
              <strong>{formatMoney(paidTotal)}</strong>
            </div>
            <div>
              <span>{t.sales.remainingTotal}</span>
              <strong>{formatMoney(remainingTotal)}</strong>
            </div>
          </div>
          {methods.length === 0 ? <p className="muted">{t.sales.noMethods}</p> : null}
          <label className="field">
            <span>{t.sales.method}</span>
            <select value={payMethodId} onChange={(event) => setPayMethodId(event.target.value)} required>
              {methods.map((method) => (
                <option key={method.id} value={method.id}>
                  {method.name}
                </option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t.sales.amount}</span>
            <input value={payAmount} onChange={(event) => setPayAmount(event.target.value)} required />
          </label>
          {overpay ? <p className="banner error">{t.sales.overpay}</p> : null}
          <div className="action-row">
            <button className="primary-btn toolbar-btn" type="submit" disabled={busy || methods.length === 0 || overpay}>
              {t.sales.pay}
            </button>
            <button type="button" className="btn-secondary" onClick={() => setShowPay(false)}>
              {t.cancel}
            </button>
          </div>
        </form>
      ) : null}

      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault()
        }}
      >
        <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={t.sales.search} />
      </form>

      {visibleRows.length === 0 ? (
        <EmptyState
          title={unpaidOnly ? t.sales.unpaidEmpty : t.sales.empty}
          hint={unpaidOnly ? undefined : t.sales.emptyHint}
          actionLabel={unpaidOnly ? undefined : t.sales.add}
          onAction={unpaidOnly ? undefined : startCreate}
        />
      ) : (
        <div className="card-grid">
          {visibleRows.map((row) => (
            <article key={row.id} className="invoice-card">
              <div className="invoice-card-head">
                <div>
                  <h2 className="entity-card-title">{row.number ?? t.sales.numberPending}</h2>
                  <p className="muted">
                    {row.customerName} · {formatDate(row.invoiceDate)}
                  </p>
                </div>
                <div className="status-row">
                  <StatusBadge kind={row.status === 'posted' ? 'posted' : 'draft'}>
                    {row.status === 'posted' ? t.sales.posted : t.sales.draft}
                  </StatusBadge>
                  {row.status === 'posted' ? (
                    <StatusBadge kind={row.paymentStatus === 'paid' ? 'paid' : row.paymentStatus === 'partial' ? 'partial' : 'unpaid'}>
                      {paymentLabel(row.paymentStatus, t)}
                    </StatusBadge>
                  ) : null}
                </div>
              </div>
              <div className="invoice-card-meta">
                <span>
                  {t.sales.goodsTotal}
                  <strong>{formatMoney(row.goodsTotal)}</strong>
                </span>
                <span>
                  {t.sales.paidTotal}
                  <strong>{formatMoney(row.paidTotal)}</strong>
                </span>
                <span>
                  {t.sales.remainingTotal}
                  <strong className={row.remainingTotal > 0 ? 'money-warn' : 'money-ok'}>{formatMoney(row.remainingTotal)}</strong>
                </span>
              </div>
              <div className="invoice-card-actions">
                <button type="button" className="primary-btn toolbar-btn icon-btn" title={t.sales.view} onClick={() => void openEdit(row.id)}>
                  <Icon name="view" />
                  {t.sales.view}
                </button>
                {row.status === 'draft' ? (
                  <button type="button" className="btn-secondary toolbar-btn icon-btn" title={t.sales.edit} onClick={() => void openEdit(row.id)}>
                    <Icon name="edit" />
                    {t.sales.edit}
                  </button>
                ) : (
                  <button type="button" className="btn-secondary toolbar-btn icon-btn" title={t.sales.edit} onClick={() => void openEdit(row.id)}>
                    <Icon name="edit" />
                    {t.sales.edit}
                  </button>
                )}
                <button
                  type="button"
                  className="btn-secondary toolbar-btn icon-btn"
                  title={t.sales.print}
                  onClick={() => onNavigate(`/print/invoice/${row.id}`)}
                >
                  <Icon name="print" />
                  {t.sales.print}
                </button>
                {canDelete ? (
                  <button
                    type="button"
                    className="btn-danger toolbar-btn icon-btn"
                    title={t.sales.delete}
                    onClick={() => setConfirmDeleteId(row.id)}
                  >
                    {t.sales.delete}
                  </button>
                ) : null}
                {row.status === 'posted' && row.remainingTotal > 0 ? (
                  <button
                    type="button"
                    className="btn-secondary toolbar-btn icon-btn"
                    title={t.sales.pay}
                    onClick={() => {
                      void openEdit(row.id).then(() => {
                        setPayAmount(String(row.remainingTotal))
                        setPayMethodId(methods[0]?.id ?? '')
                        setShowPay(true)
                      })
                    }}
                  >
                    <Icon name="payment" />
                    {t.sales.pay}
                  </button>
                ) : null}
              </div>
            </article>
          ))}
        </div>
      )}

      {confirmPost ? (
        <ConfirmDialog
          title={t.sales.postConfirm}
          body={t.sales.postConfirmBody}
          confirmLabel={t.sales.post}
          onConfirm={() => void doPost()}
          onCancel={() => setConfirmPost(false)}
        />
      ) : null}
      {confirmUnpost ? (
        <ConfirmDialog
          title={t.sales.unpostConfirm}
          body={t.sales.unpostBody}
          confirmLabel={t.sales.unpost}
          onConfirm={() => void doUnpost()}
          onCancel={() => setConfirmUnpost(false)}
        />
      ) : null}
      {confirmDeleteId ? (
        <ConfirmDialog
          title={t.sales.deleteConfirm}
          body={t.sales.deleteBody}
          confirmLabel={t.sales.delete}
          onConfirm={() => void doDelete()}
          onCancel={() => setConfirmDeleteId(null)}
        />
      ) : null}
    </section>
  )
}
