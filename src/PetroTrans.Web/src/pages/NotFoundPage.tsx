import { useLocale } from '../i18n/LocaleContext'

export function NotFoundPage() {
  const { t } = useLocale()
  return (
    <section>
      <h1 className="page-title">{t.notFound}</h1>
    </section>
  )
}
