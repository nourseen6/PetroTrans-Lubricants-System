import { useEffect, useMemo, useState } from 'react'
import { fetchAuthStatus, logout, type AuthUser } from './api/auth'
import { LocaleContext } from './i18n/LocaleContext'
import { strings, type Locale } from './i18n/strings'
import { TopBar } from './layout/TopBar'
import { pathToRoute, printInvoiceIdFromPath, printPaymentIdFromPath, printStatementIdFromPath, printSupplierStatementIdFromPath, printReceiptIdFromPath, printBillIdFromPath, customerIdFromPath } from './navigation/routes'
import { HomePage } from './pages/HomePage'
import { InventoryPage } from './pages/InventoryPage'
import { LoginPage } from './pages/LoginPage'
import { NotFoundPage } from './pages/NotFoundPage'
import { CustomersPage } from './pages/CustomersPage'
import { CustomerFilePage } from './pages/CustomerFilePage'
import { PrintInvoicePage } from './pages/PrintInvoicePage'
import { PrintPaymentPage, PrintStatementPage, PrintSupplierStatementPage, PrintTreasuryPage, PrintReportPage, PrintReceiptPage, PrintBillPage } from './pages/PrintDocumentsPage'
import { ProductsPage } from './pages/ProductsPage'
import { PricingPage } from './pages/PricingPage'
import { PurchasingPage } from './pages/PurchasingPage'
import { ReportsPage } from './pages/ReportsPage'
import { SalesPage } from './pages/SalesPage'
import { SettingsPage } from './pages/SettingsPage'
import { SetupPage } from './pages/SetupPage'
import { AssistantPage } from './pages/AssistantPage'
import { TreasuryPage } from './pages/TreasuryPage'
import { APP_VERSION } from './version'

function detectLogoSrc(): string {
  return '/branding/logo-petrotrans.png'
}

function isAbortError(err: unknown): boolean {
  return err instanceof DOMException
    ? err.name === 'AbortError'
    : err instanceof Error && err.name === 'AbortError'
}

