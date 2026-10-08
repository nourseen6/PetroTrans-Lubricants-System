import { useEffect, useState, type FormEvent } from 'react'
import {
  backupDb,
  fetchAudit,
  fetchCompany,
  fetchInvoiceSeries,
  fetchLogos,
  fetchPaymentMethods,
  fetchWarehouses,
  restoreDb,
  saveCompany,
  saveInvoiceSeries,
  savePaymentMethod,
  saveWarehouse,
  type AuditRow,
  type NamedLookup,
} from '../api/operations'
import { createCustomerType, fetchCustomerTypes, updateCustomerType, type CustomerType } from '../api/catalog'
import { useLocale } from '../i18n/LocaleContext'
import { EmptyState, ErrorBanner } from '../ui/Feedback'
import { auditLabel, formatDate } from '../ui/format'
import { APP_VERSION } from '../version'

export function SettingsPage() {
  const { t } = useLocale()
  const [error, setError] = useState<unknown>(null)
  const [ok, setOk] = useState<string | null>(null)
  const [types, setTypes] = useState<CustomerType[]>([])
  const [typeName, setTypeName] = useState('')
  const [warehouses, setWarehouses] = useState<NamedLookup[]>([])
  const [warehouseName, setWarehouseName] = useState('')
  const [methods, setMethods] = useState<NamedLookup[]>([])
  const [methodName, setMethodName] = useState('')
  const [prefix, setPrefix] = useState('')
  const [padding, setPadding] = useState('4')
  const [companyName, setCompanyName] = useState('')
  const [logoPath, setLogoPath] = useState('')
  const [logos, setLogos] = useState<Array<{ relativePath: string; url: string }>>([])
  const [backupPath, setBackupPath] = useState('')
  const [restorePath, setRestorePath] = useState('')
  const [audit, setAudit] = useState<AuditRow[]>([])
  const [hasSeries, setHasSeries] = useState(false)

  async function refresh() {
    const [typeList, warehouseList, methodList, series, company, logoList, auditRows] = await Promise.all([
      fetchCustomerTypes(false),
      fetchWarehouses(),
      fetchPaymentMethods(false),
      fetchInvoiceSeries(),
      fetchCompany(),
      fetchLogos(),
      fetchAudit(''),
    ])
    setTypes(typeList)
    setWarehouses(warehouseList)
    setMethods(methodList)
    setPrefix(series?.prefix ?? '')
    setPadding(String(series?.padding ?? 4))
    setHasSeries(Boolean(series?.prefix))
    setCompanyName(company.companyName)
    setLogoPath(company.logoRelativePath ?? '')
    setLogos(logoList)
    setAudit(auditRows)
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [])

  const ready = warehouses.some((row) => row.isActive) && methods.some((row) => row.isActive) && hasSeries

  async function submitLookup(event: FormEvent, action: () => Promise<unknown>, reset: () => void, success?: string) {
    event.preventDefault()
    setError(null)
    setOk(null)
    try {
      await action()
      reset()
      await refresh()
      if (success) {
        setOk(success)
      }
    } catch (err) {
      setError(err)
    }
  }

  return (
    <section>
      <h1 className="page-title">{t.nav.settings}</h1>
      <div className={ready ? 'setup-card ok' : 'setup-card warn'}>
        <strong>{t.settings.setupStatus}</strong>
        <div className="setup-flags">
          <span>{companyName ? '✓' : '⚠'} {t.settings.company}</span>
          <span>{warehouses.length ? '✓' : '⚠'} {warehouses.length ? `${warehouses.length} ${t.settings.warehouses}` : t.settings.warehouses}</span>
          <span>{hasSeries ? '✓' : '⚠'} {t.settings.numbering}</span>
          <span>{methods.length ? '✓' : '⚠'} {t.settings.paymentMethods}</span>
        </div>
        <p className="muted">{ready ? t.settings.setupReady : t.settings.setupHint}</p>
      </div>
      <ErrorBanner error={error} onRetry={() => void refresh()} />
      {ok ? <p className="banner ok">{ok}</p> : null}

      <section className="settings-block">
        <h2 className="editor-title">1. {t.settings.company}</h2>
        <form className="editor" onSubmit={(event) => void submitLookup(event, () => saveCompany({ companyName, logoRelativePath: logoPath || null, defaultLocale: 'ar' }), () => undefined)}>
          <label className="field">
            <span>{t.settings.companyName}</span>
            <input value={companyName} onChange={(event) => setCompanyName(event.target.value)} required />
          </label>
          <label className="field">
            <span>{t.settings.logo}</span>
            <select value={logoPath} onChange={(event) => setLogoPath(event.target.value)}>
              <option value="">{t.products.noImage}</option>
              {logos.map((logo) => (
                <option key={logo.relativePath} value={logo.relativePath}>{logo.relativePath.split(/[/\\]/).pop()}</option>
              ))}
            </select>
          </label>
          <button className="primary-btn toolbar-btn" type="submit">{t.save}</button>
        </form>
      </section>

      <section className="settings-block">
        <h2 className="editor-title">2. {t.settings.sectionCustomers}</h2>
        <p className="muted">{t.settings.customerTypesHint}</p>
        <form className="toolbar" onSubmit={(event) => void submitLookup(event, () => createCustomerType(typeName, true), () => setTypeName(''))}>
          <input value={typeName} onChange={(event) => setTypeName(event.target.value)} placeholder={t.settings.typeName} required />
          <button className="primary-btn toolbar-btn" type="submit">{t.settings.addType}</button>
        </form>
        {types.length === 0 ? <EmptyState title={t.settings.noTypes} /> : <LookupTable rows={types} onToggle={(row) => updateCustomerType(row.id, row.name, !row.isActive)} t={t} />}
      </section>

      <section className="settings-block">
        <h2 className="editor-title">3. {t.settings.warehouses}</h2>
        <form className="toolbar" onSubmit={(event) => void submitLookup(event, () => saveWarehouse(null, warehouseName, true), () => setWarehouseName(''))}>
          <input value={warehouseName} onChange={(event) => setWarehouseName(event.target.value)} placeholder={t.settings.warehouseName} required />
          <button className="primary-btn toolbar-btn" type="submit">{t.settings.addWarehouse}</button>
        </form>
        {warehouses.length === 0 ? <EmptyState title={t.settings.noWarehouses} hint={t.settings.setupHint} /> : <LookupTable rows={warehouses} onToggle={(row) => saveWarehouse(row.id, row.name, !row.isActive)} t={t} />}
      </section>

      <section className="settings-block">
        <h2 className="editor-title">4. {t.settings.sectionPay}</h2>
        <form className="toolbar" onSubmit={(event) => void submitLookup(event, () => savePaymentMethod(null, methodName, true), () => setMethodName(''))}>
          <input value={methodName} onChange={(event) => setMethodName(event.target.value)} placeholder={t.settings.methodName} required />
          <button className="primary-btn toolbar-btn" type="submit">{t.settings.addMethod}</button>
        </form>
        {methods.length === 0 ? <EmptyState title={t.settings.noMethods} /> : <LookupTable rows={methods} onToggle={(row) => savePaymentMethod(row.id, row.name, !row.isActive)} t={t} />}
      </section>

      <section className="settings-block">
        <h2 className="editor-title">5. {t.settings.sectionInvoices}</h2>
        <p className="muted">{t.settings.numberingHint}</p>
        <form className="toolbar" onSubmit={(event) => void submitLookup(event, () => saveInvoiceSeries(prefix, Number(padding)), () => undefined)}>
          <input value={prefix} onChange={(event) => setPrefix(event.target.value)} placeholder={t.settings.prefix} required />
          <input value={padding} onChange={(event) => setPadding(event.target.value)} placeholder={t.settings.padding} required />
          <button className="primary-btn toolbar-btn" type="submit">{t.save}</button>
        </form>
      </section>

      <section className="settings-block">
        <h2 className="editor-title">6. {t.settings.backup}</h2>
        <form className="toolbar" onSubmit={(event) => void submitLookup(event, () => backupDb(backupPath), () => undefined, t.settings.backupOk)}>
          <input value={backupPath} onChange={(event) => setBackupPath(event.target.value)} placeholder={t.settings.backupPath} required />
          <button className="primary-btn toolbar-btn" type="submit">{t.settings.backupAction}</button>
        </form>
        <form className="toolbar" onSubmit={(event) => {
          event.preventDefault()
          if (!window.confirm(t.settings.restoreConfirm)) {
            return
          }
          void submitLookup(event, () => restoreDb(restorePath, true), () => undefined)
        }}>
          <input value={restorePath} onChange={(event) => setRestorePath(event.target.value)} placeholder={t.settings.restore} required />
          <button type="submit" className="btn-danger">{t.settings.restore}</button>
        </form>
      </section>

      <section className="settings-block">
        <h2 className="editor-title">7. {t.settings.audit}</h2>
        {audit.length === 0 ? <p className="muted">{t.ops.emptyReports}</p> : (
          <table className="data-table compact">
            <thead>
              <tr>
                <th>الوقت</th>
                <th>المستخدم</th>
                <th>العملية</th>
              </tr>
            </thead>
            <tbody>
              {audit.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.occurredAt)}</td>
                  <td>{row.userName ?? '—'}</td>
                  <td>{auditLabel(row.action)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      <section className="settings-block">
        <h2 className="editor-title">{t.settings.about}</h2>
        <div className="panel-card">
          <p className="page-title">{t.appName}</p>
          <p className="muted">{t.settings.version} {APP_VERSION}</p>
        </div>
      </section>
    </section>
  )
}

function LookupTable({
  rows,
  onToggle,
  t,
}: {
  rows: Array<{ id: string; name: string; isActive: boolean }>
  onToggle: (row: { id: string; name: string; isActive: boolean }) => Promise<unknown>
  t: { yes: string; no: string; settings: { deactivate: string; activate: string } }
}) {
  return (
    <table className="data-table compact">
      <thead>
        <tr>
          <th>الاسم</th>
          <th />
        </tr>
      </thead>
      <tbody>
        {rows.map((row) => (
          <tr key={row.id}>
            <td>{row.name}</td>
            <td>
              <button type="button" className="btn-ghost" onClick={() => void onToggle(row)}>
                {row.isActive ? t.settings.deactivate : t.settings.activate}
              </button>
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}
