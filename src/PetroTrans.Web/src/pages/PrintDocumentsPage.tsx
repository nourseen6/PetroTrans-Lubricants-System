import { useEffect, useState, type ReactNode } from 'react'
import { fetchCompany, fetchPayment, fetchStatement, fetchSupplierStatement, fetchTreasury, fetchBill, fetchReceipt, fetchReport, type CompanySettings, type PaymentDetail, type Statement, type SupplierStatement, type TreasuryBook, type Report } from '../api/operations'
import { displayKpiValue, displayReportCell, formatDate, formatMoney } from '../ui/format'
import { requestPdf, requestPrint } from '../ui/print'
import { treasuryCategoryLabel } from '../ui/treasuryCategories'

function PrintChrome({ title, children }: { title: string; children: ReactNode }) {
  return (
    <article className="print-sheet" dir="rtl">
      <div className="print-body">{children}</div>
      <p className="print-footer">بتروترانس — مستند للطباعة</p>
      <div className="print-actions">
        <button type="button" className="btn-secondary" onClick={() => window.history.back()}>رجوع</button>
        <button type="button" className="btn-secondary" onClick={() => requestPrint(title)}>طباعة</button>
        <button type="button" className="btn-secondary" onClick={() => requestPdf(`${title}.pdf`)}>PDF</button>
      </div>
    </article>
  )
}

