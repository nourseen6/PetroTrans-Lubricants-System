export type NavId =
  | 'home'
  | 'sales'
  | 'customers'
  | 'products'
  | 'pricing'
  | 'inventory'
  | 'purchasing'
  | 'treasury'
  | 'reports'
  | 'assistant'
  | 'settings'

export type RouteId =
  | NavId
  | 'print-invoice'
  | 'print-payment'
  | 'print-statement'
  | 'print-report'
  | 'print-receipt'
  | 'print-bill'
  | 'print-supplier-statement'
  | 'print-treasury'
  | 'customer-file'
  | 'not-found'

export const routes: { id: NavId; path: string }[] = [
  { id: 'home', path: '/' },
  { id: 'sales', path: '/sales' },
  { id: 'customers', path: '/customers' },
  { id: 'products', path: '/products' },
  { id: 'pricing', path: '/pricing' },
  { id: 'inventory', path: '/inventory' },
  { id: 'purchasing', path: '/purchasing' },
  { id: 'treasury', path: '/treasury' },
  { id: 'reports', path: '/reports' },
  { id: 'assistant', path: '/assistant' },
  { id: 'settings', path: '/settings' },
]

export function pathToRoute(pathname: string): RouteId {
  const normalized = (pathname.split('?')[0] ?? '/').replace(/\/+$/, '') || '/'
  if (normalized.startsWith('/print/invoice/')) return 'print-invoice'
  if (normalized.startsWith('/print/payment/')) return 'print-payment'
  if (normalized.startsWith('/print/supplier-statement/')) return 'print-supplier-statement'
  if (normalized.startsWith('/print/statement/')) return 'print-statement'
  if (normalized.startsWith('/print/treasury')) return 'print-treasury'
  if (normalized.startsWith('/print/report')) return 'print-report'
  if (normalized.startsWith('/print/receipt/')) return 'print-receipt'
  if (normalized.startsWith('/print/bill/')) return 'print-bill'
  if (normalized.startsWith('/customers/') && normalized !== '/customers') return 'customer-file'
  const match = routes.find((item) => item.path === normalized)
  return match?.id ?? 'not-found'
}

export function printInvoiceIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function printPaymentIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function printStatementIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function printReceiptIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function printBillIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function printSupplierStatementIdFromPath(pathname: string): string {
  return pathId(pathname, 2)
}

export function customerIdFromPath(pathname: string): string {
  return pathId(pathname, 1)
}

function pathId(pathname: string, index: number): string {
  const clean = pathname.split('?')[0] ?? ''
  const parts = clean.split('/').filter(Boolean)
  return parts[index] ?? ''
}
