import { useLocale } from '../i18n/LocaleContext'
import { routes, type RouteId } from '../navigation/routes'
import { Icon } from '../ui/Icons'

type AppNavProps = {
  current: RouteId
  onNavigate: (path: string) => void
}

const iconByRoute = {
  home: 'home',
  sales: 'sales',
  customers: 'customers',
  products: 'products',
  pricing: 'products',
  inventory: 'inventory',
  purchasing: 'purchasing',
  treasury: 'payment',
  reports: 'reports',
  assistant: 'assistant',
  settings: 'settings',
} as const

export function AppNav({ current, onNavigate }: AppNavProps) {
  const { t } = useLocale()

  return (
    <nav className="app-nav" aria-label={t.appName}>
      {routes.map((item) => {
        const active = current === item.id
        return (
          <button
            key={item.id}
            type="button"
            className={active ? 'app-nav-btn active' : 'app-nav-btn'}
            title={t.nav[item.id]}
            aria-current={active ? 'page' : undefined}
            onClick={() => onNavigate(item.path)}
          >
            <Icon name={iconByRoute[item.id]} size={16} />
            <span>{t.nav[item.id]}</span>
          </button>
        )
      })}
    </nav>
  )
}
