import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  createCustomer,
  fetchCustomers,
  fetchCustomerTypes,
  type CustomerListItem,
  type CustomerType,
  type SaveCustomer,
} from '../api/catalog'
import { useLocale } from '../i18n/LocaleContext'
import { EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatMoney } from '../ui/format'
import { Icon } from '../ui/Icons'

const emptyForm: SaveCustomer = {
  name: '',
  customerTypeId: null,
  contactPerson: '',
  phone: '',
  whatsApp: '',
  address: '',
}

type CustomersPageProps = {
  onNavigate: (path: string) => void
}

type SortKey = 'name' | 'outstanding' | 'invoices' | 'recent'

export function CustomersPage({ onNavigate }: CustomersPageProps) {
  const { t } = useLocale()
  const [query, setQuery] = useState('')
  const [rows, setRows] = useState<CustomerListItem[]>([])
  const [types, setTypes] = useState<CustomerType[]>([])
  const [open, setOpen] = useState(false)
  const [form, setForm] = useState<SaveCustomer>(emptyForm)
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [sort, setSort] = useState<SortKey>('outstanding')
  const [onlyOutstanding, setOnlyOutstanding] = useState(
    () => typeof window !== 'undefined' && new URLSearchParams(window.location.search).get('outstanding') === '1',
  )
  const [showArchived, setShowArchived] = useState(false)

  async function refresh(search = query) {
    const [list, typeList] = await Promise.all([fetchCustomers(search, showArchived), fetchCustomerTypes(true)])
    setRows(list)
    setTypes(typeList)
  }

  useEffect(() => {
    void refresh('').catch((err: unknown) => setError(err))
  }, [])

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void refresh(query).catch((err: unknown) => setError(err))
    }, 200)
    return () => window.clearTimeout(handle)
  }, [query, showArchived])

  const visible = useMemo(() => {
    let list = [...rows]
    if (onlyOutstanding) {
      list = list.filter((row) => row.outstanding > 0)
    }
    list.sort((a, b) => {
      if (sort === 'name') return a.name.localeCompare(b.name, 'ar')
      if (sort === 'invoices') return b.invoiceCount - a.invoiceCount
      if (sort === 'recent') {
        const aTime = a.lastInvoiceDate ? new Date(a.lastInvoiceDate).getTime() : 0
        const bTime = b.lastInvoiceDate ? new Date(b.lastInvoiceDate).getTime() : 0
        return bTime - aTime
      }
      return b.outstanding - a.outstanding
    })
    return list
  }, [rows, sort, onlyOutstanding])

  function startCreate() {
    setOpen(true)
    setForm(emptyForm)
    setError(null)
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await createCustomer(form)
      setOpen(false)
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
          <p className="breadcrumb">{t.nav.home} / {t.nav.customers}</p>
          <h1 className="page-title">{t.nav.customers}</h1>
          <p className="muted">{t.customers.hint}</p>
        </div>
        <button type="button" className="btn-secondary toolbar-btn" onClick={() => onNavigate('/print/report?type=customer-balances')}>
          {t.ops.print} {t.customers.allStatement}
        </button>
        <button type="button" className="primary-btn toolbar-btn icon-btn" onClick={startCreate}>
          <Icon name="add" />
          {t.customers.add}
        </button>
      </div>

      <ErrorBanner error={error} onRetry={() => void refresh()} />

      <div className="toolbar panel-toolbar">
        <label className="search-field">
          <Icon name="search" />
          <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder={t.customers.search} />
        </label>
        <select value={sort} onChange={(event) => setSort(event.target.value as SortKey)} title={t.customers.sort}>
          <option value="outstanding">{t.customers.sortOutstanding}</option>
          <option value="name">{t.customers.sortName}</option>
          <option value="invoices">{t.customers.sortInvoices}</option>
          <option value="recent">{t.customers.sortRecent}</option>
        </select>
        <label className="check filter-check">
          <input type="checkbox" checked={onlyOutstanding} onChange={(event) => setOnlyOutstanding(event.target.checked)} />
          <span>{t.customers.onlyOutstanding}</span>
        </label>
        <label className="check filter-check">
          <input type="checkbox" checked={showArchived} onChange={(event) => setShowArchived(event.target.checked)} />
          <span>{t.customers.showArchived}</span>
        </label>
      </div>

      {open ? (
        <form className="editor panel-card" onSubmit={onSave}>
          <div className="page-head">
            <h2 className="editor-title">{t.customers.add}</h2>
            <button type="button" className="btn-secondary" onClick={() => setOpen(false)}>
              {t.close}
            </button>
          </div>
          <p className="muted">{t.customers.codeHint}</p>
          <div className="form-section">
            <h3 className="section-title">{t.customers.basicInfo}</h3>
            <label className="field">
              <span>{t.customers.name}</span>
              <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} required autoFocus />
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
          <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
            {busy ? t.loading : t.save}
          </button>
        </form>
      ) : null}

      {visible.length === 0 ? (
        <EmptyState title={t.customers.empty} hint={t.customers.emptyHint} actionLabel={t.customers.emptyAction} onAction={startCreate} />
      ) : (
        <>
          <p className="muted">{t.customers.statementHint}</p>
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.customers.code}</th>
                <th>{t.customers.name}</th>
                <th>{t.customers.owesUs}</th>
                <th>{t.customers.weOwe}</th>
                <th>{t.customers.invoiceCount}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {visible.map((row) => (
                <tr key={row.id} className="clickable" onClick={() => onNavigate(`/customers/${row.id}`)}>
                  <td>{row.code}</td>
                  <td>
                    <strong>{row.name}</strong>
                    {row.phone ? <div className="muted">{row.phone}</div> : null}
                  </td>
                  <td className={row.outstanding > 0 ? 'money-warn' : undefined}>
                    {row.outstanding > 0 ? formatMoney(row.outstanding) : '—'}
                  </td>
                  <td className={row.outstanding < 0 ? 'money-ok' : undefined}>
                    {row.outstanding < 0 ? formatMoney(-row.outstanding) : '—'}
                  </td>
                  <td>{row.invoiceCount}</td>
                  <td>
                    <button type="button" className="link-btn" onClick={(event) => { event.stopPropagation(); onNavigate(`/customers/${row.id}`) }}>
                      {t.customers.open}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </section>
  )
}
