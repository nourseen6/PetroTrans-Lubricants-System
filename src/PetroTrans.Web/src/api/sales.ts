import { api } from './auth'

export type InvoiceListItem = {
  id: string
  number: string | null
  invoiceDate: string
  dueDate: string | null
  customerName: string
  customerCode: string
  goodsTotal: number
  paidTotal: number
  remainingTotal: number
  status: string
  paymentStatus: string
}

export type InvoiceLine = {
  id: string
  lineNumber: number
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  quantity: number
  unitPrice: number | null
  lineTotal: number | null
  priceSource: string | null
  resolvedUnitPrice: number | null
  overrideReason: string | null
}

export type Invoice = {
  id: string
  number: string | null
  customerId: string
  customerName: string
  warehouseId: string | null
  warehouseName: string | null
  invoiceDate: string
  dueDate: string | null
  status: string
  linesSubtotal: number
  discountAmount: number
  manualAdjustment: number
  hasManualTotal: boolean
  manualTotalReason: string | null
  goodsTotal: number
  paidTotal: number
  remainingTotal: number
  paymentStatus: string
  notes: string | null
  lines: InvoiceLine[]
  createdAt: string
  updatedAt: string
  createdByName?: string | null
  updatedByName?: string | null
}

export type SaveInvoiceLine = {
  variantId: string
  quantity: number
  unitPrice: number | null
  overrideReason: string | null
}

export type SaveInvoice = {
  customerId: string
  warehouseId: string | null
  invoiceDate: string
  dueDate: string | null
  notes: string | null
  discountAmount: number
  manualTotal: number | null
  manualTotalReason: string | null
  lines: SaveInvoiceLine[]
}

export function fetchInvoices(q = '', customerId?: string, signal?: AbortSignal): Promise<InvoiceListItem[]> {
  const params = new URLSearchParams()
  if (q.trim()) params.set('q', q.trim())
  if (customerId) params.set('customerId', customerId)
  const query = params.toString()
  return api(`/api/sales/invoices${query ? `?${query}` : ''}`, { signal })
}

export function fetchInvoice(id: string): Promise<Invoice> {
  return api(`/api/sales/invoices/${id}`)
}

export function createInvoice(body: SaveInvoice): Promise<Invoice> {
  return api('/api/sales/invoices', { method: 'POST', body: JSON.stringify(body) })
}

export function updateInvoice(id: string, body: SaveInvoice): Promise<Invoice> {
  return api(`/api/sales/invoices/${id}`, { method: 'PUT', body: JSON.stringify(body) })
}

export function postInvoice(id: string): Promise<Invoice> {
  return api(`/api/sales/invoices/${id}/post`, { method: 'POST' })
}

export function unpostInvoice(id: string): Promise<Invoice> {
  return api(`/api/sales/invoices/${id}/unpost`, { method: 'POST' })
}

export function deleteInvoice(id: string): Promise<Invoice> {
  return api(`/api/sales/invoices/${id}`, { method: 'DELETE' })
}
