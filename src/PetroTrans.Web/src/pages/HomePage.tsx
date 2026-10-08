import { useEffect, useState, type KeyboardEvent, type ReactNode } from 'react'
import { fetchCustomers, fetchHomeSummary, type CustomerListItem, type HomeSummary } from '../api/catalog'
import { fetchInvoices, type InvoiceListItem } from '../api/sales'
import { fetchPayments, type PaymentRow } from '../api/operations'
import type { AuthUser } from '../api/auth'
import { useLocale } from '../i18n/LocaleContext'
import { EmptyState, ErrorBanner, StatusBadge } from '../ui/Feedback'
import { formatDate, formatMoney } from '../ui/format'
import { Icon } from '../ui/Icons'

function isAbortError(err: unknown): boolean {
  return err instanceof DOMException
    ? err.name === 'AbortError'
    : err instanceof Error && err.name === 'AbortError'
}

function KpiCard({ onClick, children }: { onClick: () => void; children: ReactNode }) {
  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault()
      onClick()
    }
  }

  return (
    <div className="kpi-card action" role="button" tabIndex={0} onClick={onClick} onKeyDown={onKeyDown}>
      {children}
    </div>
  )
}

type HomePageProps = {
  user: AuthUser
  onNavigate: (path: string) => void
}

