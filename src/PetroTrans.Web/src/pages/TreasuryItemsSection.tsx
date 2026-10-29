import { useState, type FormEvent } from 'react'
import { createTreasuryCategory, updateTreasuryCategory, type TreasuryCategory } from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ErrorBanner } from '../ui/Feedback'
import { useTreasuryCategories } from '../ui/treasuryCategories'

/** Settings block where the owner renames, adds, or hides treasury items. */
export function TreasuryItemsSection() {
  const { t } = useLocale()
  const items = useTreasuryCategories()
  const [names, setNames] = useState<Record<string, string>>({})
  const [newName, setNewName] = useState('')
  const [newDirection, setNewDirection] = useState<'in' | 'out'>('out')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [ok, setOk] = useState<string | null>(null)

  async function run(action: () => Promise<unknown>) {
    setBusy(true)
    setError(null)
    setOk(null)
    try {
      await action()
      await items.reload()
      setOk(t.treasury.itemSaved)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function kindLabel(item: TreasuryCategory) {
    switch (item.direction) {
      case 'in': return t.treasury.incoming
      case 'out': return t.treasury.outgoing
      case 'bank': return t.treasury.bankOnly
      default: return t.treasury.both
    }
  }

  function onAdd(event: FormEvent) {
    event.preventDefault()
    void run(async () => {
      await createTreasuryCategory(newName, newDirection)
      setNewName('')
    })
  }

  function save(item: TreasuryCategory, isActive: boolean) {
    const name = names[item.id] ?? item.name
    void run(async () => {
      await updateTreasuryCategory(item.id, name, isActive)
      setNames(({ [item.id]: _, ...rest }) => rest)
    })
  }

  return (
    <div>
      <p className="muted">{t.treasury.itemsHint}</p>
      <ErrorBanner error={error} onRetry={() => void items.reload()} />
      {ok ? <p className="banner ok">{ok}</p> : null}

      <form className="toolbar" onSubmit={onAdd}>
        <label className="field">
          <span>{t.treasury.itemName}</span>
          <input value={newName} onChange={(event) => setNewName(event.target.value)} maxLength={64} required />
        </label>
        <label className="field">
          <span>{t.treasury.itemKind}</span>
          <select value={newDirection} onChange={(event) => setNewDirection(event.target.value === 'in' ? 'in' : 'out')}>
            <option value="out">{t.treasury.outgoing}</option>
            <option value="in">{t.treasury.incoming}</option>
          </select>
        </label>
        <button className="primary-btn toolbar-btn" type="submit" disabled={busy}>{t.treasury.itemAdd}</button>
      </form>

      <p className="muted">{t.treasury.itemBuiltInHint}</p>
      <div className="history-scroll">
        <table className="data-table compact">
          <thead>
            <tr>
              <th>{t.treasury.itemName}</th>
              <th>{t.treasury.itemKind}</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {/* Hidden built-in items are old-data labels only; there is nothing the owner can do with them. */}
            {items.categories.filter((item) => item.isActive || !item.isSystem).map((item) => {
              const draft = names[item.id] ?? item.name
              const changed = draft.trim() !== item.name
              return (
                <tr key={item.id} className={item.isActive ? undefined : 'row-muted'}>
                  <td>
                    <input
                      value={draft}
                      maxLength={64}
                      onChange={(event) => setNames((current) => ({ ...current, [item.id]: event.target.value }))}
                    />
                  </td>
                  <td>
                    {kindLabel(item)}
                    {item.isSystem ? <span className="muted"> · {t.treasury.itemBuiltIn}</span> : null}
                    {item.isActive ? null : <span className="muted"> · {t.treasury.itemHidden}</span>}
                  </td>
                  <td className="row-actions">
                    {changed ? (
                      <button type="button" className="btn-secondary" disabled={busy || !draft.trim()} onClick={() => save(item, item.isActive)}>
                        {t.save}
                      </button>
                    ) : null}
                    {item.isSystem ? null : (
                      <button type="button" className="btn-ghost" disabled={busy} onClick={() => save(item, !item.isActive)}>
                        {item.isActive ? t.treasury.itemHide : t.treasury.itemShow}
                      </button>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </div>
  )
}
