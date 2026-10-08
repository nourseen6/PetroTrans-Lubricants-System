import { useLocale } from '../i18n/LocaleContext'
import { AppNav } from './AppNav'
import { Icon } from '../ui/Icons'
import type { RouteId } from '../navigation/routes'
import { APP_VERSION } from '../version'

type TopBarProps = {
  theme: 'light' | 'dark'
  onToggleTheme: () => void
  onLogout: () => void
  logoSrc: string | null
  userName: string
  current: RouteId
  onNavigate: (path: string) => void
}

export function TopBar({
  theme,
  onToggleTheme,
  onLogout,
  logoSrc,
  userName,
  current,
  onNavigate,
}: TopBarProps) {
  const { t, toggleLocale } = useLocale()

  return (
    <header className="topbar">
      <div className="topbar-main">
        <div className="brand">
          {logoSrc ? <img src={logoSrc} alt={t.appName} /> : <span className="brand-fallback">{t.appName}</span>}
          {logoSrc ? <span className="brand-text">{t.appName}</span> : null}
          <span className="brand-version">{APP_VERSION}</span>
        </div>
        <div className="history-nav" role="group" aria-label={t.history.label}>
          <button type="button" className="btn-ghost toolbar-chip" title={t.history.back} onClick={() => window.history.back()}>
            <Icon name="back" size={16} />
            <span>{t.history.back}</span>
          </button>
          <button type="button" className="btn-ghost toolbar-chip" title={t.history.forward} onClick={() => window.history.forward()}>
            <Icon name="forward" size={16} />
            <span>{t.history.forward}</span>
          </button>
          <button type="button" className="btn-ghost toolbar-chip" title={t.history.home} onClick={() => onNavigate('/')}>
            <Icon name="home" size={16} />
            <span>{t.history.home}</span>
          </button>
        </div>
        <AppNav current={current} onNavigate={onNavigate} />
        <div className="topbar-actions">
          <span className="user-chip" title={userName}>
            {userName}
          </span>
          <button type="button" className="btn-ghost toolbar-chip" onClick={toggleLocale} title={t.language}>
            {t.language}
          </button>
          <button
            type="button"
            className="btn-ghost toolbar-chip"
            onClick={onToggleTheme}
            title={theme === 'dark' ? t.themeLight : t.themeDark}
          >
            {theme === 'dark' ? t.themeLight : t.themeDark}
          </button>
          <button type="button" className="btn-secondary toolbar-chip" onClick={onLogout}>
            {t.auth.logout}
          </button>
        </div>
      </div>
    </header>
  )
}
