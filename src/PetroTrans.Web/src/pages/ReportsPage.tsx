import { useEffect, useState, type FormEvent } from 'react'
import { fetchCustomers, searchVariants, type CustomerListItem, type VariantPick } from '../api/catalog'
import { fetchReport, fetchStatement, type Report, type Statement } from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { EmptyState, ErrorBanner } from '../ui/Feedback'
import { displayKpiValue, displayReportCell, formatDate, formatMoney } from '../ui/format'

const types = [
  ['profit', 'مكسب الشركة'],
  ['product-track', 'تتبع الأصناف'],
  ['customer-balances', 'كشف حساب العملاء'],
  ['sales', 'المبيعات'],
  ['invoice-status', 'حالة الفواتير'],
  ['unpaid', 'فواتير غير مسددة'],
  ['payments', 'الدفعات'],
  ['inventory', 'المخزون'],
  ['movements', 'حركة المخزون'],
  ['purchasing', 'المشتريات'],
  ['supplier-balances', 'أرصدة الموردين'],
] as const

export function ReportsPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const { t } = useLocale()
  const initialType = typeof window !== 'undefined' ? new URLSearchParams(window.location.search).get('type') ?? 'sales' : 'sales'
  const [type, setType] = useState(initialType)
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [customerId, setCustomerId] = useState('')
  const [variantId, setVariantId] = useState('')
  const [customers, setCustomers] = useState<CustomerListItem[]>([])
  const [variants, setVariants] = useState<VariantPick[]>([])
  const [report, setReport] = useState<Report | null>(null)
  const [statement, setStatement] = useState<Statement | null>(null)
  const [error, setError] = useState<unknown>(null)

  async function loadLookups() {
    const [customerList, variantList] = await Promise.all([fetchCustomers(''), searchVariants('')])
    setCustomers(customerList)
    setVariants(variantList)
  }

  async function load(event?: FormEvent) {
    event?.preventDefault()
    setError(null)
    try {
      const params: Record<string, string> = {}
      if (from) params.from = from
      if (to) params.to = to
      if (customerId) params.customerId = customerId
      if (variantId) params.variantId = variantId
      setReport(await fetchReport(type, params))
      setStatement(customerId ? await fetchStatement(customerId) : null)
    } catch (err) {
      setError(err)
    }
  }

  useEffect(() => {
    void loadLookups().catch((err: unknown) => setError(err))
    void load()
  }, [])

  return (
    <section>
      <h1 className="page-title">{t.nav.reports}</h1>
      <p className="muted">{t.ops.noProfit}</p>
      <ErrorBanner error={error} onRetry={() => void load()} />
      <form className="toolbar" onSubmit={(event) => void load(event)}>
        <select value={type} onChange={(event) => setType(event.target.value)}>
          {types.map(([id, label]) => (
            <option key={id} value={id}>{label}</option>
          ))}
        </select>
        <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
        <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
        <select value={customerId} onChange={(event) => setCustomerId(event.target.value)}>
          <option value="">{t.sales.customer}</option>
          {customers.map((row) => <option key={row.id} value={row.id}>{row.name}</option>)}
        </select>
        <select value={variantId} onChange={(event) => setVariantId(event.target.value)}>
          <option value="">{t.sales.product}</option>
          {variants.map((row) => (
            <option key={row.id} value={row.id}>
              {row.productName} — {row.packagingType} {row.packagingSize}
            </option>
          ))}
        </select>
        <button className="primary-btn toolbar-btn" type="submit">{t.ops.reports}</button>
        <button
          className="btn-secondary toolbar-btn icon-btn"
          type="button"
          onClick={() => {
            const params = new URLSearchParams()
            params.set('type', type)
            if (from) params.set('from', from)
            if (to) params.set('to', to)
            if (customerId) params.set('customerId', customerId)
            if (variantId) params.set('variantId', variantId)
            onNavigate(`/print/report?${params}`)
          }}
        >
          {t.ops.print}
        </button>
        <button
          className="btn-secondary toolbar-btn"
          type="button"
          onClick={() => {
            if (!report) return
            const csv = [report.columns.join(','), ...report.rows.map((row) => row.map((cell) => `"${(cell ?? '').replaceAll('"', '""')}"`).join(','))].join('\n')
            const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
            const url = URL.createObjectURL(blob)
            const link = document.createElement('a')
            link.href = url
            link.download = `${report.title}.csv`
            link.click()
            URL.revokeObjectURL(url)
          }}
        >
          {t.ops.exportCsv}
        </button>
      </form>
      {report ? (
        <>
          <h2 className="editor-title">{report.title}</h2>
          {report.note ? <p className="muted">{report.note}</p> : null}
          {report.kpis && report.kpis.length > 0 ? (
            <div className="home-grid profit-kpis">
              {report.kpis.map((kpi) => (
                <div key={kpi.label} className="kpi-card">
                  <span className="kpi-label">{kpi.label}</span>
                  <span className="kpi-value">{displayKpiValue(kpi.label, kpi.value)}</span>
                  {kpi.hint ? <small className="muted">{kpi.hint}</small> : null}
                </div>
              ))}
            </div>
          ) : null}
          {report.rows.length === 0 ? (
            <EmptyState title={t.ops.emptyReports} />
          ) : (
            <ReportTable report={report} />
          )}
          {report.sections?.map((section) => (
            <div key={section.title}>
              <h2 className="editor-title">{section.title}</h2>
              <ReportTable report={section} />
            </div>
          ))}
        </>
      ) : null}
      {statement ? (
        <>
          <h2 className="editor-title">{t.ops.statement}: {statement.customerName}</h2>
          <p>{t.ops.customerRemaining}: <strong>{formatMoney(statement.outstanding)}</strong></p>
          {statement.lines.length === 0 ? (
            <EmptyState title={t.ops.emptyReports} />
          ) : (
            <table className="data-table compact">
              <thead>
                <tr>
                  <th>{t.sales.date}</th>
                  <th>{t.customers.document}</th>
                  <th>{t.customers.description}</th>
                  <th>{t.customers.debit}</th>
                  <th>{t.customers.credit}</th>
                  <th>{t.customers.running}</th>
                </tr>
              </thead>
              <tbody>
                {statement.lines.map((line) => (
                  <tr key={line.sourceDocumentId + line.occurredAt}>
                    <td>{formatDate(line.occurredAt)}</td>
                    <td>{line.document}</td>
                    <td>{line.description}</td>
                    <td>{formatMoney(line.debit)}</td>
                    <td>{formatMoney(line.credit)}</td>
                    <td>{formatMoney(line.runningBalance)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      ) : null}
    </section>
  )
}

function ReportTable({ report }: { report: Report }) {
  if (report.rows.length === 0) {
    return null
  }
  return (
    <table className="data-table compact">
      <thead>
        <tr>{report.columns.map((col) => <th key={col}>{col}</th>)}</tr>
      </thead>
      <tbody>
        {report.rows.map((row, index) => {
          const isGroup = Boolean(row[0]) && row.slice(1).every((cell) => !cell)
          if (isGroup) {
            return (
              <tr key={index} className="category-row">
                <td colSpan={row.length}>{row[0]}</td>
              </tr>
            )
          }
          const isTotal = row[1] === 'الإجمالي'
          return (
            <tr key={index} className={isTotal ? 'category-row' : undefined}>
              {row.map((cell, cellIndex) => (
                <td key={cellIndex}>{displayReportCell(report.columns[cellIndex] ?? '', cell)}</td>
              ))}
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
