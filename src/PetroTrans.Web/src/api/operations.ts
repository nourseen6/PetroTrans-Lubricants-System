import { api } from './auth'

export type NamedLookup = { id: string; name: string; isActive: boolean }
export type NumberSeries = { documentType: string; prefix: string; padding: number; nextValue: number }
export type CompanySettings = { companyName: string; logoRelativePath: string | null; logoUrl: string | null; defaultLocale: string }
export type StockRow = {
  warehouseId: string
  warehouseName: string
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  onHand: number
  minStock: number | null
  belowMin: boolean
  category?: string | null
  companyCost?: number | null
  stockValue?: number | null
}
export type MovementRow = {
  id: string
  occurredAt: string
  warehouseName: string
  productName: string
  packagingType: string
  packagingSize: string
  movementType: string
  direction: string
  quantity: number
  sourceDocumentType: string
  notes: string | null
}
export type Supplier = { id: string; code: string; name: string; phone: string | null; address: string | null; isActive: boolean; outstanding: number }
export type Receipt = {
  id: string
  number: string | null
  supplierId: string
  supplierName: string
  warehouseId: string
  documentDate: string
  status: string
  notes: string | null
  lines: Array<{ variantId: string; productName: string; packagingType: string; packagingSize: string; quantity: number }>
}
export type PurchaseBill = {
  id: string
  number: string | null
  supplierId: string
  supplierName: string
  invoiceDate: string
  status: string
  goodsTotal: number
  notes: string | null
  lines: Array<{ variantId: string; productName: string; packagingType: string; packagingSize: string; quantity: number; unitPrice: number; lineTotal: number }>
  linesSubtotal?: number
  paidTotal?: number
  remainingTotal?: number
  paymentStatus?: string
  createdAt?: string
  updatedAt?: string
  createdByName?: string | null
  updatedByName?: string | null
}
export type PaymentMethod = NamedLookup
export type PaymentRow = { id: string; paidOn: string; customerName: string; invoiceNumber: string | null; amount: number; methodName: string; reference: string | null }
export type SupplierPaymentRow = { id: string; paidOn: string; supplierName: string; amount: number; methodName: string; reference: string | null }
export type SalesReturnDoc = {
  id: string
  number: string | null
  customerId: string
  customerName: string
  originalSalesInvoiceId: string | null
  warehouseId: string
  returnDate: string
  status: string
  balancePostingStatus: string
  notes: string | null
  lines: Array<{ variantId: string; productName: string; packagingType: string; packagingSize: string; quantity: number }>
}
export type ReturnableLine = {
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  soldQty: number
  alreadyReturnedQty: number
  returnableQty: number
  unitPrice: number
}
export type ReturnableInvoice = {
  invoiceId: string
  number: string | null
  customerId: string
  warehouseId: string
  warehouseName: string
  lines: ReturnableLine[]
}
export type AuditRow = {
  id: string
  occurredAt: string
  userName: string | null
  action: string
  entityType: string | null
  entityId: string | null
  beforeJson: string | null
  afterJson: string | null
}
export type ReportKpi = { label: string; value: string; hint?: string | null }
export type Report = {
  title: string
  columns: string[]
  rows: Array<Array<string | null>>
  kpis?: ReportKpi[] | null
  note?: string | null
  sections?: Report[] | null
}
export type Statement = {
  customerId: string
  customerName: string
  customerCode?: string | null
  phone?: string | null
  from?: string | null
  to?: string | null
  openingBalance: number
  totalDebits: number
  totalCredits: number
  closingBalance: number
  outstanding: number
  lines: Array<{
    occurredAt: string
    document: string
    description: string
    debit: number
    credit: number
    runningBalance: number
    sourceDocumentId: string
    entryType: string
  }>
}

export type PaymentDetail = {
  id: string
  customerId: string
  invoiceId: string | null
  paymentMethodId: string
  amount: number
  paidOn: string
  reference: string | null
  notes: string | null
  customerName: string | null
  invoiceNumber: string | null
  methodName: string | null
  allocations?: Array<{ invoiceId: string; invoiceNumber: string | null; amount: number }>
}

export const fetchActiveWarehouses = () => api<NamedLookup[]>('/api/warehouses/active')
export const fetchWarehouses = () => api<NamedLookup[]>('/api/warehouses')
export const saveWarehouse = (id: string | null, name: string, isActive: boolean) =>
  api<NamedLookup>(id ? `/api/warehouses/${id}` : '/api/warehouses', { method: id ? 'PUT' : 'POST', body: JSON.stringify({ name, isActive }) })

export const fetchPaymentMethods = (activeOnly = false) => api<PaymentMethod[]>(`/api/payment-methods?activeOnly=${activeOnly}`)
export const savePaymentMethod = (id: string | null, name: string, isActive: boolean) =>
  api<PaymentMethod>(id ? `/api/payment-methods/${id}` : '/api/payment-methods', { method: id ? 'PUT' : 'POST', body: JSON.stringify({ name, isActive }) })

