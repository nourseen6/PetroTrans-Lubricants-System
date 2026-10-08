type IconName =
  | 'home'
  | 'customers'
  | 'sales'
  | 'products'
  | 'inventory'
  | 'purchasing'
  | 'reports'
  | 'assistant'
  | 'settings'
  | 'add'
  | 'edit'
  | 'view'
  | 'print'
  | 'payment'
  | 'search'
  | 'user'
  | 'more'
  | 'back'
  | 'forward'
  | 'warning'
  | 'check'

const paths: Record<IconName, string> = {
  home: 'M3 10.5 12 3l9 7.5V20a1 1 0 0 1-1 1h-5v-6H9v6H4a1 1 0 0 1-1-1v-9.5z',
  customers: 'M16 11a4 4 0 1 0-8 0 4 4 0 0 0 8 0zM4 20a8 8 0 0 1 16 0',
  sales: 'M7 7h10v2H7V7zm0 4h10v2H7v-2zm-2 8h14a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v12a1 1 0 0 0 1 1z',
  products: 'M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z',
  inventory: 'M3 7h18v4H3V7zm2 6h14v7H5v-7zm3-9h8v3H8V4z',
  purchasing: 'M6 6h15l-1.5 9h-12L6 6zm0 0L5 3H2m5 16a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3zm10 0a1.5 1.5 0 1 0 0-3 1.5 1.5 0 0 0 0 3z',
  reports: 'M4 19V5m0 14h16M8 17V10m4 7V7m4 10v-4',
  assistant: 'M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5L12 3zm7 11l.8 2.2L22 17l-2.2.8L19 20l-.8-2.2L16 17l2.2-.8L19 14zM5 14l.6 1.6L7 16.2l-1.4.6L5 18.4l-.6-1.6L3 16.2l1.4-.6L5 14z',
  settings: 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zM4.5 12l1.2-2.1.2-2.4 2.3-.8 1.5-1.9L12 3.5l2.3 1.3 1.5 1.9 2.3.8.2 2.4L19.5 12l-1.2 2.1-.2 2.4-2.3.8-1.5 1.9L12 20.5l-2.3-1.3-1.5-1.9-2.3-.8-.2-2.4L4.5 12z',
  add: 'M12 5v14M5 12h14',
  edit: 'M4 20h4l10-10-4-4L4 16v4zm11-13 4 4',
  view: 'M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12zm10 3a3 3 0 1 0 0-6 3 3 0 0 0 0 6z',
  print: 'M6 9V3h12v6M6 17H4a1 1 0 0 1-1-1v-5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v5a1 1 0 0 1-1 1h-2m-12 0h12v4H6v-4z',
  payment: 'M3 7a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7zm0 3h18M7 15h4',
  search: 'M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16zm10 2-4.3-4.3',
  user: 'M16 11a4 4 0 1 0-8 0 4 4 0 0 0 8 0zM4 20a8 8 0 0 1 16 0',
  more: 'M12 13a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm0-6a1 1 0 1 0 0-2 1 1 0 0 0 0 2zm0 12a1 1 0 1 0 0-2 1 1 0 0 0 0 2z',
  back: 'M15 18l-6-6 6-6',
  forward: 'M9 18l6-6-6-6',
  warning: 'M12 9v4m0 4h.01M10.3 4.3 2.8 17a2 2 0 0 0 1.7 3h15a2 2 0 0 0 1.7-3L13.7 4.3a2 2 0 0 0-3.4 0z',
  check: 'M20 6 9 17l-5-5',
}

export function Icon({ name, size = 18, className }: { name: IconName; size?: number; className?: string }) {
  return (
    <svg
      className={className ? `ui-icon ${className}` : 'ui-icon'}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d={paths[name]} />
    </svg>
  )
}