export default function App() {
  const [locale, setLocale] = useState<Locale>('ar')
  const [theme, setTheme] = useState<'light' | 'dark'>('light')
  const [path, setPath] = useState(() => `${window.location.pathname}${window.location.search}` || '/')
  const [logoSrc, setLogoSrc] = useState<string | null>(null)
  const [boot, setBoot] = useState<'loading' | 'error' | 'ready'>('loading')
  const [needsSetup, setNeedsSetup] = useState(false)
  const [user, setUser] = useState<AuthUser | null>(null)

  const t = strings[locale]
  const dir = locale === 'ar' ? 'rtl' : 'ltr'
  const route = pathToRoute(path)

  useEffect(() => {
    document.documentElement.lang = locale
    document.documentElement.dir = dir
    document.documentElement.dataset.theme = theme
    document.title = `${t.appName} ${APP_VERSION}`
  }, [dir, locale, t.appName, theme, APP_VERSION])

  useEffect(() => {
    const src = detectLogoSrc()
    const image = new Image()
    image.onload = () => setLogoSrc(src)
    image.onerror = () => setLogoSrc(null)
    image.src = src
  }, [])

  useEffect(() => {
    const onPop = () => setPath(`${window.location.pathname}${window.location.search}` || '/')
    window.addEventListener('popstate', onPop)
    return () => window.removeEventListener('popstate', onPop)
  }, [])

  useEffect(() => {
    const controller = new AbortController()
    let cancelled = false
    fetchAuthStatus(controller.signal)
      .then((status) => {
        if (cancelled) {
          return
        }
        setNeedsSetup(status.needsSetup)
        setUser(status.user)
        if (status.user?.locale === 'en' || status.user?.locale === 'ar') {
          setLocale(status.user.locale)
        }
        if (status.user?.theme === 'dark' || status.user?.theme === 'light') {
          setTheme(status.user.theme)
        }
        setBoot('ready')
      })
      .catch((err: unknown) => {
        if (!cancelled && !isAbortError(err)) {
          setBoot('error')
        }
      })
    return () => {
      cancelled = true
      controller.abort()
    }
  }, [])

  const localeValue = useMemo(
    () => ({
      locale,
      t,
      toggleLocale: () => setLocale((current) => (current === 'ar' ? 'en' : 'ar')),
    }),
    [locale, t],
  )

  function navigate(next: string) {
    window.history.pushState({}, '', next)
    setPath(next)
  }

  function onLoggedIn(next: AuthUser) {
    setUser(next)
    setNeedsSetup(false)
    if (next.locale === 'en' || next.locale === 'ar') {
      setLocale(next.locale)
    }
    if (next.theme === 'dark' || next.theme === 'light') {
      setTheme(next.theme)
    }
  }

  async function onLogout() {
    try {
      await logout()
    } catch {
      // Still return to the login screen.
    }
    setUser(null)
  }

  const page =
    route === 'home' && user ? (
      <HomePage user={user} onNavigate={navigate} />
    ) : route === 'sales' && user ? (
      <SalesPage user={user} onNavigate={navigate} />
    ) : route === 'customers' ? (
      <CustomersPage onNavigate={navigate} />
    ) : route === 'customer-file' && user ? (
      <CustomerFilePage customerId={customerIdFromPath(path)} user={user} onNavigate={navigate} />
    ) : route === 'products' ? (
      <ProductsPage />
    ) : route === 'pricing' ? (
      <PricingPage />
    ) : route === 'inventory' ? (
      <InventoryPage />
    ) : route === 'purchasing' ? (
      <PurchasingPage onNavigate={navigate} />
    ) : route === 'treasury' ? (
      <TreasuryPage onNavigate={navigate} />
    ) : route === 'reports' ? (
      <ReportsPage onNavigate={navigate} />
    ) : route === 'assistant' && user ? (
      <AssistantPage user={user} onNavigate={navigate} />
    ) : route === 'settings' ? (
      <SettingsPage />
    ) : route === 'not-found' ? (
      <NotFoundPage />
    ) : null

  return (
    <LocaleContext.Provider value={localeValue}>
      {boot === 'loading' ? (
        <div className="auth-screen">
          <p className="muted">{t.loading}</p>
        </div>
      ) : boot === 'error' ? (
        <div className="auth-screen">
          <div className="auth-card">
            <p className="banner error">{t.apiFail}</p>
            <button className="primary-btn" type="button" onClick={() => window.location.reload()}>
              {t.retry}
            </button>
          </div>
        </div>
      ) : needsSetup ? (
        <SetupPage logoSrc={logoSrc} onReady={onLoggedIn} />
      ) : user === null ? (
        <LoginPage logoSrc={logoSrc} onLoggedIn={onLoggedIn} />
      ) : route === 'print-invoice' ? (
        <PrintInvoicePage invoiceId={printInvoiceIdFromPath(path)} />
      ) : route === 'print-payment' ? (
        <PrintPaymentPage paymentId={printPaymentIdFromPath(path)} />
      ) : route === 'print-statement' ? (
        <PrintStatementPage customerId={printStatementIdFromPath(path)} />
      ) : route === 'print-supplier-statement' ? (
        <PrintSupplierStatementPage supplierId={printSupplierStatementIdFromPath(path)} />
      ) : route === 'print-treasury' ? (
        <PrintTreasuryPage />
      ) : route === 'print-report' ? (
        <PrintReportPage />
      ) : route === 'print-receipt' ? (
        <PrintReceiptPage receiptId={printReceiptIdFromPath(path)} />
      ) : route === 'print-bill' ? (
        <PrintBillPage billId={printBillIdFromPath(path)} />
      ) : (
        <div className="app-shell" dir={dir}>
          <TopBar
            theme={theme}
            logoSrc={logoSrc}
            userName={user.displayName}
            current={route === 'customer-file' ? 'customers' : route === 'pricing' ? 'pricing' : route}
            onNavigate={navigate}
            onToggleTheme={() => setTheme((current) => (current === 'light' ? 'dark' : 'light'))}
            onLogout={() => {
              void onLogout()
            }}
          />
          <main className="main">{page}</main>
        </div>
      )}
    </LocaleContext.Provider>
  )
}
