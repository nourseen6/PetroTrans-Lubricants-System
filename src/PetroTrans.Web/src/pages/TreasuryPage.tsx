import { useEffect, useState, type FormEvent } from 'react'
import { createTreasuryEntry, deleteTreasuryEntry, fetchTreasury, type TreasuryBook } from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, todayIso } from '../ui/format'
import {
  allTreasuryCategories,
  incomingTreasuryCategories,
  isIncomingTreasuryFilter,
  isOutgoingTreasuryFilter,
  outgoingTreasuryCategories,
  treasuryCategoryLabel,
} from '../ui/treasuryCategories'

export function TreasuryPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { t } = useLocale()
  const [book, setBook] = useState<TreasuryBook | null>(null)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [filterCategory, setFilterCategory] = useState('')
  const [direction, setDirection] = useState<'in' | 'out'>('in')
  const [category, setCategory] = useState('sales')
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [notes, setNotes] = useState('')
  const [occurredOn, setOccurredOn] = useState(todayIso)
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [deleteId, setDeleteId] = useState<string | null>(null)

  const categories = direction === 'in' ? incomingTreasuryCategories : outgoingTreasuryCategories
  const filtered = Boolean(book?.filterCategory)
  const showIncoming = isIncomingTreasuryFilter(book?.filterCategory)
  const showOutgoing = isOutgoingTreasuryFilter(book?.filterCategory)
  const incoming = book?.entries.filter((row) => row.direction === 'in') ?? []
  const outgoing = book?.entries.filter((row) => row.direction === 'out') ?? []

  async function refresh() {
    setBook(await fetchTreasury(from || undefined, to || undefined, filterCategory || undefined))
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [])

  function categoryLabel(code: string | null) {
    return treasuryCategoryLabel(code, t.treasury)
  }

  function printPath() {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    if (filterCategory) params.set('category', filterCategory)
    const query = params.toString()
    return `/print/treasury${query ? `?${query}` : ''}`
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    const value = parseAmount(amount)
    if (value == null) return
    setBusy(true)
    setError(null)
    try {
      await createTreasuryEntry({
        occurredOn,
        direction,
        category: category || null,
        description,
        amount: value,
        notes: notes.trim() || null,
      })
      setAmount('')
      setDescription('')
      setNotes('')
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onDelete(id: string) {
    setBusy(true)
    setError(null)
    setDeleteId(null)
    try {
      await deleteTreasuryEntry(id)
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
          <h1 className="page-title">{t.nav.treasury}</h1>
          <p className="muted">{t.treasury.hint}</p>
        </div>
      </div>
      <ErrorBanner error={error} onRetry={() => void refresh()} />

      <div className="profile-kpi-grid">
        {filtered ? (
          <div className="kpi-card">
            <span className="kpi-label">{t.treasury.categoryTotal} — {categoryLabel(book?.filterCategory ?? null)}</span>
            <span className="kpi-value">{formatMoney(book?.categoryTotal ?? 0)}</span>
          </div>
        ) : (
          <>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.previousBalance}</span>
              <span className="kpi-value">{formatMoney(book?.openingBalance ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.incoming}</span>
              <span className="kpi-value">{formatMoney(book?.totalIn ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.outgoing}</span>
              <span className="kpi-value">{formatMoney(book?.totalOut ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.dailyBalance}</span>
              <span className="kpi-value">{formatMoney(book?.balance ?? 0)}</span>
            </div>
          </>
        )}
      </div>

      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault()
          void refresh().catch((err: unknown) => setError(err))
        }}
      >
        <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        <label className="field">
          <span>{t.treasury.category}</span>
          <select value={filterCategory} onChange={(event) => setFilterCategory(event.target.value)}>
            <option value="">{t.treasury.allCategories}</option>
            {allTreasuryCategories.map((item) => (
              <option key={item} value={item}>{categoryLabel(item)}</option>
            ))}
          </select>
        </label>
        <button className="btn-secondary" type="submit">{t.ops.reports}</button>
        <button type="button" className="btn-secondary" onClick={() => onNavigate(printPath())}>
          {filtered ? t.treasury.printCategory : t.treasury.print}
        </button>
      </form>

      <form className="editor panel-card" onSubmit={(event) => void onSave(event)}>
        <h2 className="editor-title">{direction === 'in' ? t.treasury.addIn : t.treasury.addOut}</h2>
        <div className="form-grid">
          <label className="field">
            <span>{t.sales.date}</span>
            <input type="date" value={occurredOn} onChange={(event) => setOccurredOn(event.target.value)} required />
          </label>
          <label className="field">
            <span>{t.treasury.incoming} / {t.treasury.outgoing}</span>
            <select
              value={direction}
              onChange={(event) => {
                const next = event.target.value === 'out' ? 'out' : 'in'
                setDirection(next)
                setCategory(next === 'in' ? 'sales' : 'salaries')
              }}
            >
              <option value="in">{t.treasury.incoming}</option>
              <option value="out">{t.treasury.outgoing}</option>
            </select>
          </label>
          <label className="field">
            <span>{t.treasury.category}</span>
            <select value={category} onChange={(event) => setCategory(event.target.value)}>
              {categories.map((item) => (
                <option key={item} value={item}>{categoryLabel(item)}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t.sales.amount}</span>
            <input value={amount} onChange={(event) => setAmount(event.target.value)} required />
          </label>
        </div>
        <label className="field">
          <span>{t.treasury.description}</span>
          <input value={description} onChange={(event) => setDescription(event.target.value)} required />
        </label>
        <label className="field">
          <span>{t.treasury.notes}</span>
          <input value={notes} onChange={(event) => setNotes(event.target.value)} />
        </label>
        <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
          {busy ? t.loading : t.save}
        </button>
      </form>

      <div className="treasury-split">
        {showIncoming ? (
        <section className="panel-card">
          <h2 className="editor-title">{t.treasury.incoming}</h2>
          {!book || incoming.length === 0 ? (
            <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyIncoming} />
          ) : (
            <div className="history-scroll">
              <table className="data-table compact">
                <thead>
                  <tr>
                    <th>{t.sales.date}</th>
                    <th>{t.treasury.description}</th>
                    <th>{t.treasury.category}</th>
                    <th>{t.sales.amount}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {incoming.map((row) => (
                    <tr key={row.id}>
                      <td>{formatDate(row.occurredOn)}</td>
                      <td>{row.description}</td>
                      <td>{categoryLabel(row.category)}</td>
                      <td>{formatMoney(row.amount)}</td>
                      <td>
                        {row.linked ? (
                          <span className="muted">{t.treasury.linked}</span>
                        ) : (
                          <button type="button" className="link-btn" disabled={busy} onClick={() => setDeleteId(row.id)}>
                            {t.treasury.delete}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
        ) : null}

        {showOutgoing ? (
        <section className="panel-card">
          <h2 className="editor-title">{t.treasury.outgoing}</h2>
          {!book || outgoing.length === 0 ? (
            <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyOutgoing} />
          ) : (
            <div className="history-scroll">
              <table className="data-table compact">
                <thead>
                  <tr>
                    <th>{t.sales.date}</th>
                    <th>{t.treasury.description}</th>
                    <th>{t.treasury.category}</th>
                    <th>{t.sales.amount}</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {outgoing.map((row) => (
                    <tr key={row.id}>
                      <td>{formatDate(row.occurredOn)}</td>
                      <td>{row.description}</td>
                      <td>{categoryLabel(row.category)}</td>
                      <td>{formatMoney(row.amount)}</td>
                      <td>
                        {row.linked ? (
                          <span className="muted">{t.treasury.linked}</span>
                        ) : (
                          <button type="button" className="link-btn" disabled={busy} onClick={() => setDeleteId(row.id)}>
                            {t.treasury.delete}
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
        ) : null}
      </div>

      {deleteId ? (
        <ConfirmDialog
          title={t.treasury.delete}
          body={t.treasury.deleteConfirm}
          confirmLabel={t.treasury.delete}
          onConfirm={() => void onDelete(deleteId)}
          onCancel={() => setDeleteId(null)}
        />
      ) : null}
    </section>
  )
}
