import { useLocale } from '../i18n/LocaleContext'
import type { NavId } from '../navigation/routes'

type PlaceholderPageProps = {
  routeId: Exclude<NavId, 'home'>
}

export function PlaceholderPage({ routeId }: PlaceholderPageProps) {
  const { t } = useLocale()
  return (
    <section>
      <h1 className="page-title">{t.nav[routeId]}</h1>
      <p className="muted">{t.comingSoon}</p>
    </section>
  )
}