export function PrintPaymentPage({ paymentId }: { paymentId: string }) {
  const [row, setRow] = useState<PaymentDetail | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchPayment(paymentId), fetchCompany()])
      .then(([payment, nextCompany]) => {
        setRow(payment)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [paymentId])
  if (error) return <p className="banner error">تعذر تحميل الإيصال.</p>
  if (!row || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title="إيصال تحصيل">
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>إيصال تحصيل</h2>
        </div>
      </header>
      <p>العميل: {row.customerName}</p>
      <p>التاريخ: {formatDate(row.paidOn)}</p>
      <p>المبلغ: {formatMoney(row.amount)}</p>
      <p>طريقة الدفع: {row.methodName ?? '—'}</p>
      <p>الفاتورة: {row.invoiceNumber ?? 'على الحساب'}</p>
      {row.allocations && row.allocations.length > 0 ? (
        <table className="data-table">
          <thead>
            <tr>
              <th>الفاتورة</th>
              <th>المبلغ</th>
            </tr>
          </thead>
          <tbody>
            {row.allocations.map((item) => (
              <tr key={item.invoiceId}>
                <td>{item.invoiceNumber ?? '—'}</td>
                <td>{formatMoney(item.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : null}
      {row.reference ? <p>المرجع: {row.reference}</p> : null}
      {row.notes ? <p>ملاحظات: {row.notes}</p> : null}
    </PrintChrome>
  )
}

export function PrintStatementPage({ customerId }: { customerId: string }) {
  const params = new URLSearchParams(window.location.search)
  const from = params.get('from') ?? undefined
  const to = params.get('to') ?? undefined
  const [statement, setStatement] = useState<Statement | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchStatement(customerId, from, to), fetchCompany()])
      .then(([next, nextCompany]) => {
        setStatement(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [customerId, from, to])
  if (error) return <p className="banner error">تعذر تحميل الكشف.</p>
  if (!statement || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title={`كشف حساب ${statement.customerName}`}>
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>كشف حساب</h2>
        </div>
      </header>
      <p>العميل: {statement.customerName} — {statement.customerCode}</p>
      {statement.phone ? <p>الهاتف: {statement.phone}</p> : null}
      <table className="data-table">
        <thead>
          <tr>
            <th>التاريخ</th>
            <th>المستند</th>
            <th>البيان</th>
            <th>مدين</th>
            <th>دائن</th>
            <th>الرصيد</th>
          </tr>
        </thead>
        <tbody>
          {statement.lines.map((line) => (
            <tr key={`${line.sourceDocumentId}-${line.occurredAt}`}>
              <td>{formatDate(line.occurredAt)}</td>
              <td>{line.document}</td>
              <td>{line.description}</td>
              <td>{formatMoney(line.debit)}</td>
              <td>{formatMoney(line.credit)}</td>
              <td>{formatMoney(line.runningBalance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>رصيد أول المدة: {formatMoney(statement.openingBalance)}</p>
      <p>إجمالي المدين: {formatMoney(statement.totalDebits)} — إجمالي الدائن: {formatMoney(statement.totalCredits)}</p>
      <p>رصيد آخر المدة: {formatMoney(statement.closingBalance)}</p>
    </PrintChrome>
  )
}

export function PrintReportPage() {
  const params = new URLSearchParams(window.location.search)
  const type = params.get('type') ?? 'sales'
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  const customerId = params.get('customerId') ?? ''
  const variantId = params.get('variantId') ?? ''
  const [report, setReport] = useState<Report | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    const filters: Record<string, string> = {}
    if (from) filters.from = from
    if (to) filters.to = to
    if (customerId) filters.customerId = customerId
    if (variantId) filters.variantId = variantId
    void Promise.all([fetchReport(type, filters), fetchCompany()])
      .then(([next, nextCompany]) => {
        setReport(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [type, from, to, customerId, variantId])
  if (error) return <p className="banner error">تعذر تحميل التقرير.</p>
  if (!report || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title={report.title}>
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>{report.title}</h2>
        </div>
      </header>
      {report.note ? <p>{report.note}</p> : null}
      {report.kpis && report.kpis.length > 0 ? (
        <div className="print-kpis">
          {report.kpis.map((kpi) => (
            <div key={kpi.label} className="print-kpi">
              <span>{kpi.label}</span>
              <strong>{displayKpiValue(kpi.label, kpi.value)}</strong>
              {kpi.hint ? <small>{kpi.hint}</small> : null}
            </div>
          ))}
        </div>
      ) : null}
      <table className="data-table">
        <thead>
          <tr>{report.columns.map((col) => <th key={col}>{col}</th>)}</tr>
        </thead>
        <tbody>
          {report.rows.map((row, index) => {
            const isGroup = Boolean(row[0]) && row.slice(1).every((cell) => !cell)
            if (isGroup) {
              return (
                <tr key={index} className="category-row">
                  <td colSpan={row.length}>{row[0]}</td>
                </tr>
              )
            }
            return (
              <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{displayReportCell(report.columns[cellIndex] ?? '', cell)}</td>)}</tr>
            )
          })}
        </tbody>
      </table>
      {report.sections?.map((section) => (
        <section key={section.title} className="print-block">
          <h2>{section.title}</h2>
          <table className="data-table">
            <thead>
              <tr>{section.columns.map((col) => <th key={col}>{col}</th>)}</tr>
            </thead>
            <tbody>
              {section.rows.map((row, index) => (
                <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{displayReportCell(section.columns[cellIndex] ?? '', cell)}</td>)}</tr>
              ))}
            </tbody>
          </table>
        </section>
      ))}
    </PrintChrome>
  )
}

export function PrintReceiptPage({ receiptId }: { receiptId: string }) {
  const [doc, setDoc] = useState<Awaited<ReturnType<typeof fetchReceipt>> | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchReceipt(receiptId), fetchCompany()])
      .then(([next, nextCompany]) => {
        setDoc(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [receiptId])
  if (error) return <p className="banner error">تعذر تحميل إذن الاستلام.</p>
  if (!doc || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title="إذن استلام">
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>إذن استلام {doc.number ?? ''}</h2>
        </div>
      </header>
      <p>المورد: {doc.supplierName}</p>
      <p>التاريخ: {formatDate(doc.documentDate)} — الحالة: {doc.status}</p>
      <table className="data-table">
        <thead>
          <tr>
            <th>الصنف</th>
            <th>العبوة</th>
            <th>الكمية</th>
          </tr>
        </thead>
        <tbody>
          {doc.lines.map((line, index) => (
            <tr key={`${line.variantId}-${index}`}>
              <td>{line.productName}</td>
              <td>{line.packagingType} {line.packagingSize}</td>
              <td>{line.quantity}</td>
            </tr>
          ))}
        </tbody>
      </table>
      {doc.notes ? <p>ملاحظات: {doc.notes}</p> : null}
    </PrintChrome>
  )
}

export function PrintBillPage({ billId }: { billId: string }) {
  const [doc, setDoc] = useState<Awaited<ReturnType<typeof fetchBill>> | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchBill(billId), fetchCompany()])
      .then(([next, nextCompany]) => {
        setDoc(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [billId])
  if (error) return <p className="banner error">تعذر تحميل فاتورة المشتريات.</p>
  if (!doc || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title="فاتورة مشتريات">
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>فاتورة مشتريات {doc.number ?? ''}</h2>
        </div>
      </header>
      <div className="print-meta">
        <p>المورد: {doc.supplierName}</p>
        <p>التاريخ: {formatDate(doc.invoiceDate)}</p>
        <p>رقم الفاتورة: {doc.number ?? '—'}</p>
        <p>الحالة: {doc.status === 'posted' ? 'مرحّلة' : doc.status}</p>
      </div>
      <table className="data-table">
        <thead>
          <tr>
            <th>الصنف</th>
            <th>العبوة</th>
            <th>الكمية</th>
            <th>سعر الشركة</th>
            <th>الإجمالي</th>
          </tr>
        </thead>
        <tbody>
          {doc.lines.map((line, index) => (
            <tr key={`${line.variantId}-${index}`}>
              <td>{line.productName}</td>
              <td>{line.packagingType} {line.packagingSize}</td>
              <td>{line.quantity}</td>
              <td>{formatMoney(line.unitPrice)}</td>
              <td>{formatMoney(line.lineTotal)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <div className="print-totals">
        <p>إجمالي البنود: {formatMoney(doc.linesSubtotal ?? doc.lines.reduce((sum, line) => sum + line.lineTotal, 0))}</p>
        {(doc.linesSubtotal ?? doc.lines.reduce((sum, line) => sum + line.lineTotal, 0)) !== doc.goodsTotal ? (
          <p>تسوية الإجمالي: {formatMoney(doc.goodsTotal - (doc.linesSubtotal ?? doc.lines.reduce((sum, line) => sum + line.lineTotal, 0)))}</p>
        ) : null}
        <p>الإجمالي النهائي: {formatMoney(doc.goodsTotal)}</p>
        <p>المدفوع: {formatMoney(doc.paidTotal ?? 0)} — المتبقي: {formatMoney(doc.remainingTotal ?? doc.goodsTotal)}</p>
      </div>
      {doc.notes ? <p>ملاحظات: {doc.notes}</p> : null}
    </PrintChrome>
  )
}

const treasuryPrintLabels = {
  sales: 'المبيعات',
  deposit: 'إيداع بنك',
  salaries: 'رواتب',
  rent: 'إيجار',
  officeExpense: 'مصروف مكتب',
  officePurchases: 'مشتريات مكتب',
  warehouseExpense: 'مصروفات المخزن',
  carExpense: 'مصاريف سيارة',
  withdrawal: 'مسحوبات',
  bankDeposit: 'إيداع بنك',
  supplierPayment: 'سداد مورد',
  other: 'غير ذلك',
  noCategory: 'بدون تصنيف',
}

function printTreasuryCategoryLabel(code: string | null) {
  return treasuryCategoryLabel(code, treasuryPrintLabels)
}

export function PrintSupplierStatementPage({ supplierId }: { supplierId: string }) {
  const params = new URLSearchParams(window.location.search)
  const from = params.get('from') ?? undefined
  const to = params.get('to') ?? undefined
  const [statement, setStatement] = useState<SupplierStatement | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchSupplierStatement(supplierId, from, to), fetchCompany()])
      .then(([next, nextCompany]) => {
        setStatement(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [supplierId, from, to])
  if (error) return <p className="banner error">تعذر تحميل كشف المورد.</p>
  if (!statement || !company) return <p className="muted">جاري التحميل…</p>
  return (
    <PrintChrome title={`كشف حساب ${statement.supplierName}`}>
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>كشف حساب مورد</h2>
        </div>
      </header>
      <p>المورد: {statement.supplierName} — {statement.supplierCode}</p>
      {statement.phone ? <p>الهاتف: {statement.phone}</p> : null}
      <table className="data-table">
        <thead>
          <tr>
            <th>التاريخ</th>
            <th>المستند</th>
            <th>البيان</th>
            <th>فاتورة مشتريات</th>
            <th>دفعة</th>
            <th>المستحق</th>
          </tr>
        </thead>
        <tbody>
          {statement.lines.map((line) => (
            <tr key={`${line.sourceDocumentId}-${line.occurredAt}`}>
              <td>{formatDate(line.occurredAt)}</td>
              <td>{line.document}</td>
              <td>{line.description}</td>
              <td>{formatMoney(line.debit)}</td>
              <td>{formatMoney(line.credit)}</td>
              <td>{formatMoney(line.runningBalance)}</td>
            </tr>
          ))}
        </tbody>
      </table>
      <p>رصيد أول المدة: {formatMoney(statement.openingBalance)}</p>
      <p>إجمالي الفواتير: {formatMoney(statement.totalDebits)} — إجمالي الدفعات: {formatMoney(statement.totalCredits)}</p>
      <p>المستحق: {formatMoney(statement.outstanding)}</p>
    </PrintChrome>
  )
}

export function PrintTreasuryPage() {
  const params = new URLSearchParams(window.location.search)
  const from = params.get('from') ?? undefined
  const to = params.get('to') ?? undefined
  const category = params.get('category') ?? undefined
  const [book, setBook] = useState<TreasuryBook | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    void Promise.all([fetchTreasury(from, to, category), fetchCompany()])
      .then(([next, nextCompany]) => {
        setBook(next)
        setCompany(nextCompany)
      })
      .catch((err: unknown) => setError(err))
  }, [from, to, category])
  if (error) return <p className="banner error">تعذر تحميل الخزينة.</p>
  if (!book || !company) return <p className="muted">جاري التحميل…</p>
  const incoming = book.entries.filter((row) => row.direction === 'in')
  const outgoing = book.entries.filter((row) => row.direction === 'out')
  const filtered = Boolean(book.filterCategory)
  const categoryName = printTreasuryCategoryLabel(book.filterCategory ?? null)
  return (
    <PrintChrome title={filtered ? `تقرير ${categoryName}` : 'كشف الخزينة'}>
      <header className="print-header">
        {company.logoUrl ? <img src={company.logoUrl} alt="" className="print-logo" /> : null}
        <div>
          <h1>{company.companyName}</h1>
          <h2>{filtered ? `تقرير البند — ${categoryName}` : 'كشف الخزينة — الوارد والمنصرف'}</h2>
        </div>
      </header>
      <div className="print-meta">
        {filtered ? (
          <p>إجمالي البند: {formatMoney(book.categoryTotal ?? 0)}</p>
        ) : (
          <>
            <p>رصيد سابق: {formatMoney(book.openingBalance)}</p>
            <p>إجمالي الوارد: {formatMoney(book.totalIn)}</p>
            <p>إجمالي المنصرف: {formatMoney(book.totalOut)}</p>
            <p>الرصيد: {formatMoney(book.balance)}</p>
          </>
        )}
      </div>
      {filtered && incoming.length === 0 ? null : (
      <section className="print-block">
        <h2>الوارد</h2>
        {incoming.length === 0 ? (
          <p>لا يوجد وارد في الفترة.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>البيان</th>
                <th>التصنيف</th>
                <th>المبلغ</th>
              </tr>
            </thead>
            <tbody>
              {incoming.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.occurredOn)}</td>
                  <td>{row.description}</td>
                  <td>{printTreasuryCategoryLabel(row.category)}</td>
                  <td>{formatMoney(row.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
      )}
      {filtered && outgoing.length === 0 ? null : (
      <section className="print-block">
        <h2>المنصرف</h2>
        {outgoing.length === 0 ? (
          <p>لا يوجد منصرف في الفترة.</p>
        ) : (
          <table className="data-table">
            <thead>
              <tr>
                <th>التاريخ</th>
                <th>البيان</th>
                <th>التصنيف</th>
                <th>المبلغ</th>
              </tr>
            </thead>
            <tbody>
              {outgoing.map((row) => (
                <tr key={row.id}>
                  <td>{formatDate(row.occurredOn)}</td>
                  <td>{row.description}</td>
                  <td>{printTreasuryCategoryLabel(row.category)}</td>
                  <td>{formatMoney(row.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
      )}
    </PrintChrome>
  )
}
