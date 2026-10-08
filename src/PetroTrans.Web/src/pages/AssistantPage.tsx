import { useEffect, useRef, useState, type FormEvent } from 'react'
import type { AuthUser } from '../api/auth'
import { approveAssistantDraft, parseAssistant, rejectAssistantDraft, type AssistantDraft } from '../api/assistant'
import { useLocale } from '../i18n/LocaleContext'
import { ErrorBanner } from '../ui/Feedback'
import { formatMoney } from '../ui/format'

type BrowserSpeechRecognition = {
  lang: string
  interimResults: boolean
  continuous: boolean
  onresult: ((event: { results: ArrayLike<ArrayLike<{ transcript: string }>> }) => void) | null
  onerror: (() => void) | null
  onend: (() => void) | null
  start: () => void
  stop: () => void
}

type BrowserSpeechRecognitionCtor = new () => BrowserSpeechRecognition

function getSpeechRecognition(): BrowserSpeechRecognitionCtor | null {
  const w = window as unknown as {
    SpeechRecognition?: BrowserSpeechRecognitionCtor
    webkitSpeechRecognition?: BrowserSpeechRecognitionCtor
  }
  return w.SpeechRecognition ?? w.webkitSpeechRecognition ?? null
}

type AssistantPageProps = {
  user: AuthUser
  onNavigate: (path: string) => void
}

type ChatItem = { role: 'user' | 'assistant'; text: string }

