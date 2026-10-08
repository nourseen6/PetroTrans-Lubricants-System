import { api } from './auth'

export type AssistantChoice = { id: string; label: string }
export type AssistantAmbiguity = { field: string; message: string; choices: AssistantChoice[] }
export type AssistantDraftLine = {
  variantId: string | null
  productQuery: string
  quantity: number
  unitPrice: number | null
  packagingHint: string | null
}

export type AssistantDraft = {
  draftId: string
  intentType: string
  status: string
  summaryAr: string
  customerId: string | null
  customerName: string | null
  invoiceDate: string | null
  lines: AssistantDraftLine[]
  discountAmount: number | null
  manualTotal: number | null
  manualTotalReason: string | null
  estimatedTotal: number | null
  warnings: string[]
  ambiguities: AssistantAmbiguity[]
  canApprove: boolean
  actionLabelAr?: string | null
  amount?: number | null
  actionPayload?: { path?: string; invoiceId?: string; paymentId?: string; customerId?: string } | null
}

export type AssistantApproveResult = {
  draftId: string
  status: string
  createdEntityId: string | null
  messageAr: string
}

export function parseAssistant(text: string): Promise<AssistantDraft> {
  return api('/api/assistant/parse', {
    method: 'POST',
    body: JSON.stringify({ text, locale: 'ar' }),
  })
}

export function approveAssistantDraft(id: string): Promise<AssistantApproveResult> {
  return api(`/api/assistant/drafts/${id}/approve`, { method: 'POST' })
}

export function rejectAssistantDraft(id: string): Promise<AssistantApproveResult> {
  return api(`/api/assistant/drafts/${id}/reject`, { method: 'POST' })
}