export function HomePage({ user, onNavigate }: HomePageProps) {
  const { t } = useLocale()
  const [summary, setSummary] = useState<HomeSummary | null>(null)
  const [recentInvoices, setRecentInvoices] = useState<InvoiceListItem[]>([])
  const [recentPayments, setRecentPayments] = useState<PaymentRow[]>([])
  const [owing, setOwing] = useState<CustomerListItem[]>([])
  const [error, setError] = useState<unknown>(null)

  function load() {
    const controller = new AbortController()
    Promise.all([
      fetchHomeSummary(controller.signal),
      fetchInvoices('', undefined, controller.signal),
      fetchPayments(),
      fetchCustomers('', false, controller.signal),
    ])
      .then(([home, invoices, payments, customers]) => {
        setSummary(home)
        setRecentInvoices(invoices.slice(0, 6))
        setRecentPayments(payments.slice(0, 6))
        setOwing(
          [...customers]
            .filter((row) => row.outstanding > 0)
            .sort((a, b) => b.outstanding - a.outstanding)
            .slice(0, 6),
        )
      })
      .catch((err: unknown) => {
        if (!isAbortError(err)) {
          setError(err)
        }
      })
    return controller
  }

  useEffect(() => {
    const controller = load()
    return () => controller.abort()
  }, [])

  return (
    <section className="page">
      <div className="page-head">
        <div>
          <h1 className="page-title">{t.nav.home}</h1>
          <p className="muted">{t.home.now}</p>
          <p className="muted">
            {t.home.signedInAs} <strong>{user.displayName}</strong>
          </p>
        </div>
      </div>
      <ErrorBanner error={error} onRetry={() => { setError(null); load() }} />

      <div className="quick-actions">
        <button type="button" className="primary-btn toolbar-btn icon-btn" onClick={() => onNavigate('/sales')}>
          <Icon name="add" />
          {t.home.addInvoice}
        </button>
        <button type="button" className="btn-secondary toolbar-btn icon-btn" onClick={() => onNavigate('/customers')}>
          <Icon name="customers" />
          {t.home.addCustomer}
        </button>
        <button type="button" className="btn-secondary toolbar-btn icon-btn" onClick={() => onNavigate('/assistant')}>
          <Icon name="assistant" />
          {t.nav.assistant}
        </button>
        <button type="button" className="btn-secondary toolbar-btn icon-btn" onClick={() => onNavigate('/purchasing')}>
          <Icon name="purchasing" />
          {t.nav.purchasing}
        </button>
      </div>

      {summary ? (
        <>
          <div className="home-grid">
            <KpiCard onClick={() => onNavigate('/sales')}>
              <span className="kpi-label">{t.home.salesTotal}</span>
              <span className="kpi-value">{formatMoney(summary.salesTotal)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/sales')}>
              <span className="kpi-label">{t.home.todaySales}</span>
              <span className="kpi-value">{formatMoney(summary.todaySales)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/sales')}>
              <span className="kpi-label">{t.home.todayCollections}</span>
              <span className="kpi-value">{formatMoney(summary.todayCollections)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/customers?outstanding=1')}>
              <span className="kpi-label">{t.home.customerReceivables}</span>
              <span className="kpi-value">{formatMoney(summary.customerReceivables)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/purchasing')}>
              <span className="kpi-label">{t.home.adnocPayable}</span>
              <span className="kpi-value">{formatMoney(summary.adnocPayable)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/inventory')}>
              <span className="kpi-label">{t.home.inventoryValue}</span>
              <span className="kpi-value">{formatMoney(summary.inventoryValue)}</span>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/sales?unpaid=1')}>
              <span className="kpi-label">{t.home.outstandingInvoices}</span>
              <span className="kpi-value">{summary.unpaidInvoiceCount}</span>
              <small className="muted">
                {summary.draftInvoiceCount} {t.home.drafts}
              </small>
            </KpiCard>
            <KpiCard onClick={() => onNavigate('/inventory')}>
              <span className="kpi-label">{t.home.lowStock}</span>
              <span className="kpi-value">{summary.lowStockCount}</span>
            </KpiCard>
          </div>

          <div className="home-panels">
            <section className="panel-card">
              <div className="page-head">
                <h2 className="editor-title">{t.home.recentInvoices}</h2>
                <button type="button" className="link-btn" onClick={() => onNavigate('/sales')}>
                  {t.home.viewAll}
                </button>
              </div>
              {recentInvoices.length === 0 ? (
                <EmptyState title={t.home.noInvoices} />
              ) : (
                <ul className="timeline">
                  {recentInvoices.map((row) => (
                    <li key={row.id} className="timeline-item clickable" onClick={() => onNavigate(`/sales?id=${row.id}`)}>
                      <span className="timeline-dot" />
                      <div>
                        <strong>
                          {row.number ?? t.sales.numberPending} — {row.customerName}
                        </strong>
                        <p className="muted">
                          {formatDate(row.invoiceDate)} · {formatMoney(row.goodsTotal)} ·{' '}
                          <StatusBadge kind={row.status === 'posted' ? 'posted' : 'draft'}>
                            {row.status === 'posted' ? t.sales.posted : t.sales.draft}
                          </StatusBadge>
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section className="panel-card">
              <div className="page-head">
                <h2 className="editor-title">{t.home.recentPayments}</h2>
              </div>
              {recentPayments.length === 0 ? (
                <EmptyState title={t.home.noPayments} />
              ) : (
                <ul className="timeline">
                  {recentPayments.map((row) => (
                    <li key={row.id} className="timeline-item">
                      <span className="timeline-dot" />
                      <div>
                        <strong>
                          {row.customerName} · {formatMoney(row.amount)}
                        </strong>
                        <p className="muted">
                          {formatDate(row.paidOn)} · {row.invoiceNumber ?? t.customers.onAccount} · {row.methodName}
                        </p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>

            <section className="panel-card">
              <div className="page-head">
                <h2 className="editor-title">{t.home.owingCustomers}</h2>
                <button type="button" className="link-btn" onClick={() => onNavigate('/customers')}>
                  {t.home.viewAll}
                </button>
              </div>
              {owing.length === 0 ? (
                <EmptyState title={t.home.noOwing} />
              ) : (
                <ul className="timeline">
                  {owing.map((row) => (
                    <li key={row.id} className="timeline-item clickable" onClick={() => onNavigate(`/customers/${row.id}`)}>
                      <span className="timeline-dot" />
                      <div>
                        <strong>{row.name}</strong>
                        <p className="muted money-warn">{formatMoney(row.outstanding)}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        </>
      ) : error ? null : (
        <p className="muted">{t.loading}</p>
      )}
    </section>
  )
}
