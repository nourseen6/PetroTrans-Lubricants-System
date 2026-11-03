import { useEffect, useState, type FormEvent } from 'react'
import {
  changeTreasuryCategory,
  createBankTransfer,
  createTreasuryEntry,
  deleteTreasuryEntry,
  fetchTreasury,
  type TreasuryBook,
  type TreasuryEntry,
} from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatDate, formatMoney, parseAmount, todayIso } from '../ui/format'
import {
  allTreasuryCategories,
  incomingTreasuryCategories,
  isIncomingTreasuryFilter,
  isOutgoingTreasuryFilter,
  isTransferCategory,
  outgoingTreasuryCategories,
  treasuryCategoryLabel,
  useTreasuryCategories,
} from '../ui/treasuryCategories'
import { summarizeByItem } from '../ui/treasurySummary'

type ShowSection = 'all' | 'in' | 'out' | 'bank' | 'summary'

export function TreasuryPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { t } = useLocale()
  const [book, setBook] = useState<TreasuryBook | null>(null)
  const [bankBook, setBankBook] = useState<TreasuryBook | null>(null)
  // Empty dates show everything; the user narrows the period only when they want to.
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [filterCategory, setFilterCategory] = useState('')
  const [show, setShow] = useState<ShowSection>('all')
  const [direction, setDirection] = useState<'in' | 'out'>('in')
  const [category, setCategory] = useState('')
  const [description, setDescription] = useState('')
  const [amount, setAmount] = useState('')
  const [notes, setNotes] = useState('')
  const [occurredOn, setOccurredOn] = useState(todayIso)
  const [error, setError] = useState<unknown>(null)
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)
  const [deleteTarget, setDeleteTarget] = useState<TreasuryEntry | null>(null)

  const items = useTreasuryCategories()
  // One form for everything: bank deposits/withdrawals are ordinary items here, and bank-only
  // items (bank fees) are listed under outgoing and go straight to the Banque Misr book.
  const bankOnly = items.categories.filter((item) => item.isActive && item.direction === 'bank').map((item) => item.code)
  const managed = items.forDirection(direction).map((item) => item.code)
  const categories: readonly string[] = managed.length
    ? direction === 'out' ? [...managed.filter((code) => code !== 'other'), ...bankOnly, 'other'] : managed
    : direction === 'in' ? incomingTreasuryCategories : outgoingTreasuryCategories
  // A single item (e.g. car expenses) narrows the tables to that item. Bank-only items such as
  // bank fees live in the Banque Misr book, so the filter goes there instead of the cash box.
  const summary = show === 'summary'
  const filterItem = items.categories.find((item) => item.code === filterCategory)
  const itemIsBank = filterItem?.direction === 'bank'
  const filtered = Boolean(filterCategory) && !summary
  const matchesIn = !filtered || (filterItem ? filterItem.direction === 'in' || filterItem.direction === 'both' : isIncomingTreasuryFilter(filterCategory))
  const matchesOut = !filtered || (filterItem ? filterItem.direction === 'out' || filterItem.direction === 'both' : isOutgoingTreasuryFilter(filterCategory))
  const showIncoming = !summary && !itemIsBank && (show === 'all' || show === 'in') && matchesIn
  const showOutgoing = !summary && !itemIsBank && (show === 'all' || show === 'out') && matchesOut
  const showBank = !summary && (filtered ? itemIsBank : show === 'all' || show === 'bank')
  const incoming = book?.entries.filter((row) => row.direction === 'in') ?? []
  const outgoing = book?.entries.filter((row) => row.direction === 'out') ?? []

  async function refresh(range: { from: string; to: string } = { from, to }) {
    const cashFilter = filtered && !itemIsBank ? filterCategory : undefined
    const bankFilter = filtered && itemIsBank ? filterCategory : undefined
    const [cash, bank] = await Promise.all([
      fetchTreasury(range.from || undefined, range.to || undefined, cashFilter),
      fetchTreasury(range.from || undefined, range.to || undefined, bankFilter, 'bank'),
    ])
    setBook(cash)
    setBankBook(bank)
  }

  // The tables follow the period, item and section as soon as they change, so the screen
  // always matches what "print" will produce.
  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [from, to, filterCategory, show, itemIsBank])

  function categoryLabel(code: string | null) {
    return items.names.get(code ?? '') ?? treasuryCategoryLabel(code, t.treasury)
  }

  function optionLabel(code: string) {
    return bankOnly.includes(code) ? `${categoryLabel(code)} (${t.treasury.fromBank})` : categoryLabel(code)
  }

  // Prints exactly what is on screen: the chosen section, period and item.
  function printPath() {
    const params = new URLSearchParams()
    if (from) params.set('from', from)
    if (to) params.set('to', to)
    if (filtered && itemIsBank) params.set('section', 'bank')
    else if (show !== 'all') params.set('section', show)
    if (filtered) params.set('category', filterCategory)
    const query = params.toString()
    return `/print/treasury${query ? `?${query}` : ''}`
  }

  // Cash movements typed by hand can be moved to another item; ones posted by payments or
  // bank transfers keep theirs, because their item belongs to that document.
  function editableItem(row: TreasuryEntry) {
    return !row.linked && !row.isTransfer && (row.account ?? 'cash') === 'cash' && items.categories.length > 0
  }

  function itemChoices(row: TreasuryEntry) {
    const direction = row.direction === 'in' ? 'in' : 'out'
    return items.forDirection(direction).map((item) => item.code).filter((code) => !isTransferCategory(code))
  }

  async function onChangeItem(row: TreasuryEntry, next: string) {
    if (!next || next === row.category) return
    setBusy(true)
    setError(null)
    setMessage('')
    try {
      await changeTreasuryCategory(row.id, next)
      setMessage(`${t.treasury.itemChanged}: ${row.description} ← ${categoryLabel(next)}`)
      await refresh()
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onSave(event: FormEvent) {
    event.preventDefault()
    const value = parseAmount(amount)
    if (value == null) return
    if (!category) {
      setError(new Error(t.treasury.chooseItem))
      return
    }
    setBusy(true)
    setError(null)
    setMessage('')
    try {
      if (isTransferCategory(category)) {
        await createBankTransfer({
          occurredOn,
          direction: category === 'bank_deposit' ? 'to_bank' : 'from_bank',
          amount: value,
          description: description.trim() || null,
          notes: notes.trim() || null,
        })
        setMessage(t.treasury.transferSaved)
      } else {
        await createTreasuryEntry({
          occurredOn,
          direction,
          category: category || null,
          description,
          amount: value,
          notes: notes.trim() || null,
          account: bankOnly.includes(category) ? 'bank' : 'cash',
        })
      }
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

  // How much went in and out under each item for the chosen period; clicking a row opens that item.
  function summaryTable(title: string, entries: TreasuryEntry[]) {
    const rows = summarizeByItem(entries, categoryLabel)
    const totalIn = rows.reduce((sum, row) => sum + row.totalIn, 0)
    const totalOut = rows.reduce((sum, row) => sum + row.totalOut, 0)
    return (
      <section className="panel-card">
        <h2 className="editor-title">{t.treasury.summaryByItem} — {title}</h2>
        {rows.length === 0 ? (
          <EmptyState title={t.treasury.emptySummary} />
        ) : (
          <div className="history-scroll">
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.treasury.category}</th>
                  <th>{t.treasury.movements}</th>
                  <th>{t.treasury.incoming}</th>
                  <th>{t.treasury.outgoing}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr
                    key={row.code}
                    className="clickable"
                    title={t.treasury.openItem}
                    onClick={() => {
                      setFilterCategory(row.code)
                      setShow('all')
                    }}
                  >
                    <td>{row.name}</td>
                    <td>{row.count}</td>
                    <td>{row.totalIn ? formatMoney(row.totalIn) : '—'}</td>
                    <td>{row.totalOut ? formatMoney(row.totalOut) : '—'}</td>
                  </tr>
                ))}
                <tr className="category-row">
                  <td>{t.treasury.total}</td>
                  <td>{entries.length}</td>
                  <td>{formatMoney(totalIn)}</td>
                  <td>{formatMoney(totalOut)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        )}
      </section>
    )
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
                  {editableItem(row) ? (
                    <select
                      className="inline-select"
                      value={row.category ?? ''}
                      disabled={busy}
                      title={t.treasury.changeItem}
                      onChange={(event) => void onChangeItem(row, event.target.value)}
                    >
                      {row.category && !itemChoices(row).includes(row.category) ? (
                        <option value={row.category}>{categoryLabel(row.category)}</option>
                      ) : null}
                      {itemChoices(row).map((code) => (
                        <option key={code} value={code}>{categoryLabel(code)}</option>
                      ))}
                    </select>
                  ) : (
                    categoryLabel(row.category)
                  )}
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
            <span className="kpi-label">{t.treasury.categoryTotal} — {categoryLabel(filterCategory)}</span>
            <span className="kpi-value">{formatMoney((itemIsBank ? bankBook : book)?.categoryTotal ?? 0)}</span>
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
            <div className="kpi-card">
              <span className="kpi-label">{t.treasury.bankBalance}</span>
              <span className="kpi-value">{formatMoney(bankBook?.balance ?? 0)}</span>
            </div>
          </>
        )}
      </div>

      {/* 1. Record a movement: one form for the cash box and Banque Misr. */}
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
                setCategory('')
              }}
            >
              <option value="in">{t.treasury.incoming}</option>
              <option value="out">{t.treasury.outgoing}</option>
            </select>
          </label>
          <label className="field">
            <span>{t.treasury.category}</span>
            {/* No item is picked for the user: a forgotten choice used to save movements as "salaries". */}
            <select value={category} onChange={(event) => setCategory(event.target.value)} required>
              <option value="" disabled>{t.treasury.chooseItem}</option>
              {categories.map((item) => (
                <option key={item} value={item}>{optionLabel(item)}</option>
              ))}
            </select>
          </label>
          <label className="field">
            <span>{t.sales.amount}</span>
            <input value={amount} onChange={(event) => setAmount(event.target.value)} required />
          </label>
        </div>
        {isTransferCategory(category) || bankOnly.includes(category) ? (
          <p className="field-hint">{t.treasury.bankFormHint}</p>
        ) : null}
        <label className="field">
          <span>{t.treasury.description}</span>
          <input
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            required={!isTransferCategory(category)}
            placeholder={category === 'other' ? t.treasury.otherHint : undefined}
          />
          {category === 'other' ? <small className="field-hint">{t.treasury.otherHint}</small> : null}
        </label>
        <label className="field">
          <span>{t.treasury.notes}</span>
          <input value={notes} onChange={(event) => setNotes(event.target.value)} />
        </label>
        <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>
          {busy ? t.loading : t.save}
        </button>
      </form>

      {/* 2. Choose what to see and print, right above the tables. */}
      <form
        className="toolbar panel-card"
        onSubmit={(event) => {
          event.preventDefault()
          void refresh().catch((err: unknown) => setError(err))
        }}
      >
        <label className="field">
          <span>{t.treasury.showWhat}</span>
          <select value={show} onChange={(event) => setShow(event.target.value as ShowSection)}>
            <option value="all">{t.treasury.showAll}</option>
            <option value="in">{t.treasury.incoming}</option>
            <option value="out">{t.treasury.outgoing}</option>
            <option value="bank">{t.treasury.bankName}</option>
            <option value="summary">{t.treasury.summaryByItem}</option>
          </select>
        </label>
        <label className="field">
          <span>{t.treasury.from}</span>
          <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        </label>
        <label className="field">
          <span>{t.treasury.to}</span>
          <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        </label>
        {summary || show === 'bank' ? null : (
          <label className="field">
            <span>{t.treasury.oneItem}</span>
            <select value={filterCategory} onChange={(event) => setFilterCategory(event.target.value)}>
              <option value="">{t.treasury.allCategories}</option>
              {items.categories.length ? (
                <>
                  <optgroup label={t.treasury.incoming}>
                    {items.categories.filter((item) => item.direction === 'in').map((item) => (
                      <option key={item.code} value={item.code}>{item.name}</option>
                    ))}
                  </optgroup>
                  <optgroup label={t.treasury.outgoing}>
                    {items.categories.filter((item) => item.direction === 'out' || item.direction === 'both').map((item) => (
                      <option key={item.code} value={item.code}>{item.name}</option>
                    ))}
                  </optgroup>
                  <optgroup label={t.treasury.bankName}>
                    {items.categories.filter((item) => item.direction === 'bank').map((item) => (
                      <option key={item.code} value={item.code}>{item.name}</option>
                    ))}
                  </optgroup>
                </>
              ) : (
                allTreasuryCategories.map((item) => (
                  <option key={item} value={item}>{categoryLabel(item)}</option>
                ))
              )}
            </select>
          </label>
        )}
        {from || to ? (
          <button
            className="btn-ghost"
            type="button"
            onClick={() => {
              setFrom('')
              setTo('')
            }}
          >
            {t.treasury.allPeriods}
          </button>
        ) : null}
        <button type="button" className="btn-secondary" onClick={() => onNavigate(printPath())}>
          {t.treasury.printShown}
        </button>
      </form>

      {/* 3. The movements. */}
      <div className="treasury-split">
        {summary ? (
          <>
            {summaryTable(t.treasury.cashBox, book?.entries ?? [])}
            {summaryTable(t.treasury.bankName, bankBook?.entries ?? [])}
          </>
        ) : null}

        {showIncoming ? (
          <section className="panel-card">
            <h2 className="editor-title">{t.treasury.incoming}</h2>
            {!book || incoming.length === 0 ? (
              <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyIncoming} />
            ) : (
              entryTable(incoming)
            )}
          </section>
        ) : null}

        {showOutgoing ? (
          <section className="panel-card">
            <h2 className="editor-title">{t.treasury.outgoing}</h2>
            {!book || outgoing.length === 0 ? (
              <EmptyState title={filtered ? t.treasury.emptyCategory : t.treasury.emptyOutgoing} />
            ) : (
              entryTable(outgoing)
            )}
          </section>
        ) : null}

        {showBank ? (
          <section className="panel-card">
            <h2 className="editor-title">{t.treasury.bankName}</h2>
            {filtered ? null : (
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
            )}
            {!bankBook || bankBook.entries.length === 0 ? (
              <EmptyState title={t.treasury.emptyBank} />
            ) : (
              entryTable(bankBook.entries, { showBalance: true })
            )}
          </section>
        ) : null}
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
