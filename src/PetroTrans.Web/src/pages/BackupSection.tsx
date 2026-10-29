import { useEffect, useState, type FormEvent } from 'react'
import { backupDb, backupNow, fetchBackupStatus, restoreDb, saveBackupSettings, type BackupFile, type BackupStatus } from '../api/operations'
import { useLocale } from '../i18n/LocaleContext'
import { ConfirmDialog, ErrorBanner } from '../ui/Feedback'

const TWO_DAYS_MS = 2 * 24 * 60 * 60 * 1000

function formatDateTime(value: string | null | undefined) {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('ar-EG', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function formatSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}

function useBackupWarning(status: BackupStatus | null) {
  const { t } = useLocale()
  if (!status) return null
  if (!status.liveDatabaseOk) return t.settings.backupWarnLive
  if (!status.lastSuccessAt) return t.settings.backupWarnNever
  if (Date.now() - new Date(status.lastSuccessAt).getTime() > TWO_DAYS_MS) return t.settings.backupWarnOld
  if (status.secondaryFolder && status.lastSecondaryError) return t.settings.backupWarnSecond
  return null
}

/** Shown on the home screen only when backups need attention. */
export function BackupWarning({ onOpen }: { onOpen: () => void }) {
  const { t } = useLocale()
  const [status, setStatus] = useState<BackupStatus | null>(null)
  useEffect(() => {
    void fetchBackupStatus().then(setStatus).catch(() => undefined)
  }, [])
  const warning = useBackupWarning(status)
  if (!warning) return null
  return (
    <p className={status?.liveDatabaseOk === false ? 'banner error' : 'banner warn'}>
      {warning}{' '}
      <button type="button" className="link-btn" onClick={onOpen}>{t.settings.backupWarnOpen}</button>
    </p>
  )
}

export function BackupSection() {
  const { t } = useLocale()
  const [status, setStatus] = useState<BackupStatus | null>(null)
  const [folder, setFolder] = useState('')
  const [copyPath, setCopyPath] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [ok, setOk] = useState<string | null>(null)
  const [restoreTarget, setRestoreTarget] = useState<BackupFile | null>(null)
  const warning = useBackupWarning(status)

  async function refresh() {
    const next = await fetchBackupStatus()
    setStatus(next)
    setFolder(next.secondaryFolder ?? '')
  }

  useEffect(() => {
    void refresh().catch((err: unknown) => setError(err))
  }, [])

  async function run(action: () => Promise<unknown>, success: string) {
    setBusy(true)
    setError(null)
    setOk(null)
    try {
      await action()
      await refresh()
      setOk(success)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function reasonLabel(reason: string) {
    if (reason.startsWith('startup')) return t.settings.reasonStartup
    if (reason.startsWith('close')) return t.settings.reasonClose
    if (reason.startsWith('daily')) return t.settings.reasonDaily
    if (reason.startsWith('before-restore')) return t.settings.reasonBeforeRestore
    return t.settings.reasonManual
  }

  function saveFolder(value: string) {
    void run(() => saveBackupSettings(value.trim() || null), t.settings.backupOk)
  }

  function onSaveFolder(event: FormEvent) {
    event.preventDefault()
    saveFolder(folder)
  }

  return (
    <div>
      <p className="muted">{t.settings.autoBackupHint}</p>
      <ErrorBanner error={error} onRetry={() => void refresh()} />
      {ok ? <p className="banner ok">{ok}</p> : null}
      {warning ? <p className={status?.liveDatabaseOk === false ? 'banner error' : 'banner warn'}>{warning}</p> : null}
      {status?.lastError ? <p className="banner error">{status.lastError}</p> : null}
      {status?.lastSecondaryError ? <p className="banner warn">{status.lastSecondaryError}</p> : null}

      <div className="profile-kpi-grid">
        <div className="kpi-card">
          <span className="kpi-label">{t.settings.lastBackup}</span>
          <span className="kpi-value">{status?.lastSuccessAt ? formatDateTime(status.lastSuccessAt) : t.settings.neverBackedUp}</span>
        </div>
        <div className="kpi-card">
          <span className="kpi-label">{t.settings.lastSecondCopy}</span>
          <span className="kpi-value">{status?.secondaryFolder ? formatDateTime(status.lastSecondaryAt) : '—'}</span>
        </div>
      </div>
      <button type="button" className="primary-btn toolbar-btn" disabled={busy} onClick={() => void run(backupNow, t.settings.backupOk)}>
        {busy ? t.loading : t.settings.backupNow}
      </button>

      <form className="editor" onSubmit={onSaveFolder}>
        <label className="field">
          <span>{t.settings.secondFolder}</span>
          <input value={folder} onChange={(event) => setFolder(event.target.value)} placeholder="G:\My Drive" dir="ltr" />
        </label>
        <p className="muted">{status?.secondaryFolder ? t.settings.secondFolderHint : t.settings.secondFolderNone}</p>
        <div className="toolbar">
          <button className="btn-secondary" type="submit" disabled={busy}>{t.save}</button>
          {status?.suggestedSecondaryFolder ? (
            <button
              type="button"
              className="btn-secondary"
              disabled={busy}
              onClick={() => saveFolder(status.suggestedSecondaryFolder ?? '')}
            >
              {t.settings.useGoogleDrive} ({status.suggestedSecondaryFolder})
            </button>
          ) : null}
        </div>
      </form>

      <h3 className="editor-title">{t.settings.backupList}</h3>
      {status && status.backups.length > 0 ? (
        <div className="history-scroll">
          <table className="data-table compact">
            <thead>
              <tr>
                <th>{t.sales.date}</th>
                <th>{t.treasury.category}</th>
                <th>KB</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {status.backups.map((file) => (
                <tr key={file.fullPath}>
                  <td>{formatDateTime(file.createdAt)}</td>
                  <td>{reasonLabel(file.reason)}</td>
                  <td>{formatSize(file.sizeBytes)}</td>
                  <td>
                    <button type="button" className="link-btn" disabled={busy} onClick={() => setRestoreTarget(file)}>
                      {t.settings.restoreThis}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="muted">{t.settings.neverBackedUp}</p>
      )}

      <form
        className="toolbar"
        onSubmit={(event) => {
          event.preventDefault()
          void run(() => backupDb(copyPath), t.settings.backupOk)
        }}
      >
        <label className="field">
          <span>{t.settings.copyToPath}</span>
          <input value={copyPath} onChange={(event) => setCopyPath(event.target.value)} dir="ltr" required />
        </label>
        <button className="btn-secondary toolbar-btn" type="submit" disabled={busy}>{t.settings.backupAction}</button>
      </form>

      {restoreTarget ? (
        <ConfirmDialog
          title={t.settings.restoreThis}
          body={`${formatDateTime(restoreTarget.createdAt)} — ${t.settings.restoreThisConfirm}`}
          confirmLabel={t.settings.restoreThis}
          onConfirm={() => {
            const target = restoreTarget
            setRestoreTarget(null)
            void run(() => restoreDb(target.fullPath, true), t.settings.restoreDone)
          }}
          onCancel={() => setRestoreTarget(null)}
        />
      ) : null}
    </div>
  )
}
