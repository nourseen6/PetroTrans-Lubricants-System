import { useLocale } from '../i18n/LocaleContext'
import { routes, type RouteId } from '../navigation/routes'

type SidebarProps = {
  current: RouteId
  onNavigate: (path: string) => void
}

export function Sidebar({ current, onNavigate }: SidebarProps) {
  const { t } = useLocale()

  return (
    <nav className="sidebar" aria-label={t.appName}>
      {routes.map((item) => (
        <button
          key={item.id}
          type="button"
          className={current === item.id ? 'nav-btn active' : 'nav-btn'}
          onClick={() => onNavigate(item.path)}
        >
          {t.nav[item.id]}
        </button>
      ))}
    </nav>
  )
}
