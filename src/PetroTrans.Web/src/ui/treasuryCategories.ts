// "bank_deposit" (cash → bank) and "bank_withdrawal" (bank → cash) are saved as transfers,
// so the same amount shows up in the cash book and in the bank book at once.
export const transferCategories = ['bank_deposit', 'bank_withdrawal'] as const

export const incomingTreasuryCategories = ['sales', 'bank_withdrawal', 'other'] as const
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

export const bankIncomingCategories = ['sales', 'bank_deposit', 'other'] as const
export const bankOutgoingCategories = ['bank_withdrawal', 'supplier_payment', 'salaries', 'bank_fees', 'other'] as const

export const allTreasuryCategories = [
  'sales',
  'bank_deposit',
  'bank_withdrawal',
  'other',
  'salaries',
  'rent',
  'office_expense',
  'office_purchases',
  'warehouse_expense',
  'car_expense',
  'withdrawal',
  'supplier_payment',
  'bank_fees',
] as const

const incomingFilterCategories: readonly string[] = [...incomingTreasuryCategories, ...bankIncomingCategories, 'deposit']
const outgoingFilterCategories: readonly string[] = [...outgoingTreasuryCategories, ...bankOutgoingCategories]

export function isTransferCategory(category: string | null | undefined): boolean {
  return (transferCategories as readonly string[]).includes(category ?? '')
}

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
    bankWithdrawal: string
    bankFees: string
    supplierPayment: string
    other: string
    noCategory: string
  },
): string {
  switch (code) {
    case 'sales': return labels.sales
    case 'deposit':
    case 'bank_deposit': return labels.bankDeposit
    case 'bank_withdrawal': return labels.bankWithdrawal
    case 'bank_fees': return labels.bankFees
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
