import { useEffect, useState } from 'react'
import { fetchCompany, type CompanySettings } from '../api/operations'
import { fetchInvoice, type Invoice } from '../api/sales'
import { formatDate, formatMoney } from '../ui/format'
import { requestPdf, requestPrint } from '../ui/print'

export function PrintInvoicePage({ invoiceId }: { invoiceId: string }) {
  const [invoice, setInvoice] = useState<Invoice | null>(null)
  const [company, setCompany] = useState<CompanySettings | null>(null)
  const [error, setError] = useState<unknown>(null)

  useEffect(() => {
    void Promise.all([fetchInvoice(invoiceId), fetchCompany()])
      .then(([nextInvoice, nextCompany]) => {
        setInvoice(nextInvoice)
        setCompany(nextCompany)
        document.title = `فاتورة ${nextInvoice.number ?? ''}`
      })
      .catch((err: unknown) => setError(err))
  }, [invoiceId])

  if (error) return <p className="banner error">تعذر تحميل الفاتورة.</p>
  if (!invoice || !company) return <p className="muted">جاري التحميل…</p>

  return (
    <article className="print-sheet" dir="rtl">
      <header className="print-header">
        <img src={company.logoUrl ?? '/branding/logo-petrotrans.png'} alt="" className="print-logo" />
        <div>
          <h1>{company.companyName}</h1>
          <h2>فاتورة مبيعات</h2>
        </div>
      </header>
      <div className="print-body">
        <div className="print-meta">
          <p><span>العميل</span> {invoice.customerName}</p>
          <p><span>التاريخ</span> {formatDate(invoice.invoiceDate)}</p>
          <p><span>رقم الفاتورة</span> {invoice.number ?? '—'}</p>
        </div>
        <table className="data-table">
          <thead>
            <tr>
              <th>الصنف</th>
              <th>العبوة</th>
              <th>الكمية</th>
              <th>السعر</th>
              <th>الإجمالي</th>
            </tr>
          </thead>
          <tbody>
            {invoice.lines.map((line) => (
              <tr key={line.id}>
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
          <p>إجمالي البنود: {formatMoney(invoice.linesSubtotal)}</p>
          <p>الخصم: {formatMoney(invoice.discountAmount)}</p>
          <p>الإجمالي النهائي: {formatMoney(invoice.goodsTotal)}</p>
          <p>المدفوع: {formatMoney(invoice.paidTotal)} — المتبقي: {formatMoney(invoice.remainingTotal)}</p>
        </div>
        {invoice.notes ? <p>ملاحظات: {invoice.notes}</p> : null}
      </div>
      <p className="print-footer">بتروترانس — فاتورة مبيعات</p>
      <div className="print-actions">
        <button type="button" className="btn-secondary" onClick={() => window.history.back()}>رجوع</button>
        <button type="button" className="btn-secondary" onClick={() => requestPrint(`فاتورة ${invoice.number ?? ''}`)}>طباعة</button>
        <button type="button" className="btn-secondary" onClick={() => requestPdf(`invoice-${invoice.number ?? invoice.id}.pdf`)}>PDF</button>
      </div>
    </article>
  )
}