export const fetchCompany = () => api<CompanySettings>('/api/settings/company')
export const saveCompany = (body: { companyName: string; logoRelativePath: string | null; defaultLocale: string }) =>
  api<CompanySettings>('/api/settings/company', { method: 'PUT', body: JSON.stringify(body) })
export const fetchInvoiceSeries = () => api<NumberSeries | null>('/api/settings/invoice-series')
export const saveInvoiceSeries = (prefix: string, padding: number) =>
  api<NumberSeries>('/api/settings/invoice-series', { method: 'PUT', body: JSON.stringify({ prefix, padding }) })
export const fetchLogos = () => api<Array<{ relativePath: string; url: string }>>('/api/settings/logos')
export const fetchAudit = (q = '') => api<AuditRow[]>(`/api/audit${q.trim() ? `?q=${encodeURIComponent(q.trim())}` : ''}`)
export const backupDb = (destinationPath: string) => api<string>('/api/backup', { method: 'POST', body: JSON.stringify({ destinationPath }) })
export const restoreDb = (sourcePath: string, confirm: boolean) =>
  api<string>('/api/restore', { method: 'POST', body: JSON.stringify({ sourcePath, confirm }) })

export const fetchOnHand = (warehouseId?: string) =>
  api<StockRow[]>(`/api/inventory/on-hand${warehouseId ? `?warehouseId=${warehouseId}` : ''}`)
export const fetchMovements = () => api<MovementRow[]>('/api/inventory/movements')
export type AdjustmentRow = {
  id: string
  warehouseId: string
  warehouseName: string
  direction: 'in' | 'out' | string
  reason: string
  notes: string | null
  occurredAt: string
  lines: Array<{
    variantId: string
    productName: string
    packagingType: string
    packagingSize: string
    quantity: number
  }>
}

