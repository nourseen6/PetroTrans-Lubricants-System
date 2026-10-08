import { useState, type FormEvent } from 'react'
import { setupAccounts, type AccountSetupInput, type AuthUser } from '../api/auth'
import { useLocale } from '../i18n/LocaleContext'
import { friendlyError } from '../ui/format'

type SetupPageProps = {
  logoSrc: string | null
  onReady: (user: AuthUser) => void
}

const emptyAccount: AccountSetupInput = {
  displayName: '',
  userName: '',
  password: '',
  confirmPassword: '',
}

export function SetupPage({ logoSrc, onReady }: SetupPageProps) {
  const { t } = useLocale()
  const [owner, setOwner] = useState(emptyAccount)
  const [operator, setOperator] = useState(emptyAccount)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const user = await setupAccounts(owner, operator)
      onReady(user)
    } catch (err) {
      setError(friendlyError(err, t.auth.genericError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card auth-card-wide" onSubmit={onSubmit}>
        <div className="auth-brand">
          {logoSrc ? <img src={logoSrc} alt={t.appName} /> : <span className="auth-fallback">{t.appName}</span>}
        </div>
        <h1 className="page-title">{t.auth.setupTitle}</h1>
        <p className="muted">{t.auth.setupHint}</p>
        <div className="setup-grid">
          <AccountFields title={t.auth.ownerAccount} value={owner} onChange={setOwner} />
          <AccountFields title={t.auth.operatorAccount} value={operator} onChange={setOperator} />
        </div>
        {error ? <p className="banner error">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={busy}>
          {busy ? t.loading : t.auth.setupAction}
        </button>
      </form>
    </div>
  )
}

type AccountFieldsProps = {
  title: string
  value: AccountSetupInput
  onChange: (next: AccountSetupInput) => void
}

function AccountFields({ title, value, onChange }: AccountFieldsProps) {
  const { t } = useLocale()
  return (
    <fieldset className="account-fields">
      <legend>{title}</legend>
      <label className="field">
        <span>{t.auth.displayName}</span>
        <input
          value={value.displayName}
          onChange={(event) => onChange({ ...value, displayName: event.target.value })}
          required
        />
      </label>
      <label className="field">
        <span>{t.auth.userName}</span>
        <input
          value={value.userName}
          onChange={(event) => onChange({ ...value, userName: event.target.value })}
          autoComplete="off"
          required
        />
      </label>
      <label className="field">
        <span>{t.auth.password}</span>
        <input
          type="password"
          value={value.password}
          onChange={(event) => onChange({ ...value, password: event.target.value })}
          autoComplete="new-password"
          required
        />
      </label>
      <label className="field">
        <span>{t.auth.confirmPassword}</span>
        <input
          type="password"
          value={value.confirmPassword}
          onChange={(event) => onChange({ ...value, confirmPassword: event.target.value })}
          autoComplete="new-password"
          required
        />
      </label>
    </fieldset>
  )
}
