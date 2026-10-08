export const incomingTreasuryCategories = ['sales', 'bank_deposit', 'other'] as const
export const outgoingTreasuryCategories = [
  'salaries',
  'rent',
  'office_expense',
  'office_purchases',
  'warehouse_expense',
  'car_expense',
  'withdrawal',
  'bank_deposit',
  'supplier_payment',
  'other',
] as const

export const allTreasuryCategories = [
  'sales',
  'bank_deposit',
  'other',
  'salaries',
  'rent',
  'office_expense',
  'office_purchases',
  'warehouse_expense',
  'car_expense',
  'withdrawal',
  'supplier_payment',
] as const

const incomingFilterCategories: readonly string[] = [...incomingTreasuryCategories, 'deposit']
const outgoingFilterCategories: readonly string[] = [...outgoingTreasuryCategories]

export function isIncomingTreasuryFilter(category: string | null | undefined): boolean {
  return !category || incomingFilterCategories.includes(category)
}

export function isOutgoingTreasuryFilter(category: string | null | undefined): boolean {
  return !category || outgoingFilterCategories.includes(category)
}

export function treasuryCategoryLabel(
  code: string | null | undefined,
  labels: {
    sales: string
    deposit: string
    salaries: string
    rent: string
    officeExpense: string
    officePurchases: string
    warehouseExpense: string
    carExpense: string
    withdrawal: string
    bankDeposit: string
    supplierPayment: string
    other: string
    noCategory: string
  },
): string {
  switch (code) {
    case 'sales': return labels.sales
    case 'deposit':
    case 'bank_deposit': return labels.bankDeposit
    case 'salaries': return labels.salaries
    case 'rent': return labels.rent
    case 'office_expense': return labels.officeExpense
    case 'office_purchases': return labels.officePurchases
    case 'warehouse_expense': return labels.warehouseExpense
    case 'car_expense': return labels.carExpense
    case 'withdrawal': return labels.withdrawal
    case 'supplier_payment': return labels.supplierPayment
    case 'other': return labels.other
    default: return labels.noCategory
  }
}
