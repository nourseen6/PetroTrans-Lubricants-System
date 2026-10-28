import { useEffect, useState, type FormEvent } from 'react'
import {
  createBankTransfer,
  createTreasuryEntry,
  deleteTreasuryEntry,
  fetchTreasury,
  type TreasuryAccount,
  type TreasuryBook,
  type TreasuryEntry,
} from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, todayIso } from '../ui/format'
import {
  allTreasuryCategories,
  bankIncomingCategories,
  bankOutgoingCategories,
  incomingTreasuryCategories,
  isIncomingTreasuryFilter,
  isOutgoingTreasuryFilter,
  isTransferCategory,
  outgoingTreasuryCategories,
  treasuryCategoryLabel,
} from '../ui/treasuryCategories'

type PrintSection = 'in' | 'out' | 'bank'

function monthStartIso() {
  return `${todayIso().slice(0, 8)}01`
}

export function TreasuryPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { t } = useLocale()
  const [book, setBook] = useState<TreasuryBook | null>(null)
  const [bankBook, setBankBook] = useState<TreasuryBook | null>(null)
  const [from, setFrom] = useState(monthStartIso)
  const [to, setTo] = useState(todayIso)
  const [filterCategory, setFilterCategory] = useState('')
  const [direction, setDirection] = useState<'in' | 'out'>('in')
  const [category, setCategory] = useState('sales')
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [notes, setNotes] = useState('')
  const [occurredOn, setOccurredOn] = useState(todayIso)
  const [bankDirection, setBankDirection] = useState<'in' | 'out'>('in')
  const [bankCategory, setBankCategory] = useState('bank_deposit')
  const [bankDescription, setBankDescription] = useState('')
  const [bankAmount, setBankAmount] = useState('')
  const [bankOccurredOn, setBankOccurredOn] = useState(todayIso)
  const [error, setError] = useState<unknown>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<TreasuryEntry | null>(null)

  const categories = direction === 'in' ? incomingTreasuryCategories : outgoingTreasuryCategories
  const bankCategories = bankDirection === 'in' ? bankIncomingCategories : bankOutgoingCategories
  const filtered = Boolean(book?.filterCategory)
  const showIncoming = isIncomingTreasuryFilter(book?.filterCategory)
  const showOutgoing = isOutgoingTreasuryFilter(book?.filterCategory)
  const incoming = book?.entries.filter((row) => row.direction === 'in') ?? []
  const outgoing = book?.entries.filter((row) => row.direction === 'out') ?? []

  async function refresh() {
    const [cash, bank] = await Promise.all([
      fetchTreasury(from || undefined, to || undefined, filterCategory || undefined),
      fetchTreasury(from || undefined, to || undefined, undefined, 'bank'),
    ])
    setBook(cash)
    setBankBook(bank)
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [])

  function categoryLabel(code: string | null) {
    return treasuryCategoryLabel(code, t.treasury)
  }

  function printPath(section: PrintSection) {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    params.set('section', section)
    if (filterCategory && section !== 'bank') params.set('category', filterCategory)
    return `/print/treasury?${params.toString()}`
  }

  async function save(account: TreasuryAccount, values: { occurredOn: string; direction: 'in' | 'out'; category: string; description: string; amount: string; notes: string }) {
    const value = parseAmount(values.amount)
    if (value == null) return false
    setBusy(true)
    setError(null)
    setMessage('')
    try {
      if (isTransferCategory(values.category)) {
        await createBankTransfer({
          occurredOn: values.occurredOn,
          direction: values.category === 'bank_deposit' ? 'to_bank' : 'from_bank',
          amount: value,
          description: values.description.trim() || null,
          notes: values.notes.trim() || null,
        })
        setMessage(t.treasury.transferSaved)
      } else {
        await createTreasuryEntry({
          occurredOn: values.occurredOn,
          direction: values.direction,
          category: values.category || null,
          description: values.description,
          amount: value,
          notes: values.notes.trim() || null,
          account,
        })
      }
      await refresh()
      return true
    } catch (err) {
      setError(err)
      return false
    } finally {
      setBusy(false)
    }
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    const ok = await save('cash', { occurredOn, direction, category, description, amount, notes })
    if (ok) {
      setAmount('')
      setDescription('')
      setNotes('')
    }
  }

  async function onSaveBank(event: FormEvent) {
    event.preventDefault()
    const ok = await save('bank', {
      occurredOn: bankOccurredOn,
      direction: bankDirection,
      category: bankCategory,
      description: bankDescription,
      amount: bankAmount,
      notes: '',
    })
    if (ok) {
      setBankAmount('')
      setBankDescription('')
    }
  }

  async function onDelete(id: string) {
    setBusy(true)
    setError(null)
    setDeleteTarget(null)
    try {
      await deleteTreasuryEntry(id)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function entryTable(rows: TreasuryEntry[], options: { showBalance?: boolean } = {}) {
    return (
      <div className="history-scroll">
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.sales.date}</th>
              <th>{t.treasury.description}</th>
              <th>{t.treasury.category}</th>
              {options.showBalance ? (
                <>
                  <th>{t.treasury.bankIn}</th>
                  <th>{t.treasury.bankOut}</th>
                  <th>{t.treasury.runningBalance}</th>
                </>
              ) : (
                <th>{t.sales.amount}</th>
              )}
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id}>
                <td>{formatDate(row.occurredOn)}</td>
                <td>{row.description}</td>
                <td>
                  {categoryLabel(row.category)}
                  {row.isTransfer ? <span className="muted"> · {t.treasury.transferTag}</span> : null}
                </td>
                {options.showBalance ? (
                  <>
                    <td>{row.direction === 'in' ? formatMoney(row.amount) : ''}</td>
                    <td>{row.direction === 'out' ? formatMoney(row.amount) : ''}</td>
                    <td>{formatMoney(row.runningBalance)}</td>
                  </>
                ) : (
                  <td>{formatMoney(row.amount)}</td>
                )}
                <td>
                  {row.linked ? (
                    <span className="muted">{t.treasury.linked}</span>
                  ) : (
                    <button type="button" className="link-btn" disabled={busy} onClick={() => setDeleteTarget(row)}>
                      {t.treasury.delete}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    )
  }

  function panelHead(title: string, section: PrintSection, label: string) {
    return (
      <div className="panel-head">
        <h2 className="editor-title">{title}</h2>
        <button type="button" className="btn-secondary" onClick={() => onNavigate(printPath(section))}>
          {label}
        </button>
      </div>
    )
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
      {message ? <p className="banner ok">{message}</p> : null}

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
        <div className="kpi-card">
          <span className="kpi-label">{t.treasury.bankBalance}</span>
          <span className="kpi-value">{formatMoney(bankBook?.balance ?? 0)}</span>
        </div>
      </div>

      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault()
          void refresh().catch((err: unknown) => setError(err))
        }}
      >
        <label className="field">
          <span>{t.treasury.from}</span>
          <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        </label>
        <label className="field">
          <span>{t.treasury.to}</span>
          <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </label>
        <label className="field">
          <span>{t.treasury.category}</span>
          <select value={filterCategory} onChange={(event) => setFilterCategory(event.target.value)}>
            <option value="">{t.treasury.allCategories}</option>
            {allTreasuryCategories.map((item) => (
              <option key={item} value={item}>{categoryLabel(item)}</option>
            ))}
          </select>
        </label>
        <button className="btn-secondary" type="submit">{t.treasury.show}</button>
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
          <input value={description} onChange={(event) => setDescription(event.target.value)} required={!isTransferCategory(category)} />
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
            {panelHead(t.treasury.incoming, 'in', t.treasury.printIn)}
            {!book || incoming.length === 0 ? (
              <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyIncoming} />
            ) : (
              entryTable(incoming)
            )}
          </section>
        ) : null}

        {showOutgoing ? (
          <section className="panel-card">
            {panelHead(t.treasury.outgoing, 'out', t.treasury.printOut)}
            {!book || outgoing.length === 0 ? (
              <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyOutgoing} />
            ) : (
              entryTable(outgoing)
            )}
          </section>
        ) : null}

        <section className="panel-card">
          {panelHead(t.treasury.bankName, 'bank', t.treasury.printBank)}
          <p className="muted">{t.treasury.bankHint}</p>
          <form className="editor" onSubmit={(event) => void onSaveBank(event)}>
            <h3 className="editor-title">{t.treasury.addBank}</h3>
            <div className="form-grid">
              <label className="field">
                <span>{t.sales.date}</span>
                <input type="date" value={bankOccurredOn} onChange={(event) => setBankOccurredOn(event.target.value)} required />
              </label>
              <label className="field">
                <span>{t.treasury.bankIn} / {t.treasury.bankOut}</span>
                <select
                  value={bankDirection}
                  onChange={(event) => {
                    const next = event.target.value === 'out' ? 'out' : 'in'
                    setBankDirection(next)
                    setBankCategory(next === 'in' ? 'bank_deposit' : 'bank_withdrawal')
                  }}
                >
                  <option value="in">{t.treasury.bankIn}</option>
                  <option value="out">{t.treasury.bankOut}</option>
                </select>
              </label>
              <label className="field">
                <span>{t.treasury.category}</span>
                <select value={bankCategory} onChange={(event) => setBankCategory(event.target.value)}>
                  {bankCategories.map((item) => (
                    <option key={item} value={item}>{categoryLabel(item)}</option>
                  ))}
                </select>
              </label>
              <label className="field">
                <span>{t.sales.amount}</span>
                <input value={bankAmount} onChange={(event) => setBankAmount(event.target.value)} required />
              </label>
            </div>
            <label className="field">
              <span>{t.treasury.description}</span>
              <input value={bankDescription} onChange={(event) => setBankDescription(event.target.value)} required={!isTransferCategory(bankCategory)} />
            </label>
            <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
              {busy ? t.loading : t.save}
            </button>
          </form>
          <div className="profile-kpi-grid">
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.previousBalance}</span>
              <span className="kpi-value">{formatMoney(bankBook?.openingBalance ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.bankIn}</span>
              <span className="kpi-value">{formatMoney(bankBook?.totalIn ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.bankOut}</span>
              <span className="kpi-value">{formatMoney(bankBook?.totalOut ?? 0)}</span>
            </div>
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.bankBalance}</span>
              <span className="kpi-value">{formatMoney(bankBook?.balance ?? 0)}</span>
            </div>
          </div>
          {!bankBook || bankBook.entries.length === 0 ? (
            <EmptyState title={t.treasury.emptyBank} />
          ) : (
            entryTable(bankBook.entries, { showBalance: true })
          )}
        </section>
      </div>

      {deleteTarget ? (
        <ConfirmDialog
          title={t.treasury.delete}
          body={deleteTarget.isTransfer ? t.treasury.deleteTransferConfirm : t.treasury.deleteConfirm}
          confirmLabel={t.treasury.delete}
          onConfirm={() => void onDelete(deleteTarget.id)}
          onCancel={() => setDeleteTarget(null)}
        />
      ) : null}
    </section>
  )
}
