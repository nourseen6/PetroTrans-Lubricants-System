import { api } from './auth'

export type AnalyticsTotals = {
  sales: number
  cost: number
  grossProfit: number
  expenses: number
  netProfit: number
  invoices: number
  averageInvoice: number
  buyingCustomers: number
  collections: number
  receivables: number
}

export type AnalyticsMonth = { year: number; month: number; sales: number; cost: number; expenses: number; netProfit: number; invoices: number }

export type AnalyticsProduct = {
  variantId: string
  name: string
  pack: string
  quantity: number
  sales: number
  invoices: number
  customers: number
  profit: number | null
  marginPercent: number | null
  lastSold: string
}

export type AnalyticsUnsold = { variantId: string; name: string; pack: string; onHand: number; lastSold: string | null }

export type AnalyticsCustomer = {
  customerId: string
  name: string
  invoices: number
  sales: number
  profit: number | null
  averageInvoice: number
  lastPurchase: string
  daysSinceLastPurchase: number
  outstanding: number
}

export type AnalyticsExpense = { code: string; name: string; amount: number; count: number }

export type Analytics = {
  from: string | null
  to: string | null
  totals: AnalyticsTotals
  months: AnalyticsMonth[]
  products: AnalyticsProduct[]
  unsold: AnalyticsUnsold[]
  customers: AnalyticsCustomer[]
  expenses: AnalyticsExpense[]
  linesWithoutCost: number
}

export function fetchAnalytics(from?: string, to?: string): Promise<Analytics> {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  const query = params.toString()
  return api<Analytics>(`/api/analytics${query ? `?${query}` : ''}`)
}
