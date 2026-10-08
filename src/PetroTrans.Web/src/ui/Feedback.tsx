import { friendlyError } from './format'

export function ErrorBanner({
  error,
  onRetry,
}: {
  error: unknown
  onRetry?: () => void
}) {
  if (!error) {
    return null
  }
  return (
    <p className="banner error">
      {friendlyError(error)}
      {onRetry ? (
        <>
          {' '}
          <button type="button" className="link-btn" onClick={onRetry}>
            إعادة المحاولة
          </button>
        </>
      ) : null}
    </p>
  )
}

export function EmptyState({
  title,
  hint,
  actionLabel,
  onAction,
}: {
  title: string
  hint?: string
  actionLabel?: string
  onAction?: () => void
}) {
  return (
    <div className="empty-state">
      <p>{title}</p>
      {hint ? <p className="muted">{hint}</p> : null}
      {actionLabel && onAction ? (
        <button type="button" className="primary-btn toolbar-btn" onClick={onAction}>
          {actionLabel}
        </button>
      ) : null}
    </div>
  )
}

export function StatusBadge({ kind, children }: { kind: 'draft' | 'posted' | 'paid' | 'partial' | 'unpaid' | 'warn'; children: string }) {
  return <span className={`badge badge-${kind}`}>{children}</span>
}

export function ConfirmDialog({
  title,
  body,
  confirmLabel,
  onConfirm,
  onCancel,
}: {
  title: string
  body: string
  confirmLabel: string
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <div className="dialog-backdrop">
      <div className="dialog" role="dialog">
        <h2 className="editor-title">{title}</h2>
        <p>{body}</p>
        <div className="dialog-actions">
          <button type="button" className="primary-btn toolbar-btn" onClick={onConfirm}>
            {confirmLabel}
          </button>
          <button type="button" className="btn-secondary" onClick={onCancel}>
            إلغاء
          </button>
        </div>
      </div>
    </div>
  )
}
