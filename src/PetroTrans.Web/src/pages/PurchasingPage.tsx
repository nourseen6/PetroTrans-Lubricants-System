import { useEffect, useState, type FormEvent } from 'react'
import { searchVariants, type VariantPick } from '../api/catalog'
import {
  createSupplierPayment,
  deleteBill,
  deleteSupplierPayment,
  fetchActiveWarehouses,
  fetchBill,
  fetchBills,
  fetchPaymentMethods,
  fetchSupplierPayments,
  fetchSupplierStatement,
  fetchSuppliers,
  postBill,
  saveBill,
  saveSupplier,
  unpostBill,
  type NamedLookup,
  type PaymentMethod,
  type PurchaseBill,
  type Supplier,
  type SupplierPaymentRow,
  type SupplierStatement,
} from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner, StatusBadge } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, todayIso } from '../ui/format'
import { Typeahead } from '../ui/Typeahead'

type PurchasingSection = 'bills' | 'payments' | 'suppliers' | 'statement'

type DraftLine = {
  key: string
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  quantity: string
  unitPrice: string
}

function newKey(): string {
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function lineFromVariant(item: VariantPick, qty: string, price: string): DraftLine {
  return {
    key: newKey(),
    variantId: item.id,
    productName: item.productName,
    packagingType: item.packagingType,
    packagingSize: item.packagingSize,
    quantity: qty,
    unitPrice: price,
  }
}

function toSaveLines(lines: DraftLine[]) {
  return lines
    .map((line) => ({
      variantId: line.variantId,
      quantity: parseAmount(line.quantity) ?? 0,
      unitPrice: parseAmount(line.unitPrice) ?? 0,
    }))
    .filter((line) => line.variantId && line.quantity > 0 && line.unitPrice >= 0)
}

export function PurchasingPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { t } = useLocale()
  const [section, setSection] = useState<PurchasingSection>('bills')
  const [error, setError] = useState<unknown>(null)
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [warehouses, setWarehouses] = useState<NamedLookup[]>([])
  const [variants, setVariants] = useState<VariantPick[]>([])
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [warehouseId, setWarehouseId] = useState('')
  const [variantId, setVariantId] = useState('')
  const [pendingVariant, setPendingVariant] = useState<VariantPick | null>(null)
  const [productQuery, setProductQuery] = useState('')
  const [qty, setQty] = useState('1')
  const [price, setPrice] = useState('')
  const [draftLines, setDraftLines] = useState<DraftLine[]>([])
  const [newBillDate, setNewBillDate] = useState(todayIso)
  const [bills, setBills] = useState<Array<{ id: string; number: string | null; invoiceDate: string; supplierName: string; goodsTotal: number; status: string }>>([])
  const [openBill, setOpenBill] = useState<PurchaseBill | null>(null)
  const [editingBill, setEditingBill] = useState(false)
  const [editLines, setEditLines] = useState<DraftLine[]>([])
  const [confirmDeleteBill, setConfirmDeleteBill] = useState(false)
  const [deletePaymentId, setDeletePaymentId] = useState<string | null>(null)
  const [billDate, setBillDate] = useState('')
  const [billNotes, setBillNotes] = useState('')
  const [methods, setMethods] = useState<PaymentMethod[]>([])
  const [supplierPayments, setSupplierPayments] = useState<SupplierPaymentRow[]>([])
  const [payAmount, setPayAmount] = useState('')
  const [payDate, setPayDate] = useState(todayIso)
  const [payMethodId, setPayMethodId] = useState('')
  const [payReference, setPayReference] = useState('')
  const [statement, setStatement] = useState<SupplierStatement | null>(null)
  const [stmtFrom, setStmtFrom] = useState('')
  const [stmtTo, setStmtTo] = useState('')

  async function refresh() {
    const [supplierList, warehouseList, variantList, billList, methodList, paymentList] = await Promise.all([
      fetchSuppliers(''),
      fetchActiveWarehouses(),
      searchVariants(''),
      fetchBills(),
      fetchPaymentMethods(true),
      fetchSupplierPayments(),
    ])
    setSuppliers(supplierList)
    setWarehouses(warehouseList)
    setVariants(variantList)
    setBills(billList)
    setMethods(methodList)
    setSupplierPayments(paymentList)
    setSupplierId((current) => current || supplierList[0]?.id || '')
    setWarehouseId((current) => current || warehouseList[0]?.id || '')
    setPayMethodId((current) => current || methodList[0]?.id || '')
    const sid = supplierId || supplierList[0]?.id || ''
    if (section === 'statement' && sid) {
      setStatement(await fetchSupplierStatement(sid, stmtFrom || undefined, stmtTo || undefined))
    }
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void searchVariants(productQuery).then(setVariants).catch(() => setVariants([]))
    }, 180)
    return () => window.clearTimeout(handle)
  }, [productQuery])

  useEffect(() => {
    if (section !== 'statement' || !supplierId) {
      return
    }
    void fetchSupplierStatement(supplierId, stmtFrom || undefined, stmtTo || undefined)
      .then(setStatement)
      .catch((err: unknown) => setError(err))
  }, [section, supplierId])

  async function run(event: FormEvent, action: () => Promise<unknown>) {
    event.preventDefault()
    setError(null)
    try {
      await action()
      await refresh()
      if (openBill) {
        try {
          setOpenBill(await fetchBill(openBill.id))
        } catch {
          setOpenBill(null)
        }
      }
    } catch (err) {
      setError(err)
    }
  }

  function linesFromBill(bill: PurchaseBill): DraftLine[] {
    return bill.lines.map((line) => ({
      key: newKey(),
      variantId: line.variantId,
      productName: line.productName,
      packagingType: line.packagingType,
      packagingSize: line.packagingSize,
      quantity: String(line.quantity),
      unitPrice: String(line.unitPrice),
    }))
  }

  async function openBillDoc(id: string) {
    setError(null)
    try {
      setSection('bills')
      const bill = await fetchBill(id)
      setOpenBill(bill)
      setEditingBill(bill.status === 'draft')
      setEditLines(linesFromBill(bill))
      setBillDate(bill.invoiceDate.slice(0, 10))
      setBillNotes(bill.notes ?? '')
    } catch (err) {
      setError(err)
    }
  }

  function addPendingLine(target: 'draft' | 'edit') {
    const item = pendingVariant ?? variants.find((row) => row.id === variantId)
    const quantity = parseAmount(qty)
    const unitPrice = parseAmount(price)
    if (!item || quantity == null || quantity <= 0 || unitPrice == null || unitPrice < 0) {
      return
    }
    const next = lineFromVariant(item, String(quantity), String(unitPrice))
    if (target === 'edit') {
      setEditLines((current) => [...current, next])
    } else {
      setDraftLines((current) => [...current, next])
    }
    setPendingVariant(null)
    setVariantId('')
    setProductQuery('')
    setQty('1')
    setPrice('')
  }

  const selectedSupplier = suppliers.find((row) => row.id === supplierId)
  const payable = selectedSupplier?.outstanding ?? 0
  const payValue = Number(payAmount)
  const overpay = Number.isFinite(payValue) && payAmount.trim() !== '' && payValue > payable
  const supplierName = selectedSupplier?.name
  const visibleBills = supplierName ? bills.filter((row) => row.supplierName === supplierName) : bills
  const visiblePayments = supplierName ? supplierPayments.filter((row) => row.supplierName === supplierName) : supplierPayments
  const draftTotal = toSaveLines(draftLines).reduce((sum, line) => sum + line.quantity * line.unitPrice, 0)

  function sectionBtn(id: PurchasingSection, label: string) {
    return (
      <button type="button" className={section === id ? 'primary-btn' : 'btn-secondary'} onClick={() => {
        setOpenBill(null)
        setEditingBill(false)
        setSection(id)
      }}>{label}</button>
    )
  }

  function productPicker() {
    return (
      <Typeahead
        items={variants.filter((row) => row.isActive)}
        query={productQuery}
        onQuery={(value) => {
          setProductQuery(value)
          setVariantId('')
          setPendingVariant(null)
        }}
        placeholder={t.sales.searchProduct}
        itemKey={(item) => item.id}
        onSelect={(item) => {
          setVariantId(item.id)
          setPendingVariant(item)
          setProductQuery(`${item.productName} — ${item.packagingType} ${item.packagingSize}`)
          if (item.standardPurchasePrice != null) setPrice(String(item.standardPurchasePrice))
        }}
        renderItem={(item) => (
          <span>
            <strong>{item.productName}</strong>
            <small>{item.packagingType} {item.packagingSize}</small>
          </span>
        )}
      />
    )
  }

  function linesTable(lines: DraftLine[], onChange: (next: DraftLine[]) => void, locked: boolean) {
    return (
      <table className="data-table compact">
        <thead>
          <tr>
            <th>{t.sales.product}</th>
            <th>{t.sales.quantity}</th>
            <th>{t.ops.companyPrice}</th>
            <th>{t.sales.lineTotal}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {lines.length === 0 ? (
            <tr>
              <td colSpan={5}>{t.sales.noLines}</td>
            </tr>
          ) : lines.map((line) => {
            const quantity = parseAmount(line.quantity)
            const unitPrice = parseAmount(line.unitPrice)
            const total = quantity != null && unitPrice != null ? quantity * unitPrice : null
            return (
              <tr key={line.key}>
                <td>
                  {line.productName}
                  <div className="muted">{line.packagingType} {line.packagingSize}</div>
                </td>
                <td>
                  <input
                    value={line.quantity}
                    onChange={(event) => onChange(lines.map((row) => row.key === line.key ? { ...row, quantity: event.target.value } : row))}
                    inputMode="decimal"
                    disabled={locked}
                  />
                </td>
                <td>
                  <input
                    value={line.unitPrice}
                    onChange={(event) => onChange(lines.map((row) => row.key === line.key ? { ...row, unitPrice: event.target.value } : row))}
                    inputMode="decimal"
                    disabled={locked}
                  />
                </td>
                <td>{formatMoney(total)}</td>
                <td>
                  {locked ? null : (
                    <button type="button" className="btn-ghost" onClick={() => onChange(lines.filter((row) => row.key !== line.key))}>
                      {t.sales.remove}
                    </button>
                  )}
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    )
  }

  return (
    <section>
      <div className="page-head">
        <div>
          <h1 className="page-title">{t.nav.purchasing}</h1>
          <p className="muted">{t.ops.purchasePageHint}</p>
        </div>
        {selectedSupplier ? (
          <div className="kpi-card">
            <span className="kpi-label">{t.ops.supplierPayable}</span>
            <span className="kpi-value">{formatMoney(payable)}</span>
          </div>
        ) : null}
      </div>
      {suppliers.length > 0 ? (
        <div className="toolbar">
          <select value={supplierId} onChange={(event) => setSupplierId(event.target.value)} aria-label={t.ops.suppliers}>
            {suppliers.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}
          </select>
        </div>
      ) : null}
      <div className="action-row">
        {sectionBtn('bills', t.ops.bills)}
        {sectionBtn('payments', t.ops.supplierPay)}
        {sectionBtn('statement', t.ops.supplierStatement)}
        {sectionBtn('suppliers', t.ops.suppliers)}
      </div>
      <ErrorBanner error={error} onRetry={() => void refresh()} />

      {openBill ? (
        <section className="panel-card">
          <div className="page-head">
            <h2 className="editor-title">{t.ops.documentDetails} — {t.ops.bills}</h2>
            <div className="action-row">
              <button type="button" className="btn-secondary" onClick={() => onNavigate(`/print/bill/${openBill.id}`)}>{t.ops.print}</button>
              {openBill.status === 'posted' ? (
                <button type="button" className="btn-secondary" onClick={() => void run({ preventDefault() {} } as FormEvent, async () => {
                  const draft = await unpostBill(openBill.id)
                  setOpenBill(draft)
                  setEditingBill(true)
                  setEditLines(linesFromBill(draft))
                  setBillDate(draft.invoiceDate.slice(0, 10))
                  setBillNotes(draft.notes ?? '')
                })}>{t.ops.unpostBill}</button>
              ) : (
                <button type="button" className="btn-secondary" onClick={() => { setEditingBill(true); setEditLines(linesFromBill(openBill)) }}>{t.ops.editBill}</button>
              )}
              <button type="button" className="btn-danger" onClick={() => setConfirmDeleteBill(true)}>{t.ops.deleteBill}</button>
              <button type="button" className="btn-secondary" onClick={() => { setOpenBill(null); setEditingBill(false) }}>{t.ops.closeDocument}</button>
            </div>
          </div>
          <p>{t.ops.suppliers}: {openBill.supplierName}</p>
          <p>{t.ops.documentNumber}: {openBill.number ?? '—'}</p>
          {editingBill && openBill.status === 'draft' ? (
            <label className="field">
              <span>{t.ops.documentDate}</span>
              <input type="date" value={billDate} onChange={(event) => setBillDate(event.target.value)} />
            </label>
          ) : (
            <p>{t.ops.documentDate}: {formatDate(openBill.invoiceDate)}</p>
          )}
          <p>{t.sales.documentStatus}: {openBill.status === 'posted' ? t.sales.posted : openBill.status === 'draft' ? t.sales.draft : openBill.status}</p>
          {editingBill && openBill.status === 'draft' ? (
            <div className="line-entry">
              {productPicker()}
              <input className="amount-input" value={qty} onChange={(event) => setQty(event.target.value)} placeholder={t.sales.quantity} />
              <input className="amount-input" value={price} onChange={(event) => setPrice(event.target.value)} placeholder={t.ops.companyPrice} />
              <button type="button" className="primary-btn toolbar-btn" onClick={() => addPendingLine('edit')}>{t.sales.addLine}</button>
            </div>
          ) : null}
          {editingBill && openBill.status === 'draft'
            ? linesTable(editLines, setEditLines, false)
            : (
              <table className="data-table compact">
                <thead>
                  <tr>
                    <th>{t.sales.product}</th>
                    <th>{t.products.packagingType}</th>
                    <th>{t.sales.quantity}</th>
                    <th>{t.ops.companyPrice}</th>
                    <th>{t.sales.lineTotal}</th>
                  </tr>
                </thead>
                <tbody>
                  {openBill.lines.map((line, index) => (
                    <tr key={`${line.variantId}-${index}`}>
                      <td>{line.productName}</td>
                      <td>{line.packagingType} {line.packagingSize}</td>
                      <td>{line.quantity}</td>
                      <td>{formatMoney(line.unitPrice)}</td>
                      <td>{formatMoney(line.lineTotal)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          <div className="totals">
            <div>
              <span>{t.sales.linesSubtotal}</span>
              <strong>{formatMoney(openBill.linesSubtotal ?? openBill.lines.reduce((sum, line) => sum + line.lineTotal, 0))}</strong>
            </div>
            {(openBill.linesSubtotal ?? openBill.lines.reduce((sum, line) => sum + line.lineTotal, 0)) !== openBill.goodsTotal ? (
              <div>
                <span>{t.ops.settlement}</span>
                <strong>{formatMoney(openBill.goodsTotal - (openBill.linesSubtotal ?? openBill.lines.reduce((sum, line) => sum + line.lineTotal, 0)))}</strong>
              </div>
            ) : null}
            <div>
              <span>{t.sales.goodsTotal}</span>
              <strong>{formatMoney(openBill.goodsTotal)}</strong>
            </div>
            <div>
              <span>{t.ops.paidAmount}</span>
              <strong>{formatMoney(openBill.paidTotal ?? 0)}</strong>
            </div>
            <div>
              <span>{t.ops.remainingAmount}</span>
              <strong>{formatMoney(openBill.remainingTotal ?? openBill.goodsTotal)}</strong>
            </div>
          </div>
          {editingBill && openBill.status === 'draft' ? (
            <label className="field">
              <span>{t.sales.notes}</span>
              <textarea value={billNotes} onChange={(event) => setBillNotes(event.target.value)} rows={2} />
            </label>
          ) : openBill.notes ? (
            <p>{t.sales.notes}: {openBill.notes}</p>
          ) : null}
          {editingBill && openBill.status === 'draft' ? (
            <div className="action-row">
              <button type="button" className="btn-secondary" onClick={() => void run({ preventDefault() {} } as FormEvent, async () => {
                const lines = toSaveLines(editLines)
                if (lines.length === 0) throw new Error(t.sales.noLines)
                const saved = await saveBill(openBill.id, {
                  supplierId: openBill.supplierId,
                  invoiceDate: billDate,
                  notes: billNotes.trim() || null,
                  lines,
                })
                setOpenBill(saved)
                setEditLines(linesFromBill(saved))
                setEditingBill(false)
              })}>{t.save}</button>
              <button type="button" className="primary-btn" onClick={() => void run({ preventDefault() {} } as FormEvent, async () => {
                const lines = toSaveLines(editLines)
                if (lines.length === 0) throw new Error(t.sales.noLines)
                await saveBill(openBill.id, {
                  supplierId: openBill.supplierId,
                  invoiceDate: billDate,
                  notes: billNotes.trim() || null,
                  lines,
                })
                const posted = await postBill(openBill.id)
                setOpenBill(posted)
                setEditingBill(false)
              })}>{t.sales.post}</button>
            </div>
          ) : null}
        </section>
      ) : null}

      {section === 'bills' && !openBill ? (
      <section className="settings-block">
        <h2 className="editor-title">{t.ops.bills}</h2>
        <p className="muted">{t.ops.billHint}</p>
        <div className="line-entry">
          {warehouses.length === 1 ? <span className="muted">{warehouses[0].name}</span> : warehouses.length > 1 ? (
            <select value={warehouseId} onChange={(event) => setWarehouseId(event.target.value)}>{warehouses.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}</select>
          ) : <span className="muted">يجب إضافة مخزن أولاً من الإعدادات.</span>}
          <input type="date" value={newBillDate} onChange={(event) => setNewBillDate(event.target.value)} />
          {productPicker()}
          <input className="amount-input" value={qty} onChange={(event) => setQty(event.target.value)} placeholder={t.sales.quantity} />
          <input className="amount-input" value={price} onChange={(event) => setPrice(event.target.value)} placeholder={t.ops.companyPrice} />
          <button type="button" className="primary-btn toolbar-btn" onClick={() => addPendingLine('draft')}>{t.sales.addLine}</button>
        </div>
        {linesTable(draftLines, setDraftLines, false)}
        <div className="totals">
          <div>
            <span>{t.sales.goodsTotal}</span>
            <strong>{formatMoney(draftTotal)}</strong>
          </div>
        </div>
        <div className="action-row">
          <button type="button" className="btn-secondary" disabled={draftLines.length === 0} onClick={() => void run({ preventDefault() {} } as FormEvent, async () => {
            const lines = toSaveLines(draftLines)
            if (lines.length === 0) throw new Error(t.sales.noLines)
            const created = await saveBill(null, { supplierId, invoiceDate: newBillDate, notes: null, lines })
            setDraftLines([])
            await openBillDoc(created.id)
          })}>{t.sales.saveDraft}</button>
          <button type="button" className="primary-btn toolbar-btn" disabled={draftLines.length === 0} onClick={() => void run({ preventDefault() {} } as FormEvent, async () => {
            const lines = toSaveLines(draftLines)
            if (lines.length === 0) throw new Error(t.sales.noLines)
            const created = await saveBill(null, { supplierId, invoiceDate: newBillDate, notes: null, lines })
            await postBill(created.id)
            setDraftLines([])
          })}>{t.sales.post}</button>
        </div>
        {visibleBills.length > 0 ? (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.ops.documentNumber}</th>
                <th>{t.ops.documentDate}</th>
                <th>{t.ops.suppliers}</th>
                <th>{t.sales.goodsTotal}</th>
                <th>{t.sales.documentStatus}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {visibleBills.map((row) => (
                <tr key={row.id} className="clickable" onClick={() => void openBillDoc(row.id)}>
                  <td>{row.number ?? '—'}</td>
                  <td>{formatDate(row.invoiceDate)}</td>
                  <td>{row.supplierName}</td>
                  <td>{formatMoney(row.goodsTotal)}</td>
                  <td><StatusBadge kind={row.status === 'posted' ? 'posted' : 'draft'}>{row.status === 'posted' ? t.sales.posted : t.sales.draft}</StatusBadge></td>
                  <td>
                    <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); void openBillDoc(row.id) }}>{t.ops.openDocument}</button>
                    <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); onNavigate(`/print/bill/${row.id}`) }}>{t.ops.print}</button>
                    <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); void openBillDoc(row.id).then(() => setConfirmDeleteBill(true)) }}>{t.ops.deleteBill}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : <EmptyState title={t.ops.emptyReports} />}
      </section>
      ) : null}

      {section === 'suppliers' ? (
      <section className="settings-block">
        <h2 className="editor-title">{t.ops.suppliers}</h2>
        <form className="toolbar" onSubmit={(event) => void run(event, () => saveSupplier(null, { code, name, phone: '', address: '', isActive: true }))}>
          <input value={code} onChange={(event) => setCode(event.target.value)} placeholder={t.customers.code} required />
          <input value={name} onChange={(event) => setName(event.target.value)} placeholder={t.customers.name} required />
          <button className="btn-secondary" type="submit">{t.save}</button>
        </form>
        {suppliers.length === 0 ? <EmptyState title="لا يوجد موردون حتى الآن." hint="أضف مورداً بكود واسم من عندك." /> : (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.customers.code}</th>
                <th>{t.customers.name}</th>
                <th>{t.ops.supplierPayable}</th>
              </tr>
            </thead>
            <tbody>
              {suppliers.map((row) => (
                <tr key={row.id}>
                  <td>{row.code}</td>
                  <td>{row.name}</td>
                  <td>{formatMoney(row.outstanding)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
      ) : null}

      {section === 'payments' ? (
      <section className="settings-block">
        <h2 className="editor-title">{t.ops.supplierPay}</h2>
        <p className="muted">{t.ops.supplierPayHint}</p>
        {suppliers.length === 0 ? (
          <EmptyState title="لا يوجد موردون حتى الآن." />
        ) : (
          <form className="toolbar" onSubmit={(event) => void run(event, async () => {
            await createSupplierPayment({
              supplierId,
              paymentMethodId: payMethodId,
              amount: Number(payAmount),
              paidOn: payDate,
              reference: payReference.trim() || null,
              notes: null,
            })
            setPayAmount('')
            setPayReference('')
          })}>
            <input type="date" value={payDate} onChange={(event) => setPayDate(event.target.value)} required aria-label={t.ops.payDate} />
            {methods.length === 0 ? <span className="muted">{t.sales.noMethods}</span> : (
              <select value={payMethodId} onChange={(event) => setPayMethodId(event.target.value)} required>
                {methods.map((method) => <option key={method.id} value={method.id}>{method.name}</option>)}
              </select>
            )}
            <input value={payAmount} onChange={(event) => setPayAmount(event.target.value)} placeholder={t.sales.amount} required />
            <input value={payReference} onChange={(event) => setPayReference(event.target.value)} placeholder={t.ops.payReference} />
            <button className="primary-btn toolbar-btn" type="submit" disabled={methods.length === 0 || overpay}>{t.ops.supplierPay}</button>
          </form>
        )}
        {overpay ? <p className="banner error">{t.sales.overpay}</p> : null}
        {visiblePayments.length > 0 ? (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.ops.payDate}</th>
                <th>{t.ops.suppliers}</th>
                <th>{t.sales.amount}</th>
                <th>{t.sales.method}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {visiblePayments.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.paidOn)}</td>
                  <td>{row.supplierName}</td>
                  <td>{formatMoney(row.amount)}</td>
                  <td>{row.methodName}{row.reference ? ` — ${row.reference}` : ''}</td>
                  <td>
                    <button type="button" className="link-btn" onClick={() => setDeletePaymentId(row.id)}>{t.ops.deletePayment}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        ) : <EmptyState title={t.ops.emptySupplierPayments} />}
      </section>
      ) : null}

      {section === 'statement' ? (
      <section className="settings-block">
        <div className="page-head">
          <div>
            <h2 className="editor-title">{t.ops.supplierStatement}</h2>
            <p className="muted">{t.ops.supplierStatementHint}</p>
          </div>
          {supplierId ? (
            <button
              type="button"
              className="btn-secondary"
              onClick={() => onNavigate(`/print/supplier-statement/${supplierId}${stmtFrom || stmtTo ? `?from=${stmtFrom}&to=${stmtTo}` : ''}`)}
            >
              {t.customers.printStatement}
            </button>
          ) : null}
        </div>
        <form
          className="toolbar"
          onSubmit={(event) => {
            event.preventDefault()
            if (!supplierId) return
            void fetchSupplierStatement(supplierId, stmtFrom || undefined, stmtTo || undefined)
              .then(setStatement)
              .catch((err: unknown) => setError(err))
          }}
        >
          <input type="date" value={stmtFrom} onChange={(event) => setStmtFrom(event.target.value)} />
          <input type="date" value={stmtTo} onChange={(event) => setStmtTo(event.target.value)} />
          <button className="btn-secondary" type="submit">{t.ops.reports}</button>
        </form>
        {statement ? (
          <>
            <div className="totals">
              <div>
                <span>{t.customers.opening}</span>
                <strong>{formatMoney(statement.openingBalance)}</strong>
              </div>
              <div>
                <span>{t.ops.bills}</span>
                <strong>{formatMoney(statement.totalDebits)}</strong>
              </div>
              <div>
                <span>{t.ops.supplierPay}</span>
                <strong>{formatMoney(statement.totalCredits)}</strong>
              </div>
              <div>
                <span>{t.ops.supplierPayable}</span>
                <strong>{formatMoney(statement.outstanding)}</strong>
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
                    <th>{t.ops.bills}</th>
                    <th>{t.ops.supplierPay}</th>
                    <th>{t.ops.supplierPayable}</th>
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
          <EmptyState title={t.ops.supplierStatement} hint={t.ops.supplierStatementHint} />
        )}
      </section>
      ) : null}
      {confirmDeleteBill && openBill ? (
        <ConfirmDialog
          title={t.ops.deleteBill}
          body={t.ops.deleteBillBody}
          confirmLabel={t.ops.deleteBill}
          onConfirm={() => void run({ preventDefault() {} } as FormEvent, async () => {
            await deleteBill(openBill.id)
            setOpenBill(null)
            setConfirmDeleteBill(false)
            setEditingBill(false)
          })}
          onCancel={() => setConfirmDeleteBill(false)}
        />
      ) : null}
      {deletePaymentId ? (
        <ConfirmDialog
          title={t.ops.deletePayment}
          body={t.ops.deletePaymentBody}
          confirmLabel={t.ops.deletePayment}
          onConfirm={() => void run({ preventDefault() {} } as FormEvent, async () => {
            await deleteSupplierPayment(deletePaymentId)
            setDeletePaymentId(null)
          })}
          onCancel={() => setDeletePaymentId(null)}
        />
      ) : null}
    </section>
  )
}