export const fetchAdjustments = () => api<AdjustmentRow[]>('/api/inventory/adjustments')
export const createAdjustment = (body: {
  warehouseId: string
  direction: 'in' | 'out'
  reason: string
  notes: string | null
  occurredAt: string
  lines: Array<{ variantId: string; quantity: number }>
}) => api<AdjustmentRow>('/api/inventory/adjustments', { method: 'POST', body: JSON.stringify(body) })
export const updateAdjustment = (id: string, body: {
  warehouseId: string
  direction: 'in' | 'out'
  reason: string
  notes: string | null
  occurredAt: string
  lines: Array<{ variantId: string; quantity: number }>
}) => api<AdjustmentRow>(`/api/inventory/adjustments/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const deleteAdjustment = (id: string) => api<boolean>(`/api/inventory/adjustments/${id}`, { method: 'DELETE' })
export const createTransfer = (body: {
  fromWarehouseId: string
  toWarehouseId: string
  notes: string | null
  occurredAt: string
  lines: Array<{ variantId: string; quantity: number }>
}) => api('/api/inventory/transfers', { method: 'POST', body: JSON.stringify(body) })

export const fetchPayments = (customerId?: string) =>
  api<PaymentRow[]>(`/api/payments${customerId ? `?customerId=${customerId}` : ''}`)
export const fetchPayment = (id: string) => api<PaymentDetail>(`/api/payments/${id}`)
export const createPayment = (body: {
  invoiceId?: string | null
  customerId?: string | null
  paymentMethodId: string
  amount: number
  paidOn: string
  reference: string | null
  notes: string | null
  allocations?: Array<{ invoiceId: string; amount: number }>
}) => api('/api/payments', { method: 'POST', body: JSON.stringify(body) })
export const updatePayment = (id: string, body: {
  invoiceId?: string | null
  customerId?: string | null
  paymentMethodId: string
  amount: number
  paidOn: string
  reference: string | null
  notes: string | null
  allocations?: Array<{ invoiceId: string; amount: number }>
}) => api(`/api/payments/${id}`, { method: 'PUT', body: JSON.stringify(body) })
export const voidPayment = (id: string) => api(`/api/payments/${id}/void`, { method: 'POST' })

export const fetchSupplierPayments = (supplierId?: string) =>
  api<SupplierPaymentRow[]>(`/api/supplier-payments${supplierId ? `?supplierId=${supplierId}` : ''}`)
export const createSupplierPayment = (body: {
  supplierId: string
  paymentMethodId: string
  amount: number
  paidOn: string
  reference: string | null
  notes: string | null
}) => api('/api/supplier-payments', { method: 'POST', body: JSON.stringify(body) })
export const deleteSupplierPayment = (id: string) => api<boolean>(`/api/supplier-payments/${id}`, { method: 'DELETE' })

export const fetchSuppliers = (q = '') => api<Supplier[]>(`/api/suppliers${q.trim() ? `?q=${encodeURIComponent(q.trim())}` : ''}`)
export const saveSupplier = (id: string | null, body: { code: string; name: string; phone: string; address: string; isActive: boolean }) =>
  api<Supplier>(id ? `/api/suppliers/${id}` : '/api/suppliers', { method: id ? 'PUT' : 'POST', body: JSON.stringify(body) })

export const fetchReceipts = () => api<Array<{ id: string; number: string | null; documentDate: string; supplierName: string; warehouseName: string; status: string }>>('/api/goods-receipts')
export const fetchReceipt = (id: string) => api<Receipt>(`/api/goods-receipts/${id}`)
export const saveReceipt = (id: string | null, body: { supplierId: string; warehouseId: string; documentDate: string; notes: string | null; lines: Array<{ variantId: string; quantity: number }> }) =>
  api<Receipt>(id ? `/api/goods-receipts/${id}` : '/api/goods-receipts', { method: id ? 'PUT' : 'POST', body: JSON.stringify(body) })
export const postReceipt = (id: string) => api<Receipt>(`/api/goods-receipts/${id}/post`, { method: 'POST' })

export const fetchBills = () => api<Array<{ id: string; number: string | null; invoiceDate: string; supplierName: string; goodsTotal: number; status: string }>>('/api/purchase-invoices')
export const fetchBill = (id: string) => api<PurchaseBill>(`/api/purchase-invoices/${id}`)
export const saveBill = (id: string | null, body: { supplierId: string; invoiceDate: string; notes: string | null; lines: Array<{ variantId: string; quantity: number; unitPrice: number }> }) =>
  api<PurchaseBill>(id ? `/api/purchase-invoices/${id}` : '/api/purchase-invoices', { method: id ? 'PUT' : 'POST', body: JSON.stringify(body) })
export const postBill = (id: string) => api<PurchaseBill>(`/api/purchase-invoices/${id}/post`, { method: 'POST' })
export const unpostBill = (id: string) => api<PurchaseBill>(`/api/purchase-invoices/${id}/unpost`, { method: 'POST' })
export const deleteBill = (id: string) => api<PurchaseBill>(`/api/purchase-invoices/${id}`, { method: 'DELETE' })

export const fetchReturns = () => api<Array<{ id: string; returnDate: string; customerName: string; originalNumber: string | null; status: string; balancePostingStatus: string }>>('/api/sales-returns')
export const fetchReturn = (id: string) => api<SalesReturnDoc>(`/api/sales-returns/${id}`)
export const fetchReturnableInvoice = (invoiceId: string) => api<ReturnableInvoice>(`/api/sales-returns/invoices/${invoiceId}/lines`)
export const saveReturn = (id: string | null, body: { customerId: string; originalSalesInvoiceId: string | null; warehouseId: string; returnDate: string; notes: string | null; lines: Array<{ variantId: string; quantity: number }> }) =>
  api<SalesReturnDoc>(id ? `/api/sales-returns/${id}` : '/api/sales-returns', { method: id ? 'PUT' : 'POST', body: JSON.stringify(body) })
export const postReturn = (id: string) => api<SalesReturnDoc>(`/api/sales-returns/${id}/post`, { method: 'POST' })

export const fetchReport = (type: string, params: Record<string, string> = {}) => {
  const query = new URLSearchParams(params).toString()
  return api<Report>(`/api/reports/${type}${query ? `?${query}` : ''}`)
}
export const fetchStatement = (customerId: string, from?: string, to?: string) => {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  const query = params.toString() ? `?${params}` : ''
  return api<Statement>(`/api/customers/${customerId}/statement${query}`)
}

export type SupplierStatement = {
  supplierId: string
  supplierName: string
  supplierCode?: string | null
  phone?: string | null
  from?: string | null
  to?: string | null
  openingBalance: number
  totalDebits: number
  totalCredits: number
  closingBalance: number
  outstanding: number
  lines: Statement['lines']
}

export const fetchSupplierStatement = (supplierId: string, from?: string, to?: string) => {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  const query = params.toString() ? `?${params}` : ''
  return api<SupplierStatement>(`/api/suppliers/${supplierId}/statement${query}`)
}

export type TreasuryEntry = {
  id: string
  occurredOn: string
  direction: 'in' | 'out' | string
  category: string | null
  description: string
  amount: number
  notes: string | null
  runningBalance: number
  linked?: boolean
}

export type TreasuryBook = {
  from: string | null
  to: string | null
  openingBalance: number
  totalIn: number
  totalOut: number
  balance: number
  entries: TreasuryEntry[]
  filterCategory?: string | null
  categoryTotal?: number
}

export const fetchTreasury = (from?: string, to?: string, category?: string) => {
  const params = new URLSearchParams()
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (category) params.set('category', category)
  const query = params.toString() ? `?${params}` : ''
  return api<TreasuryBook>(`/api/treasury${query}`)
}

export const createTreasuryEntry = (body: {
  occurredOn: string
  direction: 'in' | 'out'
  category: string | null
  description: string
  amount: number
  notes: string | null
}) => api<TreasuryEntry>('/api/treasury', { method: 'POST', body: JSON.stringify(body) })

export const deleteTreasuryEntry = (id: string) => api<boolean>(`/api/treasury/${id}`, { method: 'DELETE' })
