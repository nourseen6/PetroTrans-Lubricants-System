import { api } from './auth'

export type CustomerListItem = {
  id: string
  code: string
  name: string
  customerTypeName: string | null
  phone: string | null
  isActive: boolean
  outstanding: number
  invoiceCount: number
  lastInvoiceDate: string | null
}

export type Customer = {
  id: string
  code: string
  name: string
  customerTypeId: string | null
  customerTypeName: string | null
  contactPerson: string | null
  phone: string | null
  whatsApp: string | null
  address: string | null
  isActive: boolean
}

export type CustomerType = {
  id: string
  name: string
  isActive: boolean
}

export type SaveCustomer = {
  name: string
  customerTypeId: string | null
  contactPerson: string
  phone: string
  whatsApp: string
  address: string
}

export type Variant = {
  id: string
  productId: string
  packagingType: string
  packagingSize: string
  sku: string | null
  barcode: string | null
  minStock: number | null
  standardWholesalePrice: number | null
  isActive: boolean
  standardPurchasePrice?: number | null
}

export type ProductListItem = {
  id: string
  name: string
  brand: string | null
  isActive: boolean
  variantCount: number
  imageUrl: string | null
  category?: string | null
}

export type Product = {
  id: string
  name: string
  brand: string | null
  category: string | null
  specification: string | null
  isActive: boolean
  imageRelativePath: string | null
  imageUrl: string | null
  variants: Variant[]
}

export type SaveVariant = {
  packagingType: string
  packagingSize: string
  sku: string
  barcode: string
  minStock: number | null
  standardWholesalePrice: number | null
  isActive: boolean
  standardPurchasePrice?: number | null
}

export type HomeSummary = {
  displayName: string
  userName: string
  customerCount: number
  productCount: number
  variantCount: number
  draftInvoiceCount: number
  postedInvoiceCount: number
  unpaidInvoiceCount: number
  lowStockCount: number
  salesTotal: number
  collectionsTotal: number
  customerReceivables: number
  adnocPayable: number
  inventoryValue: number
  todaySales: number
  todayCollections: number
}

export type OfficialImage = {
  relativePath: string
  url: string
}

export type VariantPick = {
  id: string
  productId: string
  productName: string
  packagingType: string
  packagingSize: string
  standardWholesalePrice: number | null
  isActive: boolean
  customerUnitPrice?: number | null
  standardPurchasePrice?: number | null
}

export function fetchHomeSummary(signal?: AbortSignal): Promise<HomeSummary> {
  return api('/api/home/summary', { signal })
}

export function fetchCustomers(q: string, includeInactive = false, signal?: AbortSignal): Promise<CustomerListItem[]> {
  const params = new URLSearchParams()
  if (q.trim()) params.set('q', q.trim())
  if (includeInactive) params.set('includeInactive', 'true')
  const query = params.toString() ? `?${params}` : ''
  return api(`/api/customers${query}`, { signal })
}

export function fetchCustomer(id: string): Promise<Customer> {
  return api(`/api/customers/${id}`)
}

export function createCustomer(body: SaveCustomer): Promise<Customer> {
  return api('/api/customers', { method: 'POST', body: JSON.stringify(body) })
}

export function updateCustomer(id: string, body: SaveCustomer): Promise<Customer> {
  return api(`/api/customers/${id}`, { method: 'PUT', body: JSON.stringify(body) })
}

export function archiveCustomer(id: string): Promise<Customer> {
  return api(`/api/customers/${id}/archive`, { method: 'POST' })
}

export function restoreCustomer(id: string): Promise<Customer> {
  return api(`/api/customers/${id}/restore`, { method: 'POST' })
}

export function fetchCustomerTypes(activeOnly = false, signal?: AbortSignal): Promise<CustomerType[]> {
  return api(`/api/customer-types?activeOnly=${activeOnly}`, { signal })
}

export function createCustomerType(name: string, isActive = true): Promise<CustomerType> {
  return api('/api/customer-types', { method: 'POST', body: JSON.stringify({ name, isActive }) })
}

export function updateCustomerType(id: string, name: string, isActive: boolean): Promise<CustomerType> {
  return api(`/api/customer-types/${id}`, { method: 'PUT', body: JSON.stringify({ name, isActive }) })
}

export function fetchProducts(q: string, includeInactive = false, signal?: AbortSignal): Promise<ProductListItem[]> {
  const params = new URLSearchParams()
  if (q.trim()) params.set('q', q.trim())
  if (includeInactive) params.set('includeInactive', 'true')
  const query = params.toString() ? `?${params}` : ''
  return api(`/api/products${query}`, { signal })
}

