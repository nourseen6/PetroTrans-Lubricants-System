import { useState, type FormEvent } from 'react'
import { login } from '../api/auth'
import type { AuthUser } from '../api/auth'
import { useLocale } from '../i18n/LocaleContext'
import { friendlyError } from '../ui/format'

type LoginPageProps = {
  logoSrc: string | null
  onLoggedIn: (user: AuthUser) => void
}

export function LoginPage({ logoSrc, onLoggedIn }: LoginPageProps) {
  const { t } = useLocale()
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const user = await login(userName, password)
      onLoggedIn(user)
    } catch (err) {
      setError(friendlyError(err, t.auth.genericError))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="auth-screen">
      <form className="auth-card" onSubmit={onSubmit}>
        <div className="auth-brand">
          {logoSrc ? <img src={logoSrc} alt={t.appName} /> : <span className="auth-fallback">{t.appName}</span>}
        </div>
        <h1 className="page-title">{t.auth.loginTitle}</h1>
        <label className="field">
          <span>{t.auth.userName}</span>
          <input
            value={userName}
            onChange={(event) => setUserName(event.target.value)}
            autoComplete="username"
            autoFocus
            required
          />
        </label>
        <label className="field">
          <span>{t.auth.password}</span>
          <input
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            autoComplete="current-password"
            required
          />
        </label>
        {error ? <p className="banner error">{error}</p> : null}
        <button className="primary-btn" type="submit" disabled={busy}>
          {busy ? t.loading : t.auth.loginAction}
        </button>
      </form>
    </div>
  )
}
