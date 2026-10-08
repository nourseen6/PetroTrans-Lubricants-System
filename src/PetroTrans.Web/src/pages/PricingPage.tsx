import { useEffect, useState, type FormEvent } from 'react'
import {
  fetchCustomers,
  fetchCustomerPrices,
  fetchPriceHistory,
  fetchPricingMatrix,
  removeCustomerPrice,
  setCustomerPrice,
  updateBasePrice,
  updatePurchasePrice,
  type CustomerListItem,
  type CustomerPrice,
  type PriceHistory,
  type PricingMatrixRow,
} from '../api/catalog'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount } from '../ui/format'
import { Icon } from '../ui/Icons'
import { groupByCatalogCategory } from '../ui/catalogGroups'

export function PricingPage() {
  const { t } = useLocale()
  const [query, setQuery] = useState('')
  const [rows, setRows] = useState<PricingMatrixRow[]>([])
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [selected, setSelected] = useState<PricingMatrixRow | null>(null)
  const [basePrice, setBasePrice] = useState('')
  const [purchasePrice, setPurchasePrice] = useState('')
  const [reason, setReason] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [customerPrice, setCustomerPriceValue] = useState('')
  const [specials, setSpecials] = useState<CustomerPrice[]>([])
  const [history, setHistory] = useState<PriceHistory[]>([])
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [removeTarget, setRemoveTarget] = useState<CustomerPrice | null>(null)

  async function refresh(search = query) {
    const [matrix, customerList] = await Promise.all([fetchPricingMatrix(search), fetchCustomers('')])
    setRows(matrix)
    setCustomers(customerList)
  }

  useEffect(() => {
    void refresh('').catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void refresh(query).catch((err: unknown) => setError(err))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [query])

  async function openRow(row: PricingMatrixRow) {
    setSelected(row)
    setBasePrice(row.basePrice == null ? '' : String(row.basePrice))
    setPurchasePrice(row.purchasePrice == null ? '' : String(row.purchasePrice))
    setReason('')
    setCustomerId('')
    setCustomerPriceValue('')
    const [prices, hist] = await Promise.all([fetchCustomerPrices(), fetchPriceHistory(row.variantId)])
    setSpecials(prices.filter((item) => item.variantId === row.variantId))
    setHistory(hist)
  }

  async function saveBase(event: FormEvent) {
    event.preventDefault()
    if (!selected) return
    const amount = parseAmount(basePrice)
    if (amount == null) return
    setBusy(true)
    setError(null)
    try {
      await updateBasePrice(selected.variantId, amount, reason.trim() || null)
      await refresh()
      const next = (await fetchPricingMatrix(query)).find((item) => item.variantId === selected.variantId)
      if (next) await openRow(next)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function savePurchase(event: FormEvent) {
    event.preventDefault()
    if (!selected) return
    const amount = parseAmount(purchasePrice)
    if (amount == null) return
    setBusy(true)
    setError(null)
    try {
      await updatePurchasePrice(selected.variantId, amount, reason.trim() || null)
      await refresh()
      const next = (await fetchPricingMatrix(query)).find((item) => item.variantId === selected.variantId)
      if (next) await openRow(next)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function saveCustomerPrice(event: FormEvent) {
    event.preventDefault()
    if (!selected || !customerId) return
    const amount = parseAmount(customerPrice)
    if (amount == null) return
    setBusy(true)
    setError(null)
    try {
      await setCustomerPrice({ customerId, variantId: selected.variantId, unitPrice: amount, reason: reason.trim() || null })
      await openRow(selected)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function confirmRemove() {
    if (!removeTarget || !selected) return
    setBusy(true)
    setError(null)
    try {
      await removeCustomerPrice(removeTarget.customerId, removeTarget.variantId, reason.trim() || undefined)
      setRemoveTarget(null)
      await openRow(selected)
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
          <h1 className="page-title">{t.nav.pricing}</h1>
          <p className="muted">{t.pricing.hint}</p>
        </div>
      </div>
      <ErrorBanner error={error} onRetry={() => void refresh()} />
      <div className="toolbar panel-toolbar">
        <label className="search-field">
          <Icon name="search" />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={t.pricing.search} />
        </label>
      </div>
      {rows.length === 0 ? (
        <EmptyState title={t.products.empty} />
      ) : (
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.sales.product}</th>
              <th>{t.products.brand}</th>
              <th>{t.products.packagingType}</th>
              <th>{t.pricing.customerList}</th>
              <th>{t.pricing.companyPrices}</th>
              <th>{t.pricing.customersUsing}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {groupByCatalogCategory(rows, (row) => row.category).flatMap((group) => [
              <tr key={`cat-${group.title}`} className="category-row">
                <td colSpan={7}>{group.title}</td>
              </tr>,
              ...group.rows.map((row) => (
                <tr key={row.variantId} className="clickable" onClick={() => void openRow(row)}>
                  <td>{row.productName}</td>
                  <td>{row.brand ?? '—'}</td>
                  <td>
                    {row.packagingType} {row.packagingSize}
                  </td>
                  <td>{formatMoney(row.basePrice)}</td>
                  <td>{formatMoney(row.purchasePrice ?? null)}</td>
                  <td>{row.customerPriceCount}</td>
                  <td>
                    <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); void openRow(row) }}>
                      {t.customers.edit}
                    </button>
                  </td>
                </tr>
              )),
            ])}
          </tbody>
        </table>
      )}

      {selected ? (
        <form className="editor panel-card" onSubmit={(event) => void saveBase(event)}>
          <h2 className="editor-title">
            {t.pricing.customerList}: {selected.productName} — {selected.packagingType} {selected.packagingSize}
          </h2>
          <p className="muted">{t.pricing.customerListHint}</p>
          <div className="form-grid">
            <label className="field">
              <span>{t.pricing.basePrice}</span>
              <input value={basePrice} onChange={(event) => setBasePrice(event.target.value)} />
            </label>
            <label className="field">
              <span>{t.pricing.reason}</span>
              <input value={reason} onChange={(event) => setReason(event.target.value)} />
            </label>
          </div>
          <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
            {t.pricing.saveBase}
          </button>
        </form>
      ) : null}

      {selected ? (
        <form className="editor panel-card" onSubmit={(event) => void savePurchase(event)}>
          <h2 className="editor-title">{t.pricing.companyPrices}</h2>
          <p className="muted">{t.pricing.companyPriceHint}</p>
          <div className="form-grid">
            <label className="field">
              <span>{t.pricing.companyPrices}</span>
              <input value={purchasePrice} onChange={(event) => setPurchasePrice(event.target.value)} />
            </label>
          </div>
          <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
            {t.pricing.saveCompany}
          </button>
        </form>
      ) : null}

      {selected ? (
        <form className="panel-card" onSubmit={(event) => void saveCustomerPrice(event)}>
          <h2 className="editor-title">{t.pricing.customerPrices}</h2>
          <p className="muted">{t.pricing.customerPriceHint}</p>
          <div className="form-grid">
            <label className="field">
              <span>{t.sales.customer}</span>
              <select value={customerId} onChange={(event) => setCustomerId(event.target.value)} required>
                <option value="">{t.sales.chooseCustomer}</option>
                {customers.map((customer) => (
                  <option key={customer.id} value={customer.id}>
                    {customer.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="field">
              <span>{t.pricing.customerPrice}</span>
              <input value={customerPrice} onChange={(event) => setCustomerPriceValue(event.target.value)} required />
            </label>
          </div>
          <button className="btn-secondary toolbar-btn" type="submit" disabled={busy}>
            {t.pricing.addCustomerPrice}
          </button>
          {specials.length === 0 ? (
            <p className="muted">{t.ops.emptyReports}</p>
          ) : (
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.sales.customer}</th>
                  <th>{t.pricing.customerPrice}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {specials.map((row) => (
                  <tr key={row.id}>
                    <td>{row.customerName}</td>
                    <td>{formatMoney(row.unitPrice)}</td>
                    <td>
                      <button type="button" className="link-btn" onClick={() => setRemoveTarget(row)}>
                        {t.pricing.remove}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </form>
      ) : null}

      {selected ? (
        <section className="panel-card">
          <h2 className="editor-title">{t.pricing.history}</h2>
          {history.length === 0 ? (
            <EmptyState title={t.ops.emptyReports} />
          ) : (
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.sales.date}</th>
                  <th>{t.pricing.kind}</th>
                  <th>{t.pricing.oldPrice}</th>
                  <th>{t.pricing.newPrice}</th>
                  <th>{t.pricing.reason}</th>
                </tr>
              </thead>
              <tbody>
                {history.map((row) => (
                  <tr key={row.id}>
                    <td>{formatDate(row.changedAt)}</td>
                    <td>{row.changeKind}</td>
                    <td>{formatMoney(row.oldUnitPrice)}</td>
                    <td>{formatMoney(row.newUnitPrice)}</td>
                    <td>{row.reason ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      ) : null}

      {removeTarget ? (
        <ConfirmDialog
          title={t.pricing.remove}
          body={t.pricing.hint}
          confirmLabel={t.pricing.remove}
          onConfirm={() => void confirmRemove()}
          onCancel={() => setRemoveTarget(null)}
        />
      ) : null}
    </section>
  )
}