export function fetchProduct(id: string): Promise<Product> {
  return api(`/api/products/${id}`)
}

export function createProduct(body: {
  name: string
  brand: string
  category: string
  specification: string
  isActive: boolean
  imageRelativePath: string | null
  variants: SaveVariant[]
}): Promise<Product> {
  return api('/api/products', { method: 'POST', body: JSON.stringify(body) })
}

export function updateProduct(
  id: string,
  body: {
    name: string
    brand: string
    category: string
    specification: string
    isActive: boolean
    imageRelativePath: string | null
  },
): Promise<Product> {
  return api(`/api/products/${id}`, { method: 'PUT', body: JSON.stringify(body) })
}

export function addVariant(productId: string, body: SaveVariant): Promise<Variant> {
  return api(`/api/products/${productId}/variants`, { method: 'POST', body: JSON.stringify(body) })
}

export function updateVariant(id: string, body: SaveVariant): Promise<Variant> {
  return api(`/api/variants/${id}`, { method: 'PUT', body: JSON.stringify(body) })
}

export function archiveProduct(id: string): Promise<Product> {
  return api(`/api/products/${id}/archive`, { method: 'POST' })
}

export function restoreProduct(id: string): Promise<Product> {
  return api(`/api/products/${id}/restore`, { method: 'POST' })
}

export function archiveVariant(id: string): Promise<Variant> {
  return api(`/api/variants/${id}/archive`, { method: 'POST' })
}

export function restoreVariant(id: string): Promise<Variant> {
  return api(`/api/variants/${id}/restore`, { method: 'POST' })
}

export type PricingMatrixRow = {
  variantId: string
  productId: string
  productName: string
  brand: string | null
  packagingType: string
  packagingSize: string
  basePrice: number | null
  customerPriceCount: number
  isActive: boolean
  purchasePrice?: number | null
  category?: string | null
}

export type CustomerPrice = {
  id: string
  customerId: string
  customerName: string
  variantId: string
  productName: string
  packagingType: string
  packagingSize: string
  unitPrice: number
}

export type PriceHistory = {
  id: string
  customerId: string
  variantId: string
  oldUnitPrice: number | null
  newUnitPrice: number | null
  changeKind: string
  reason: string | null
  changedAt: string
}

export function fetchPricingMatrix(q = ''): Promise<PricingMatrixRow[]> {
  return api(`/api/pricing/matrix${q.trim() ? `?q=${encodeURIComponent(q.trim())}` : ''}`)
}

export function fetchCustomerPrices(customerId?: string): Promise<CustomerPrice[]> {
  return api(`/api/pricing/customer-prices${customerId ? `?customerId=${customerId}` : ''}`)
}

export function setCustomerPrice(body: { customerId: string; variantId: string; unitPrice: number; reason: string | null }): Promise<CustomerPrice> {
  return api('/api/pricing/customer-prices', { method: 'POST', body: JSON.stringify(body) })
}

export function removeCustomerPrice(customerId: string, variantId: string, reason?: string): Promise<boolean> {
  const params = new URLSearchParams({ customerId, variantId })
  if (reason) params.set('reason', reason)
  return api(`/api/pricing/customer-prices?${params}`, { method: 'DELETE' })
}

export function updateBasePrice(variantId: string, unitPrice: number, reason: string | null): Promise<Variant> {
  return api(`/api/pricing/base-price/${variantId}`, { method: 'PUT', body: JSON.stringify({ unitPrice, reason }) })
}

export function updatePurchasePrice(variantId: string, unitPrice: number, reason: string | null): Promise<Variant> {
  return api(`/api/pricing/purchase-price/${variantId}`, { method: 'PUT', body: JSON.stringify({ unitPrice, reason }) })
}

export function fetchPriceHistory(variantId?: string, customerId?: string): Promise<PriceHistory[]> {
  const params = new URLSearchParams()
  if (variantId) params.set('variantId', variantId)
  if (customerId) params.set('customerId', customerId)
  const query = params.toString() ? `?${params}` : ''
  return api(`/api/pricing/history${query}`)
}

export function fetchOfficialImages(signal?: AbortSignal): Promise<OfficialImage[]> {
  return api('/api/assets/product-images', { signal })
}

export function searchVariants(q = '', customerId?: string, signal?: AbortSignal): Promise<VariantPick[]> {
  const params = new URLSearchParams()
  if (q.trim()) params.set('q', q.trim())
  if (customerId) params.set('customerId', customerId)
  const query = params.toString() ? `?${params}` : ''
  return api(`/api/variants${query}`, { signal })
}
