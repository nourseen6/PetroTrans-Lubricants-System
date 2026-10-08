export function formatMoney(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '—'
  }
  const formatted = new Intl.NumberFormat('ar-EG', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 2,
  }).format(value)
  return `${formatted} ج.م`
}

export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value.slice(0, 10)
  }
  return new Intl.DateTimeFormat('ar-EG', { day: 'numeric', month: 'numeric', year: 'numeric' }).format(date)
}

export function todayIso(): string {
  const date = new Date()
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`
}

export function toDateInput(value: string | null | undefined): string {
  return value ? value.slice(0, 10) : ''
}

export function parseAmount(value: string): number | null {
  const trimmed = value.trim().replace(/,/g, '')
  if (!trimmed) {
    return null
  }
  const parsed = Number(trimmed)
  return Number.isFinite(parsed) ? parsed : null
}

export function friendlyError(err: unknown, fallback = 'حدث خطأ أثناء تحميل البيانات. حاول مرة أخرى.'): string {
  const raw = err instanceof Error ? err.message : String(err ?? '')
  const text = raw.trim()
  if (!text) {
    return fallback
  }

  const lower = text.toLowerCase()
  if (
    lower.includes('unexpected end of json') ||
    lower.includes('failed to execute') ||
    lower.includes('json') && lower.includes('response') ||
    lower.includes('failed to fetch') ||
    lower.includes('networkerror') ||
    /^request \d+$/.test(lower)
  ) {
    if (lower.includes('401') || text.includes('يجب تسجيل الدخول')) {
      return 'يجب تسجيل الدخول.'
    }
    if (lower.includes('403') || text.includes('غير مسموح')) {
      return 'غير مسموح بهذه العملية.'
    }
    if (lower.includes('400')) {
      return 'من فضلك أدخل البيانات المطلوبة.'
    }
    return fallback
  }

  if (text.includes('أضف مخزناً')) {
    return 'يجب إضافة مخزن أولاً من الإعدادات.'
  }
  if (text.includes('ترقيم الفواتير')) {
    return 'يجب إعداد ترقيم الفواتير أولاً من الإعدادات.'
  }
  if (text.includes('الكمية المتاحة غير كافية')) {
    return 'الكمية المطلوبة أكبر من الكمية المتاحة في المخزن.'
  }
  if (text.includes('لا يمكن أن يتجاوز المتبقي')) {
    return 'المبلغ المدفوع لا يمكن أن يكون أكبر من المتبقي.'
  }
  if (text.includes('أضف طريقة دفع')) {
    return 'يجب إضافة طريقة دفع أولاً من الإعدادات.'
  }
  if (text.startsWith('request ') || text.startsWith('HTTP') || text.includes('Exception') || text.includes('stack')) {
    return fallback
  }

  return text
}

export function movementLabel(type: string, direction: string): string {
  const labels: Record<string, string> = {
    sale: 'بيع',
    purchase_receipt: 'استلام مشتريات',
    sales_return: 'مرتجع بيع',
    adjustment_in: 'تسوية زيادة',
    adjustment_out: 'تسوية نقص',
    transfer_in: 'تحويل وارد',
    transfer_out: 'تحويل صادر',
    opening_stock: 'رصيد أول المدة',
  }
  return labels[type] ?? (direction === 'in' ? 'زيادة' : 'نقص')
}

export function statusLabel(value: string | null | undefined): string {
  if (!value) {
    return '—'
  }
  const labels: Record<string, string> = {
    posted: 'مرحّلة',
    draft: 'مسودة',
    paid: 'مدفوعة',
    partial: 'جزئية',
    unpaid: 'غير مدفوعة',
    in: 'وارد',
    out: 'صادر',
    invoice: 'فاتورة',
    payment: 'دفعة',
    supplier_payment: 'دفعة مورد',
    purchase_invoice: 'فاتورة مشتريات',
    sale: 'بيع',
    purchase_receipt: 'استلام مشتريات',
    sales_return: 'مرتجع بيع',
    adjustment_in: 'تسوية زيادة',
    adjustment_out: 'تسوية نقص',
    transfer_in: 'تحويل وارد',
    transfer_out: 'تحويل صادر',
    opening_stock: 'رصيد أول المدة',
  }
  return labels[value] ?? value
}

const moneyColumns = new Set([
  'الإجمالي', 'المدفوع', 'المتبقي', 'المبلغ', 'المستحق',
  'عليه', 'له', 'الصافي', 'المبيعات', 'التكلفة', 'المكسب', 'القيمة',
])
const statusColumns = new Set(['حالة الدفع', 'حالة المستند', 'الحالة', 'النوع', 'الاتجاه'])
const dateColumns = new Set(['التاريخ', 'الاستحقاق'])

export function displayKpiValue(label: string, value: string): string {
  if (/عبوات|عدد|كمية/.test(label)) {
    return value
  }
  return displayReportCell('المبلغ', value)
}

export function displayReportCell(column: string, value: string | null | undefined): string {
  if (value == null || value === '') {
    return '—'
  }
  if (moneyColumns.has(column)) {
    const amount = Number(value)
    return Number.isFinite(amount) ? formatMoney(amount) : value
  }
  if (statusColumns.has(column)) {
    return statusLabel(value)
  }
  if (dateColumns.has(column)) {
    return formatDate(value)
  }
  return value
}

export function auditLabel(action: string): string {
  const labels: Record<string, string> = {
    'sales.create': 'إنشاء فاتورة',
    'sales.update': 'تعديل فاتورة',
    'sales.post': 'ترحيل فاتورة',
    'sales.unpost': 'إرجاع فاتورة لمسودة',
    'payments.create': 'تسجيل دفعة',
    'payments.void': 'حذف دفعة',
    'payments.supplier': 'تسجيل دفعة مورد',
    'pricing.override': 'تعديل سعر',
    'inventory.adjust': 'تسوية مخزون',
    'inventory.adjust_update': 'تعديل تسوية مخزون',
    'inventory.adjust_void': 'حذف تسوية مخزون',
    'inventory.receive': 'استلام بضاعة',
    'inventory.transfer': 'تحويل مخزون',
    'purchasing.post': 'ترحيل فاتورة مشتريات',
    'purchasing.supplier': 'حفظ مورد',
    'returns.create': 'مرتجع بيع',
    'settings.warehouse': 'مخزن',
    'settings.payment_method': 'طريقة دفع',
    'settings.number_series': 'ترقيم الفواتير',
    'backup.create': 'نسخة احتياطية',
    'backup.restore': 'استعادة نسخة',
    'customers.create': 'إضافة عميل',
    'customers.update': 'تعديل عميل',
  }
  return labels[action] ?? action
}