export function AssistantPage({ user, onNavigate }: AssistantPageProps) {
  const { t } = useLocale()
  const [text, setText] = useState('')
  const [chat, setChat] = useState<ChatItem[]>([
    { role: 'assistant', text: t.assistant.welcome },
  ])
  const [draft, setDraft] = useState<AssistantDraft | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const [recording, setRecording] = useState(false)
  const [seconds, setSeconds] = useState(0)
  const recognitionRef = useRef<BrowserSpeechRecognition | null>(null)
  const timerRef = useRef<number | null>(null)

  useEffect(() => {
    return () => {
      recognitionRef.current?.stop()
      if (timerRef.current) window.clearInterval(timerRef.current)
    }
  }, [])

  async function onSubmit(event: FormEvent) {
    event.preventDefault()
    const prompt = text.trim()
    if (!prompt) return
    setBusy(true)
    setError(null)
    setChat((current) => [...current, { role: 'user', text: prompt }])
    setText('')
    try {
      const preview = await parseAssistant(prompt)
      setDraft(preview)
      setChat((current) => [
        ...current,
        {
          role: 'assistant',
          text: preview.canApprove
            ? t.assistant.draftReady
            : preview.ambiguities[0]?.message ?? t.assistant.needClarify,
        },
      ])
    } catch (err) {
      const message = err instanceof Error && err.message.trim() ? err.message : t.assistant.unavailable
      setError(err)
      setChat((current) => [...current, { role: 'assistant', text: message }])
    } finally {
      setBusy(false)
    }
  }

  async function onApprove() {
    if (!draft) return
    setBusy(true)
    setError(null)
    try {
      const result = await approveAssistantDraft(draft.draftId)
      setChat((current) => [...current, { role: 'assistant', text: result.messageAr }])
      if (result.createdEntityId) {
        if (draft.intentType === 'create_customer' || draft.intentType === 'archive_customer') {
          onNavigate(`/customers/${result.createdEntityId}`)
        } else if (draft.intentType === 'record_payment') {
          onNavigate(`/print/payment/${result.createdEntityId}`)
        } else {
          onNavigate(`/sales?id=${result.createdEntityId}`)
        }
      }
      setDraft(null)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  async function onReject() {
    if (!draft) return
    setBusy(true)
    setError(null)
    try {
      const result = await rejectAssistantDraft(draft.draftId)
      setChat((current) => [...current, { role: 'assistant', text: result.messageAr }])
      setDraft(null)
    } catch (err) {
      setError(err)
    } finally {
      setBusy(false)
    }
  }

  function startVoice() {
    const SpeechRecognitionCtor = getSpeechRecognition()
    if (!SpeechRecognitionCtor) {
      setError(t.assistant.voiceUnsupported)
      return
    }
    const recognition = new SpeechRecognitionCtor()
    recognition.lang = 'ar-EG'
    recognition.interimResults = true
    recognition.continuous = false
    recognition.onresult = (event) => {
      const transcript = Array.from(event.results as ArrayLike<ArrayLike<{ transcript: string }>>)
        .map((result) => result[0]?.transcript ?? '')
        .join(' ')
        .trim()
      if (transcript) {
        setText(transcript)
      }
    }
    recognition.onerror = () => {
      setRecording(false)
      setError(t.assistant.voiceFailed)
    }
    recognition.onend = () => {
      setRecording(false)
      if (timerRef.current) window.clearInterval(timerRef.current)
    }
    recognitionRef.current = recognition
    setRecording(true)
    setSeconds(0)
    timerRef.current = window.setInterval(() => setSeconds((value) => value + 1), 1000)
    recognition.start()
  }

  function stopVoice() {
    recognitionRef.current?.stop()
    setRecording(false)
    if (timerRef.current) window.clearInterval(timerRef.current)
  }

  return (
    <section>
      <div className="page-head">
        <div>
          <h1 className="page-title">{t.nav.assistant}</h1>
          <p className="muted">{t.assistant.hint}</p>
          <p className="muted">
            {user.displayName} · {t.assistant.safety}
          </p>
        </div>
      </div>
      <ErrorBanner error={error} />
      <div className="assistant-layout">
        <div className="panel-card">
          <div className="chat-box">
            {chat.map((item, index) => (
              <div key={`${item.role}-${index}`} className={`chat-bubble ${item.role}`}>
                {item.text}
              </div>
            ))}
          </div>
          <form className="toolbar" onSubmit={onSubmit}>
            <input
              value={text}
              onChange={(event) => setText(event.target.value)}
              placeholder={t.assistant.placeholder}
              disabled={busy}
            />
            <button type="submit" className="primary-btn toolbar-btn" disabled={busy || !text.trim()}>
              {t.assistant.analyze}
            </button>
          </form>
          <div className="voice-bar">
            {recording ? (
              <>
                <span className="banner warn" style={{ margin: 0 }}>
                  {t.assistant.recording} {seconds}s
                </span>
                <button type="button" className="btn-danger toolbar-btn" onClick={stopVoice}>
                  {t.assistant.stop}
                </button>
              </>
            ) : (
              <button type="button" className="btn-secondary toolbar-btn" onClick={startVoice} title={t.assistant.record}>
                {t.assistant.record}
              </button>
            )}
            <span className="muted">{t.assistant.voiceNote}</span>
          </div>
        </div>
        <aside className="draft-panel">
          <h2 className="editor-title">{t.assistant.review}</h2>
          {!draft ? (
            <p className="muted">{t.assistant.noDraft}</p>
          ) : (
            <>
              <p>
                <strong>{draft.actionLabelAr ?? (draft.canApprove ? t.assistant.intentInvoice : t.assistant.review)}</strong>
              </p>
              <p>
                {t.sales.customer}: {draft.customerName ?? '—'}
              </p>
              <ul>
                {draft.lines.map((line, index) => (
                  <li key={`${line.productQuery}-${index}`}>
                    {line.quantity} × {line.productQuery}
                    {line.unitPrice != null ? ` · ${formatMoney(line.unitPrice)}` : ''}
                  </li>
                ))}
              </ul>
              {draft.manualTotal != null ? (
                <p className="banner warn">{t.assistant.manualTotal}: {formatMoney(draft.manualTotal)}</p>
              ) : null}
              <p>
                {t.sales.goodsTotal}: {formatMoney(draft.estimatedTotal)}
              </p>
              {draft.warnings.map((warning) => (
                <p key={warning} className="muted">
                  ⚠ {warning}
                </p>
              ))}
              {draft.actionPayload?.path ? (
                <div className="action-row">
                  <button type="button" className="btn-secondary" onClick={() => onNavigate(draft.actionPayload!.path!)}>
                    فتح
                  </button>
                </div>
              ) : null}
              {draft.ambiguities.map((item) => (
                <div key={item.field} className="banner warn">
                  <p>{item.message}</p>
                  {item.choices.map((choice) => (
                    <div key={choice.id}>{choice.label}</div>
                  ))}
                </div>
              ))}
              <div className="action-row">
                {draft.canApprove ? (
                  <>
                    <button type="button" className="btn-secondary" onClick={onReject} disabled={busy}>
                      {t.cancel}
                    </button>
                    <button type="button" className="primary-btn" onClick={onApprove} disabled={busy}>
                      {t.assistant.approve}
                    </button>
                  </>
                ) : (
                  <p className="muted">{draft.summaryAr}</p>
                )}
              </div>
            </>
          )}
        </aside>
      </div>
    </section>
  )
}
