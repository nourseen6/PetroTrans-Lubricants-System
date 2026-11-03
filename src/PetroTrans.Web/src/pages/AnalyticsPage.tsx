import { useEffect, useState } from 'react'
import { fetchAnalytics, type Analytics, type AnalyticsMonth } from '../api/analytics'
import { EmptyState, ErrorBanner } from '../ui/Feedback'
import { formatDate, formatMoney, todayIso } from '../ui/format'

const MONTHS = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر']
// A customer who has not bought for this many days is shown as "stopped buying".
const INACTIVE_DAYS = 60

type Preset = 'month' | '3months' | 'year' | '12months' | 'all' | 'custom'

function iso(date: Date) {
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

function presetRange(preset: Preset): { from: string; to: string } {
  const today = new Date()
  switch (preset) {
    case 'month': return { from: iso(new Date(today.getFullYear(), today.getMonth(), 1)), to: todayIso() }
    case '3months': return { from: iso(new Date(today.getFullYear(), today.getMonth() - 2, 1)), to: todayIso() }
    case 'year': return { from: iso(new Date(today.getFullYear(), 0, 1)), to: todayIso() }
    case '12months': return { from: iso(new Date(today.getFullYear(), today.getMonth() - 11, 1)), to: todayIso() }
    default: return { from: '', to: '' }
  }
}

function monthName(row: AnalyticsMonth) {
  return `${MONTHS[row.month - 1]} ${row.year}`
}

function compact(value: number) {
  return new Intl.NumberFormat('ar-EG', { notation: 'compact', maximumFractionDigits: 1 }).format(value)
}

/**
 * The owner's overview: is the company making money, which months, products and customers
 * drive it, and what is not selling. Every number comes from /api/analytics, which uses the
 * same rules as the profit report.
 */
export function AnalyticsPage() {
  const [preset, setPreset] = useState<Preset>('12months')
  const [from, setFrom] = useState(() => presetRange('12months').from)
  const [to, setTo] = useState(() => presetRange('12months').to)
  const [data, setData] = useState<Analytics | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [reload, setReload] = useState(0)

  useEffect(() => {
    setError(null)
    void fetchAnalytics(from || undefined, to || undefined).then(setData).catch((err: unknown) => setError(err))
  }, [from, to, reload])

  function choose(next: Preset) {
    setPreset(next)
    if (next === 'custom') return
    const range = presetRange(next)
    setFrom(range.from)
    setTo(range.to)
  }

  const totals = data?.totals
  const earning = (totals?.netProfit ?? 0) >= 0
  const activeMonths = data?.months.filter((row) => row.sales !== 0 || row.expenses !== 0) ?? []
  const bestMonth = activeMonths.length ? activeMonths.reduce((a, b) => (b.netProfit > a.netProfit ? b : a)) : null
  const worstMonth = activeMonths.length > 1 ? activeMonths.reduce((a, b) => (b.netProfit < a.netProfit ? b : a)) : null
  const byQuantity = [...(data?.products ?? [])].sort((a, b) => b.quantity - a.quantity)
  const byDemand = [...(data?.products ?? [])].sort((a, b) => b.invoices - a.invoices)
  const byProfit = [...(data?.products ?? [])].filter((row) => row.profit != null).sort((a, b) => (b.profit ?? 0) - (a.profit ?? 0))
  const losing = byProfit.filter((row) => (row.profit ?? 0) < 0)
  const neverSold = data?.unsold.filter((row) => row.lastSold == null) ?? []
  const notThisPeriod = data?.unsold.filter((row) => row.lastSold != null) ?? []
  const customers = data?.customers ?? []
  const frequent = [...customers].sort((a, b) => b.invoices - a.invoices)
  // Big, regular buyers who pay: a discount here keeps good business.
  const discount = customers.filter((row) => row.invoices >= 2 && row.outstanding <= row.sales * 0.1).slice(0, 5)
  const stopped = customers.filter((row) => row.daysSinceLastPurchase > INACTIVE_DAYS)
  const owing = [...customers].filter((row) => row.outstanding > 0).sort((a, b) => b.outstanding - a.outstanding)

  return (
    <section className="page analytics">
      <div className="page-head">
        <div>
          <h1 className="page-title">التحليلات</h1>
          <p className="muted">الشركة بتكسب ولا بتخسر، وأكتر الأصناف والعملاء، وإيه اللي مش بيتباع.</p>
        </div>
      </div>

      <div className="toolbar panel-card">
        <div className="segmented" role="group" aria-label="الفترة">
          {([
            ['month', 'الشهر ده'],
            ['3months', 'آخر 3 شهور'],
            ['year', 'السنة دي'],
            ['12months', 'آخر 12 شهر'],
            ['all', 'كل الفترات'],
            ['custom', 'فترة أخرى'],
          ] as Array<[Preset, string]>).map(([key, label]) => (
            <button key={key} type="button" className={preset === key ? 'segment active' : 'segment'} onClick={() => choose(key)}>
              {label}
            </button>
          ))}
        </div>
        {preset === 'custom' ? (
          <>
            <label className="field">
              <span>من تاريخ</span>
              <input type="date" value={from} onChange={(event) => setFrom(event.target.value)} />
            </label>
            <label className="field">
              <span>إلى تاريخ</span>
              <input type="date" value={to} onChange={(event) => setTo(event.target.value)} />
            </label>
          </>
        ) : null}
      </div>

      <ErrorBanner error={error} onRetry={() => setReload((value) => value + 1)} />
      {!data || !totals ? (
        <p className="muted">جاري التحميل…</p>
      ) : (
        <>
          {/* The answer first, in one sentence and one colour. */}
          <div className={earning ? 'verdict verdict-up' : 'verdict verdict-down'}>
            <span className="verdict-icon" aria-hidden="true">{earning ? '▲' : '▼'}</span>
            <div>
              <strong>{earning ? 'الشركة كسبانة' : 'الشركة خسرانة'} {formatMoney(Math.abs(totals.netProfit))}</strong>
              <span>
                {data.from || data.to
                  ? `من ${data.from ? formatDate(data.from) : 'البداية'} إلى ${data.to ? formatDate(data.to) : 'النهارده'}`
                  : 'من أول ما بدأ البرنامج'}
                {totals.sales > 0 ? ` — صافي الربح ${Math.round((totals.netProfit / totals.sales) * 100)}٪ من المبيعات` : ''}
              </span>
            </div>
          </div>

          <div className="profile-kpi-grid">
            <Kpi label="المبيعات" value={formatMoney(totals.sales)} note={`${totals.invoices} فاتورة — متوسط ${formatMoney(totals.averageInvoice)}`} />
            <Kpi label="تكلفة البضاعة" value={formatMoney(totals.cost)} note="بسعر الشركة" />
            <Kpi label="مجمل الربح" value={formatMoney(totals.grossProfit)} note="المبيعات − التكلفة" tone={totals.grossProfit >= 0 ? 'up' : 'down'} />
            <Kpi label="المصروفات" value={formatMoney(totals.expenses)} note="مرتبات، إيجار، عربيات…" />
            <Kpi label="صافي الربح" value={formatMoney(totals.netProfit)} note="مجمل الربح − المصروفات" tone={earning ? 'up' : 'down'} />
            <Kpi label="المتبقي على العملاء" value={formatMoney(totals.receivables)} note={`اتحصّل في الفترة ${formatMoney(totals.collections)}`} />
          </div>

          <section className="panel-card">
            <h2 className="editor-title">صافي الربح كل شهر</h2>
            {bestMonth || worstMonth ? (
              <p className="insight-line">
                {bestMonth ? <span className={bestMonth.netProfit >= 0 ? 'chip chip-up' : 'chip'}>{bestMonth.netProfit >= 0 ? 'أحسن شهر' : 'أقل شهر خسارة'}: {monthName(bestMonth)} ({formatMoney(bestMonth.netProfit)})</span> : null}
                {worstMonth && worstMonth !== bestMonth ? (
                  <span className={worstMonth.netProfit < 0 ? 'chip chip-down' : 'chip'}>
                    {worstMonth.netProfit < 0 ? 'أكتر شهر خسارة' : 'أضعف شهر'}: {monthName(worstMonth)} ({formatMoney(worstMonth.netProfit)})
                  </span>
                ) : null}
              </p>
            ) : null}
            <ProfitChart months={data.months} best={bestMonth} worst={worstMonth} />
            <div className="chart-legend">
              <span><i className="swatch swatch-up" /> شهر كسبان</span>
              <span><i className="swatch swatch-down" /> شهر خسران</span>
            </div>
          </section>

          <section className="panel-card">
            <h2 className="editor-title">المبيعات والمصروفات كل شهر</h2>
            <SalesSpendChart months={data.months} />
            <div className="chart-legend">
              <span><i className="swatch swatch-sales" /> المبيعات</span>
              <span><i className="swatch swatch-spend" /> التكلفة + المصروفات</span>
            </div>
          </section>

          <div className="analytics-grid">
            <section className="panel-card">
              <h2 className="editor-title">أكتر الأصناف بيعاً (بالكمية)</h2>
              <Bars rows={byQuantity.slice(0, 8).map((row) => ({ key: row.variantId, label: `${row.name} — ${row.pack}`, value: row.quantity, text: `${row.quantity.toLocaleString('ar-EG')} عبوة` }))} empty="مفيش مبيعات في الفترة دي." />
            </section>
            <section className="panel-card">
              <h2 className="editor-title">أكتر الأصناف الناس بتطلبها (عدد الفواتير)</h2>
              <Bars rows={byDemand.slice(0, 8).map((row) => ({ key: row.variantId, label: `${row.name} — ${row.pack}`, value: row.invoices, text: `${row.invoices} فاتورة · ${row.customers} عميل` }))} empty="مفيش مبيعات في الفترة دي." />
            </section>
            <section className="panel-card">
              <h2 className="editor-title">أكتر الأصناف مكسباً</h2>
              <Bars tone="up" rows={byProfit.filter((row) => (row.profit ?? 0) > 0).slice(0, 8).map((row) => ({ key: row.variantId, label: `${row.name} — ${row.pack}`, value: row.profit ?? 0, text: `${formatMoney(row.profit)} (${row.marginPercent ?? 0}٪)` }))} empty="مفيش أصناف ليها سعر شركة عشان نحسب مكسبها." />
              {losing.length ? (
                <p className="banner warn">أصناف بتتباع بخسارة (سعر البيع أقل من سعر الشركة): {losing.map((row) => `${row.name} — ${row.pack}`).join('، ')}</p>
              ) : null}
            </section>
            <section className="panel-card">
              <h2 className="editor-title">أصناف ما اتباعتش في الفترة</h2>
              {neverSold.length + notThisPeriod.length === 0 ? (
                <EmptyState title="كل الأصناف اتباعت في الفترة دي." />
              ) : (
                <div className="history-scroll">
                  <table className="data-table compact">
                    <thead>
                      <tr>
                        <th>الصنف</th>
                        <th>الموجود في المخزن</th>
                        <th>آخر مرة اتباع</th>
                      </tr>
                    </thead>
                    <tbody>
                      {[...neverSold, ...notThisPeriod].slice(0, 15).map((row) => (
                        <tr key={row.variantId}>
                          <td>{row.name} — {row.pack}</td>
                          <td>{row.onHand.toLocaleString('ar-EG')}</td>
                          <td>{row.lastSold ? formatDate(row.lastSold) : <span className="chip chip-down">عمره ما اتباع</span>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </section>
          </div>

          <div className="analytics-grid">
            <section className="panel-card">
              <h2 className="editor-title">أكبر العملاء (بالمبيعات)</h2>
              <Bars rows={customers.slice(0, 8).map((row) => ({ key: row.customerId, label: row.name, value: row.sales, text: compact(row.sales) }))} empty="مفيش عملاء اشتروا في الفترة دي." />
            </section>
            <section className="panel-card">
              <h2 className="editor-title">العملاء اللي بيطلبوا كتير (عدد الفواتير)</h2>
              <Bars rows={frequent.slice(0, 8).map((row) => ({ key: row.customerId, label: row.name, value: row.invoices, text: `${row.invoices} فاتورة · متوسط ${compact(row.averageInvoice)}` }))} empty="مفيش عملاء اشتروا في الفترة دي." />
            </section>
            <section className="panel-card">
              <h2 className="editor-title">عملاء يستاهلوا خصم</h2>
              <p className="muted">بيشتروا كتير، أكتر من فاتورة، والمتبقي عليهم قليل.</p>
              {discount.length === 0 ? (
                <EmptyState title="مفيش عميل ينطبق عليه ده في الفترة دي." />
              ) : (
                <ul className="plain-list">
                  {discount.map((row) => (
                    <li key={row.customerId}>
                      <strong>{row.name}</strong> — {formatMoney(row.sales)} في {row.invoices} فواتير
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <section className="panel-card">
              <h2 className="editor-title">محتاجين متابعة</h2>
              {stopped.length === 0 && owing.length === 0 ? (
                <EmptyState title="مفيش حاجة محتاجة متابعة." />
              ) : (
                <ul className="plain-list">
                  {stopped.slice(0, 5).map((row) => (
                    <li key={`s-${row.customerId}`}>
                      <span className="chip chip-down">بطّل يشتري</span> <strong>{row.name}</strong> — آخر فاتورة من {row.daysSinceLastPurchase} يوم
                    </li>
                  ))}
                  {owing.slice(0, 5).map((row) => (
                    <li key={`o-${row.customerId}`}>
                      <span className="chip">عليه فلوس</span> <strong>{row.name}</strong> — {formatMoney(row.outstanding)}
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>

          <section className="panel-card">
            <h2 className="editor-title">المصروفات بتروح فين</h2>
            <Bars tone="spend" rows={data.expenses.slice(0, 10).map((row) => ({ key: row.code, label: row.name, value: row.amount, text: `${formatMoney(row.amount)} (${totals.expenses ? Math.round((row.amount / totals.expenses) * 100) : 0}٪)` }))} empty="مفيش مصروفات في الفترة دي." />
          </section>

          <p className="muted analytics-note">
            التكلفة محسوبة بسعر الشركة الحالي على كل عبوة. المصروفات = كل اللي طلع من الخزينة والبنك ما عدا سداد الموردين، والإيداع والسحب بين الخزينة والبنك، والمسحوبات الشخصية، وسلف الموظفين.
            {data.linesWithoutCost > 0 ? ` فيه ${data.linesWithoutCost} سطر في الفواتير لأصناف من غير سعر شركة، فمكسبها مش محسوب — حط سعر الشركة من صفحة الأسعار.` : ''}
          </p>
        </>
      )}
    </section>
  )
}

function Kpi({ label, value, note, tone }: { label: string; value: string; note: string; tone?: 'up' | 'down' }) {
  return (
    <div className={tone ? `kpi-card kpi-${tone}` : 'kpi-card'}>
      <span className="kpi-label">{label}</span>
      <span className="kpi-value">{value}</span>
      <small className="muted">{note}</small>
    </div>
  )
}

type BarRow = { key: string; label: string; value: number; text: string }

/** Horizontal bars: one series, longest first, the value written beside each bar. */
function Bars({ rows, empty, tone = 'sales' }: { rows: BarRow[]; empty: string; tone?: 'sales' | 'up' | 'spend' }) {
  if (rows.length === 0) return <EmptyState title={empty} />
  const max = Math.max(...rows.map((row) => Math.abs(row.value)), 1)
  return (
    <ul className="hbars">
      {rows.map((row) => (
        <li key={row.key} title={`${row.label}: ${row.text}`}>
          <span className="hbar-label">{row.label}</span>
          <span className="hbar-track">
            <span className={`hbar-fill hbar-${tone}`} style={{ width: `${Math.max(2, (Math.abs(row.value) / max) * 100)}%` }} />
          </span>
          <span className="hbar-value">{row.text}</span>
        </li>
      ))}
    </ul>
  )
}

const CHART_W = 760
const CHART_H = 220
const PAD_X = 8
const PAD_TOP = 22
const PAD_BOTTOM = 34

/** Net profit per month: bars above the zero line for profit, below it for loss, best/worst labelled. */
function ProfitChart({ months, best, worst }: { months: AnalyticsMonth[]; best: AnalyticsMonth | null; worst: AnalyticsMonth | null }) {
  if (months.length === 0) return <EmptyState title="مفيش بيانات." />
  const max = Math.max(...months.map((row) => row.netProfit), 0)
  const min = Math.min(...months.map((row) => row.netProfit), 0)
  const span = max - min || 1
  const plotH = CHART_H - PAD_TOP - PAD_BOTTOM
  const zeroY = PAD_TOP + (max / span) * plotH
  const slot = (CHART_W - PAD_X * 2) / months.length
  const barW = Math.min(36, slot * 0.6)
  return (
    <svg className="chart" viewBox={`0 0 ${CHART_W} ${CHART_H}`} role="img" aria-label="صافي الربح كل شهر">
      <line x1={PAD_X} x2={CHART_W - PAD_X} y1={zeroY} y2={zeroY} className="chart-axis" />
      {months.map((row, position) => {
        // Arabic reads right to left, so the oldest month sits on the right.
        const index = months.length - 1 - position
        const height = Math.max(row.netProfit === 0 ? 0 : 2, (Math.abs(row.netProfit) / span) * plotH)
        const x = PAD_X + slot * index + (slot - barW) / 2
        const up = row.netProfit >= 0
        const y = up ? zeroY - height : zeroY
        const labelled = row === best || row === worst
        return (
          <g key={`${row.year}-${row.month}`}>
            <title>{`${monthName(row)}: ${up ? 'مكسب' : 'خسارة'} ${formatMoney(Math.abs(row.netProfit))} — مبيعات ${formatMoney(row.sales)}`}</title>
            <rect x={PAD_X + slot * index} y={PAD_TOP} width={slot} height={plotH} className="chart-hit" />
            <rect x={x} y={y} width={barW} height={height} rx={4} className={up ? 'bar-up' : 'bar-down'} />
            {labelled && row.netProfit !== 0 ? (
              <text x={x + barW / 2} y={up ? y - 6 : zeroY - 6} textAnchor="middle" className="chart-value">{compact(row.netProfit)}</text>
            ) : null}
            <text x={PAD_X + slot * index + slot / 2} y={CHART_H - 12} textAnchor="middle" className="chart-tick">
              {MONTHS[row.month - 1]}
            </text>
          </g>
        )
      })}
    </svg>
  )
}

/** Sales next to cost + expenses per month, on one shared scale. */
function SalesSpendChart({ months }: { months: AnalyticsMonth[] }) {
  if (months.length === 0) return <EmptyState title="مفيش بيانات." />
  const spend = (row: AnalyticsMonth) => row.cost + row.expenses
  const max = Math.max(...months.map((row) => Math.max(row.sales, spend(row))), 1)
  const plotH = CHART_H - PAD_TOP - PAD_BOTTOM
  const baseY = PAD_TOP + plotH
  const slot = (CHART_W - PAD_X * 2) / months.length
  const barW = Math.min(18, slot * 0.32)
  return (
    <svg className="chart" viewBox={`0 0 ${CHART_W} ${CHART_H}`} role="img" aria-label="المبيعات والمصروفات كل شهر">
      <line x1={PAD_X} x2={CHART_W - PAD_X} y1={baseY} y2={baseY} className="chart-axis" />
      {months.map((row, position) => {
        // Arabic reads right to left, so the oldest month sits on the right.
        const index = months.length - 1 - position
        const center = PAD_X + slot * index + slot / 2
        const salesH = Math.max(row.sales ? 2 : 0, (row.sales / max) * plotH)
        const spendH = Math.max(spend(row) ? 2 : 0, (spend(row) / max) * plotH)
        return (
          <g key={`${row.year}-${row.month}`}>
            <title>{`${monthName(row)}: مبيعات ${formatMoney(row.sales)} — تكلفة ومصروفات ${formatMoney(spend(row))}`}</title>
            <rect x={PAD_X + slot * index} y={PAD_TOP} width={slot} height={plotH} className="chart-hit" />
            <rect x={center - barW - 1} y={baseY - salesH} width={barW} height={salesH} rx={4} className="bar-sales" />
            <rect x={center + 1} y={baseY - spendH} width={barW} height={spendH} rx={4} className="bar-spend" />
            <text x={center} y={CHART_H - 12} textAnchor="middle" className="chart-tick">{MONTHS[row.month - 1]}</text>
          </g>
        )
      })}
    </svg>
  )
}
