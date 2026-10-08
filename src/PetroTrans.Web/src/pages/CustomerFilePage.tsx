import { Fragment, useEffect, useMemo, useState, type FormEvent } from 'react'
import type { AuthUser } from '../api/auth'
import {
  archiveCustomer,
  fetchCustomer,
  fetchCustomerTypes,
  restoreCustomer,
  updateCustomer,
  type CustomerType,
  type SaveCustomer,
} from '../api/catalog'
import { fetchInvoices, fetchInvoice, type Invoice, type InvoiceListItem } from '../api/sales'
import { createPayment, fetchPayment, fetchPaymentMethods, fetchPayments, fetchStatement, updatePayment, voidPayment, type PaymentDetail, type PaymentMethod, type PaymentRow, type Statement } from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner, StatusBadge } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, statusLabel, todayIso } from '../ui/format'
import { Icon } from '../ui/Icons'

type CustomerFilePageProps = {
  customerId: string
  user: AuthUser
  onNavigate: (path: string) => void
}

export function CustomerFilePage({ customerId, user, onNavigate }: CustomerFilePageProps) {
  const { t } = useLocale()
  const canVoid = user.permissions.includes('payments.void')
  const canPay = user.permissions.includes('payments.create')
  const canEdit = user.permissions.includes('customers.edit')
  const [isActive, setIsActive] = useState(true)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [archiveOpen, setArchiveOpen] = useState(false)
  const [name, setName] = useState('')
  const [code, setCode] = useState('')
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<SaveCustomer>({
    name: '',
    customerTypeId: null,
    contactPerson: '',
    phone: '',
    whatsApp: '',
    address: '',
  })
  const [types, setTypes] = useState<CustomerType[]>([])
  const [invoices, setInvoices] = useState<InvoiceListItem[]>([])
  const [invoiceDetails, setInvoiceDetails] = useState<Record<string, Invoice>>({})
  const [expandedInvoiceId, setExpandedInvoiceId] = useState<string | null>(null)
  const [payments, setPayments] = useState<PaymentRow[]>([])
  const [methods, setMethods] = useState<PaymentMethod[]>([])
  const [payAmount, setPayAmount] = useState('')
  const [payDate, setPayDate] = useState(todayIso())
  const [payMethodId, setPayMethodId] = useState('')
  const [statement, setStatement] = useState<Statement | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [voidId, setVoidId] = useState<string | null>(null)
  const [allocAmounts, setAllocAmounts] = useState<Record<string, string>>({})
  const [allocDirty, setAllocDirty] = useState(false)
  const [editingPayment, setEditingPayment] = useState<PaymentDetail | null>(null)

  async function refresh() {
    const [customer, typeList, invoiceList, paymentList, stmt, methodList] = await Promise.all([
      fetchCustomer(customerId),
      fetchCustomerTypes(true),
      fetchInvoices('', customerId),
      fetchPayments(customerId),
      fetchStatement(customerId, from || undefined, to || undefined),
      fetchPaymentMethods(true),
    ])
    setCode(customer.code)
    setName(customer.name)
    setIsActive(customer.isActive)
    setForm({
      name: customer.name,
      customerTypeId: customer.customerTypeId,
      contactPerson: customer.contactPerson ?? '',
      phone: customer.phone ?? '',
      whatsApp: customer.whatsApp ?? '',
      address: customer.address ?? '',
    })
    setTypes(typeList)
    setInvoices(invoiceList)
    setPayments(paymentList)
    setStatement(stmt)
    setMethods(methodList)
    setPayMethodId((current) => current || methodList[0]?.id || '')
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [customerId])

  const overview = useMemo(() => {
    const posted = invoices.filter((row) => row.status === 'posted')
    const sales = posted.reduce((sum, row) => sum + row.goodsTotal, 0)
    const paid = payments.reduce((sum, row) => sum + row.amount, 0)
    return {
      sales,
      paid,
      invoiceCount: invoices.length,
      lastDate: invoices[0]?.invoiceDate ?? null,
    }
  }, [invoices, payments])

  const outstandingInvoices = useMemo(
    () =>
      [...invoices]
        .filter((row) => {
          if (row.status !== 'posted') return false
          if (!editingPayment) return row.remainingTotal > 0
          const already = editingPayment.allocations?.find((item) => item.invoiceId === row.id)?.amount ?? 0
          return row.remainingTotal + already > 0
        })
        .sort((a, b) => {
          const byDate = new Date(a.invoiceDate).getTime() - new Date(b.invoiceDate).getTime()
          if (byDate !== 0) return byDate
          return (a.number ?? a.id).localeCompare(b.number ?? b.id, 'ar')
        }),
    [invoices, editingPayment],
  )

  function openRemaining(row: InvoiceListItem) {
    if (!editingPayment) {
      return row.remainingTotal
    }
    return row.remainingTotal + (editingPayment.allocations?.find((item) => item.invoiceId === row.id)?.amount ?? 0)
  }

  function applyFifo(amount: number, rows: InvoiceListItem[]) {
    let left = amount
    const next: Record<string, string> = {}
    for (const row of rows) {
      if (left <= 0) break
      const open = openRemaining(row)
      if (open <= 0) continue
      const take = Math.min(left, open)
      next[row.id] = String(take)
      left -= take
    }
    setAllocAmounts(next)
  }

  useEffect(() => {
    if (allocDirty) return
    const amount = parseAmount(payAmount) ?? 0
    if (amount <= 0) {
      setAllocAmounts({})
      return
    }
    applyFifo(amount, outstandingInvoices)
  }, [payAmount, outstandingInvoices, allocDirty])

  const historyInvoices = useMemo(() => {
    return [...invoices].sort((a, b) => {
      const byDate = new Date(a.invoiceDate).getTime() - new Date(b.invoiceDate).getTime()
      if (byDate !== 0) return byDate
      return (a.number ?? a.id).localeCompare(b.number ?? b.id, 'ar')
    })
  }, [invoices])

  const runningByDocument = useMemo(() => {
    const map = new Map<string, number>()
    for (const line of statement?.lines ?? []) {
      map.set(line.sourceDocumentId, line.runningBalance)
    }
    return map
  }, [statement])

  const timeline = useMemo(() => {
    const items = [
      ...invoices.map((row) => ({
        id: `inv-${row.id}`,
        invoiceId: row.id,
        at: row.invoiceDate,
        kind: 'invoice' as const,
        title: row.number ?? t.sales.numberPending,
        amount: row.goodsTotal,
        paid: row.paidTotal,
        remaining: row.remainingTotal,
        meta: statusLabel(row.status),
      })),
      ...payments.map((row) => ({
        id: `pay-${row.id}`,
        invoiceId: null as string | null,
        at: row.paidOn,
        kind: 'payment' as const,
        title: row.invoiceNumber ?? t.customers.onAccount,
        amount: row.amount,
        paid: row.amount,
        remaining: 0,
        meta: row.methodName,
      })),
    ]
    return items.sort((a, b) => {
      const byDate = new Date(a.at).getTime() - new Date(b.at).getTime()
      if (byDate !== 0) return byDate
      return a.id.localeCompare(b.id)
    })
  }, [invoices, payments, t])

  async function toggleInvoiceDetails(id: string) {
    setExpandedInvoiceId((current) => (current === id ? null : id))
    if (invoiceDetails[id]) return
    try {
      const detail = await fetchInvoice(id)
      setInvoiceDetails((current) => ({ ...current, [id]: detail }))
    } catch (err) {
      setError(err)
    }
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await updateCustomer(customerId, form)
      setEditing(false)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onPayOnAccount(event: FormEvent) {
    event.preventDefault()
    const amount = parseAmount(payAmount) ?? 0
    if (amount <= 0 || !payMethodId) {
      return
    }
    const allocations = Object.entries(allocAmounts)
      .map(([invoiceId, value]) => ({ invoiceId, amount: parseAmount(value) ?? 0 }))
      .filter((row) => row.amount > 0)
    setBusy(true)
    setError(null)
    try {
      if (editingPayment) {
        await updatePayment(editingPayment.id, {
          customerId,
          paymentMethodId: payMethodId,
          amount,
          paidOn: payDate || todayIso(),
          reference: editingPayment.reference,
          notes: editingPayment.notes,
          allocations,
        })
        setEditingPayment(null)
      } else {
        await createPayment({
          invoiceId: null,
          customerId,
          paymentMethodId: payMethodId,
          amount,
          paidOn: payDate || todayIso(),
          reference: null,
          notes: null,
          allocations,
        })
      }
      setPayAmount('')
      setAllocAmounts({})
      setAllocDirty(false)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function openPayment(id: string) {
    setBusy(true)
    setError(null)
    try {
      const payment = await fetchPayment(id)
      setEditingPayment(payment)
      setPayAmount(String(payment.amount))
      setPayDate(payment.paidOn.slice(0, 10))
      setPayMethodId(payment.paymentMethodId)
      const next: Record<string, string> = {}
      for (const row of payment.allocations ?? []) {
        next[row.invoiceId] = String(row.amount)
      }
      setAllocAmounts(next)
      setAllocDirty(true)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onVoid(id: string) {
    setBusy(true)
    setError(null)
    setVoidId(null)
    try {
      await voidPayment(id)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onArchiveToggle() {
    setBusy(true)
    setError(null)
    setArchiveOpen(false)
    try {
      if (isActive) await archiveCustomer(customerId)
      else await restoreCustomer(customerId)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <p className="breadcrumb">
            <button type="button" className="link-btn" onClick={() => onNavigate('/customers')}>
              {t.nav.customers}
            </button>
            {' / '}
            {name || '…'}
          </p>
          <h1 className="page-title">{name || t.customers.file}</h1>
          <p className="muted">
            {code}
            {form.phone ? ` · ${form.phone}` : ''}
            {form.address ? ` · ${form.address}` : ''}
          </p>
        </div>
        <div className="action-row">
          <button type="button" className="btn-secondary icon-btn" onClick={() => onNavigate('/customers')}>
            <Icon name="back" />
            {t.customers.back}
          </button>
          <button type="button" className="btn-secondary icon-btn" onClick={() => setEditing((value) => !value)}>
            <Icon name="edit" />
            {t.customers.edit}
          </button>
          <button type="button" className="primary-btn icon-btn" onClick={() => onNavigate(`/sales?customerId=${customerId}`)}>
            <Icon name="add" />
            {t.customers.newInvoice}
          </button>
          <button type="button" className="btn-secondary icon-btn" onClick={() => onNavigate(`/print/statement/${customerId}${from || to ? `?from=${from}&to=${to}` : ''}`)}>
            <Icon name="print" />
            {t.customers.printStatement}
          </button>
          {canEdit ? (
            <button type="button" className="btn-secondary" onClick={() => setArchiveOpen(true)}>
              {isActive ? t.customers.archive : t.customers.restore}
            </button>
          ) : null}
        </div>
      </div>

      <ErrorBanner error={error} onRetry={() => void refresh()} />

      <div className="profile-kpi-grid">
        <div className="kpi-card">
          <span className="kpi-label">{t.ops.customerRemaining}</span>
          <span className="kpi-value">{formatMoney(statement?.outstanding ?? 0)}</span>
        </div>
        <div className="kpi-card">
          <span className="kpi-label">{t.customers.totalSales}</span>
          <span className="kpi-value">{formatMoney(overview.sales)}</span>
        </div>
        <div className="kpi-card">
          <span className="kpi-label">{t.customers.totalPaid}</span>
          <span className="kpi-value">{formatMoney(overview.paid)}</span>
        </div>
        <div className="kpi-card">
          <span className="kpi-label">{t.customers.invoiceCount}</span>
          <span className="kpi-value">{overview.invoiceCount}</span>
        </div>
      </div>

      {editing ? (
        <form className="editor panel-card" onSubmit={(event) => void onSave(event)}>
          <h2 className="editor-title">{t.customers.edit}</h2>
          <div className="form-section">
            <h3 className="section-title">{t.customers.basicInfo}</h3>
            <label className="field">
              <span>{t.customers.code}</span>
              <input value={code} readOnly />
            </label>
            <label className="field">
              <span>{t.customers.name}</span>
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required />
            </label>
            {types.length > 0 ? (
              <label className="field">
                <span>{t.customers.type}</span>
                <select
                  value={form.customerTypeId ?? ''}
                  onChange={(event) => setForm({ ...form, customerTypeId: event.target.value || null })}
                >
                  <option value="">{t.customers.noType}</option>
                  {types.map((type) => (
                    <option key={type.id} value={type.id}>
                      {type.name}
                    </option>
                  ))}
                </select>
              </label>
            ) : null}
            <div className="form-grid">
              <label className="field">
                <span>{t.customers.phone}</span>
                <input value={form.phone} onChange={(event) => setForm({ ...form, phone: event.target.value })} />
              </label>
              <label className="field">
                <span>{t.customers.whatsapp}</span>
                <input value={form.whatsApp} onChange={(event) => setForm({ ...form, whatsApp: event.target.value })} />
              </label>
              <label className="field">
                <span>{t.customers.contact}</span>
                <input value={form.contactPerson} onChange={(event) => setForm({ ...form, contactPerson: event.target.value })} />
              </label>
            </div>
            <label className="field">
              <span>{t.customers.address}</span>
              <input value={form.address} onChange={(event) => setForm({ ...form, address: event.target.value })} />
            </label>
          </div>
          <div className="action-row">
            <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
              {busy ? t.loading : t.save}
            </button>
            <button type="button" className="btn-secondary" onClick={() => setEditing(false)}>
              {t.cancel}
            </button>
          </div>
        </form>
      ) : null}

      <section className="panel-card">
        <div className="page-head">
          <h2 className="editor-title">{t.customers.history}</h2>
        </div>
        {historyInvoices.length === 0 ? (
          <EmptyState title={t.customers.emptyInvoices} />
        ) : (
          <div className="history-scroll">
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.sales.date}</th>
                  <th>{t.sales.number}</th>
                  <th>{t.sales.goodsTotal}</th>
                  <th>{t.sales.paidTotal}</th>
                  <th>{t.sales.remainingTotal}</th>
                  <th>{t.customers.running}</th>
                  <th>{t.sales.documentStatus}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {historyInvoices.map((row) => {
                  const detail = invoiceDetails[row.id]
                  const expanded = expandedInvoiceId === row.id
                  return (
                    <Fragment key={row.id}>
                      <tr className="clickable" onClick={() => void toggleInvoiceDetails(row.id)}>
                        <td>{formatDate(row.invoiceDate)}</td>
                        <td>{row.number ?? t.sales.numberPending}</td>
                        <td>{formatMoney(row.goodsTotal)}</td>
                        <td>{formatMoney(row.paidTotal)}</td>
                        <td>{formatMoney(row.remainingTotal)}</td>
                        <td>{runningByDocument.has(row.id) ? formatMoney(runningByDocument.get(row.id)) : '—'}</td>
                        <td>
                          <StatusBadge kind={row.status === 'posted' ? 'posted' : 'draft'}>
                            {row.status === 'posted' ? t.sales.posted : t.sales.draft}
                          </StatusBadge>{' '}
                          {statusLabel(row.paymentStatus)}
                        </td>
                        <td>
                          <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); void toggleInvoiceDetails(row.id) }}>
                            {expanded ? t.customers.hideDetails : t.customers.showDetails}
                          </button>
                          <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); onNavigate(`/sales?id=${row.id}`) }}>
                            {t.customers.openInvoice}
                          </button>
                        </td>
                      </tr>
                      {expanded ? (
                        <tr className="invoice-detail-row">
                          <td colSpan={8}>
                            {detail ? (
                              <div className="invoice-detail">
                                <table className="data-table compact">
                                  <thead>
                                    <tr>
                                      <th>{t.sales.product}</th>
                                      <th>{t.products.packagingType}</th>
                                      <th>{t.sales.quantity}</th>
                                      <th>{t.sales.unitPrice}</th>
                                      <th>{t.sales.lineTotal}</th>
                                    </tr>
                                  </thead>
                                  <tbody>
                                    {detail.lines.map((line) => (
                                      <tr key={line.id}>
                                        <td>{line.productName}</td>
                                        <td>{line.packagingType} {line.packagingSize}</td>
                                        <td>{line.quantity}</td>
                                        <td>{formatMoney(line.unitPrice ?? line.resolvedUnitPrice)}</td>
                                        <td>{formatMoney(line.lineTotal)}</td>
                                      </tr>
                                    ))}
                                  </tbody>
                                </table>
                                <p className="muted">
                                  {t.sales.goodsTotal}: {formatMoney(detail.goodsTotal)}
                                  {detail.notes ? ` — ${detail.notes}` : ''}
                                </p>
                              </div>
                            ) : (
                              <p className="muted">{t.loading}</p>
                            )}
                          </td>
                        </tr>
                      ) : null}
                    </Fragment>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel-card">
        <div className="page-head">
          <h2 className="editor-title">{t.customers.paymentsSection}</h2>
        </div>
        {canPay ? (
          <form className="toolbar" onSubmit={(event) => void onPayOnAccount(event)}>
            <input type="date" value={payDate} onChange={(event) => setPayDate(event.target.value)} required />
            <select value={payMethodId} onChange={(event) => setPayMethodId(event.target.value)} required>
              {methods.map((method) => (
                <option key={method.id} value={method.id}>
                  {method.name}
                </option>
              ))}
            </select>
            <input
              value={payAmount}
              onChange={(event) => {
                setPayAmount(event.target.value)
                setAllocDirty(false)
              }}
              placeholder={t.sales.amount}
              required
            />
            <button className="primary-btn toolbar-btn" type="submit" disabled={busy || methods.length === 0}>
              {editingPayment ? t.customers.editPayment : t.customers.recordPayment}
            </button>
            {editingPayment ? (
              <button
                type="button"
                className="btn-secondary"
                onClick={() => {
                  setEditingPayment(null)
                  setPayAmount('')
                  setAllocAmounts({})
                  setAllocDirty(false)
                }}
              >
                {t.cancel}
              </button>
            ) : null}
          </form>
        ) : null}
        {canPay && methods.length === 0 ? <p className="muted">{t.sales.noMethods}</p> : null}
        {canPay && outstandingInvoices.length > 0 ? (
          <div>
            <p className="muted">{t.customers.fifoHint}</p>
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.sales.number}</th>
                  <th>{t.sales.date}</th>
                  <th>{t.sales.remainingTotal}</th>
                  <th>{t.customers.allocate}</th>
                </tr>
              </thead>
              <tbody>
                {outstandingInvoices.map((row) => (
                  <tr key={row.id}>
                    <td>{row.number ?? t.sales.numberPending}</td>
                    <td>{formatDate(row.invoiceDate)}</td>
                    <td>{formatMoney(openRemaining(row))}</td>
                    <td>
                      <input
                        value={allocAmounts[row.id] ?? ''}
                        onChange={(event) => {
                          setAllocDirty(true)
                          setAllocAmounts((current) => ({ ...current, [row.id]: event.target.value }))
                        }}
                        placeholder="0"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {(() => {
              const allocated = Object.values(allocAmounts).reduce((sum, value) => sum + (parseAmount(value) ?? 0), 0)
              const cash = parseAmount(payAmount) ?? 0
              const leftover = cash - allocated
              return leftover > 0 ? <p className="muted">{t.customers.unallocated}: {formatMoney(leftover)}</p> : null
            })()}
          </div>
        ) : null}
        {payments.length === 0 ? (
          <EmptyState title={t.customers.emptyPayments} />
        ) : (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.ops.payDate}</th>
                <th>{t.sales.number}</th>
                <th>{t.sales.amount}</th>
                <th>{t.sales.method}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {payments.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.paidOn)}</td>
                  <td>{row.invoiceNumber ?? t.customers.onAccount}</td>
                  <td>{formatMoney(row.amount)}</td>
                  <td>
                    {row.methodName}
                    {row.reference ? ` — ${row.reference}` : ''}
                  </td>
                  <td>
                    <button type="button" className="link-btn" onClick={() => void openPayment(row.id)}>
                      {t.customers.editPayment}
                    </button>
                    <button type="button" className="link-btn" onClick={() => onNavigate(`/print/payment/${row.id}`)}>
                      {t.sales.print}
                    </button>
                    {canVoid ? (
                      <button type="button" className="link-btn" disabled={busy} onClick={() => setVoidId(row.id)}>
                        {t.customers.voidPay}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="panel-card">
        <div className="page-head">
          <h2 className="editor-title">{t.customers.statement}</h2>
          <form
            className="toolbar"
            onSubmit={(event) => {
              event.preventDefault()
              void refresh().catch((err: unknown) => setError(err))
            }}
          >
            <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
            <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
            <button className="btn-secondary" type="submit">{t.ops.reports}</button>
          </form>
        </div>
        {statement ? (
          <>
            <div className="totals">
              <div>
                <span>{t.customers.opening}</span>
                <strong>{formatMoney(statement.openingBalance)}</strong>
              </div>
              <div>
                <span>{t.customers.debit}</span>
                <strong>{formatMoney(statement.totalDebits)}</strong>
              </div>
              <div>
                <span>{t.customers.credit}</span>
                <strong>{formatMoney(statement.totalCredits)}</strong>
              </div>
              <div>
                <span>{t.customers.closing}</span>
                <strong>{formatMoney(statement.closingBalance)}</strong>
              </div>
            </div>
            {statement.lines.length === 0 ? (
              <EmptyState title={t.customers.emptyActivity} />
            ) : (
              <table className="data-table compact">
                <thead>
                  <tr>
                    <th>{t.sales.date}</th>
                    <th>{t.customers.document}</th>
                    <th>{t.customers.description}</th>
                    <th>{t.customers.debit}</th>
                    <th>{t.customers.credit}</th>
                    <th>{t.customers.running}</th>
                  </tr>
                </thead>
                <tbody>
                  {statement.lines.map((line) => (
                    <tr key={`${line.sourceDocumentId}-${line.occurredAt}`}>
                      <td>{formatDate(line.occurredAt)}</td>
                      <td>{line.document}</td>
                      <td>{line.description}</td>
                      <td>{formatMoney(line.debit)}</td>
                      <td>{formatMoney(line.credit)}</td>
                      <td>{formatMoney(line.runningBalance)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        ) : (
          <p className="muted">{t.loading}</p>
        )}
      </section>

      <section className="panel-card">
        <h2 className="editor-title">{t.customers.activity}</h2>
        {timeline.length === 0 ? (
          <EmptyState title={t.customers.emptyActivity} />
        ) : (
          <ul className="timeline history-scroll">
            {timeline.map((item) => (
              <li key={item.id} className="timeline-item">
                <span className="timeline-dot" />
                <div>
                  <strong>
                    {item.kind === 'invoice' ? t.customers.timelineInvoice : t.customers.timelinePayment}: {item.title}
                  </strong>
                  <p className="muted">
                    {formatDate(item.at)} · {item.meta} · {formatMoney(item.amount)}
                    {item.kind === 'invoice' ? ` · ${t.sales.paidTotal} ${formatMoney(item.paid)} · ${t.sales.remainingTotal} ${formatMoney(item.remaining)}` : ''}
                    {item.invoiceId && runningByDocument.has(item.invoiceId) ? ` · ${t.customers.running} ${formatMoney(runningByDocument.get(item.invoiceId))}` : ''}
                  </p>
                  {item.kind === 'invoice' && item.invoiceId ? (
                    <button type="button" className="link-btn" onClick={() => void toggleInvoiceDetails(item.invoiceId!)}>
                      {expandedInvoiceId === item.invoiceId ? t.customers.hideDetails : t.customers.showDetails}
                    </button>
                  ) : null}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {voidId ? (
        <ConfirmDialog
          title={t.customers.voidPay}
          body={t.customers.voidPayConfirm}
          confirmLabel={t.customers.voidPay}
          onConfirm={() => void onVoid(voidId)}
          onCancel={() => setVoidId(null)}
        />
      ) : null}
      {archiveOpen ? (
        <ConfirmDialog
          title={isActive ? t.customers.archiveConfirm : t.customers.restore}
          body={t.customers.archiveBody}
          confirmLabel={isActive ? t.customers.archive : t.customers.restore}
          onConfirm={() => void onArchiveToggle()}
          onCancel={() => setArchiveOpen(false)}
        />
      ) : null}
    </section>
  )
}
