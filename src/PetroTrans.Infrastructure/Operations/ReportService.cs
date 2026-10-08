using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Purchasing;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class ReportService : IReportService
{
    private readonly AppDbContext _db;
    private readonly IPartyLedgerService _ledger;

    public ReportService(AppDbContext db, IPartyLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    public async Task<ReportDto> SalesAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var items = await PostedInvoices(filters).OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);
        return new ReportDto(
            "المبيعات",
            ["الرقم", "التاريخ", "العميل", "الإجمالي", "المدفوع", "المتبقي", "حالة الدفع"],
            items.Select(x => (IReadOnlyList<string?>)[x.Number, x.InvoiceDate.ToString("yyyy-MM-dd"), x.Customer.Name, x.GoodsTotal.ToString(), x.PaidTotal.ToString(), x.RemainingTotal.ToString(), x.PaymentStatus]).ToList());
    }

    public async Task<ReportDto> InvoiceStatusAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.SalesInvoices.AsNoTracking().Include(x => x.Customer).AsQueryable();
        if (filters.From is { } from) query = query.Where(x => x.InvoiceDate >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.InvoiceDate <= to.Date);
        if (filters.CustomerId is { } customerId) query = query.Where(x => x.CustomerId == customerId);
        if (filters.VariantId is { } variantId) query = query.Where(x => x.Lines.Any(line => line.VariantId == variantId));
        var items = await query.OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);
        return new ReportDto(
            "حالة الفواتير",
            ["الرقم", "التاريخ", "العميل", "حالة المستند", "حالة الدفع", "المتبقي"],
            items.Select(x => (IReadOnlyList<string?>)[x.Number, x.InvoiceDate.ToString("yyyy-MM-dd"), x.Customer.Name, x.Status, x.PaymentStatus, x.RemainingTotal.ToString()]).ToList());
    }

    public async Task<ReportDto> UnpaidInvoicesAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var items = await PostedInvoices(filters).Where(x => x.RemainingTotal > 0).OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);
        return new ReportDto(
            "فواتير غير مسددة",
            ["الرقم", "التاريخ", "العميل", "المتبقي", "الاستحقاق"],
            items.Select(x => (IReadOnlyList<string?>)[x.Number, x.InvoiceDate.ToString("yyyy-MM-dd"), x.Customer.Name, x.RemainingTotal.ToString(), x.DueDate?.ToString("yyyy-MM-dd")]).ToList());
    }

    public async Task<ReportDto> PaymentsAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.Payments.AsNoTracking().Include(x => x.Invoice).Include(x => x.PaymentMethod).AsQueryable();
        if (filters.From is { } from) query = query.Where(x => x.PaidOn >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.PaidOn <= to.Date);
        if (filters.CustomerId is { } customerId) query = query.Where(x => x.PartyId == customerId);
        var items = await query.OrderBy(x => x.PaidOn).ToListAsync(cancellationToken);
        var customerIds = items.Select(x => x.PartyId).Distinct().ToList();
        var names = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return new ReportDto(
            "الدفعات",
            ["التاريخ", "العميل", "الفاتورة", "المبلغ", "الطريقة"],
            items.Select(x => (IReadOnlyList<string?>)[
                x.PaidOn.ToString("yyyy-MM-dd"),
                names.GetValueOrDefault(x.PartyId, "—"),
                x.Invoice?.Number ?? "على الحساب",
                x.Amount.ToString(),
                x.PaymentMethod.Name]).ToList());
    }

    public async Task<ReportDto> InventoryOnHandAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryBalances.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Variant).ThenInclude(x => x.Product).AsQueryable();
        if (filters.WarehouseId is { } warehouseId) query = query.Where(x => x.WarehouseId == warehouseId);
        if (filters.VariantId is { } variantId) query = query.Where(x => x.VariantId == variantId);
        var items = await query.ToListAsync(cancellationToken);
        var ordered = items
            .OrderBy(x => x.Warehouse.Name, StringComparer.Ordinal)
            .ThenBy(x => CatalogDisplayOrder.CategoryRank(x.Variant.Product.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.Variant.Product.Name, x.Variant.Product.Brand, x.Variant.Product.Specification))
            .ThenBy(x => CatalogDisplayOrder.PackagingRank(x.Variant.PackagingSize))
            .ThenBy(x => x.Variant.PackagingSize, StringComparer.Ordinal)
            .ToList();
        var rows = new List<IReadOnlyList<string?>>();
        string? lastCategory = null;
        foreach (var x in ordered)
        {
            var title = CatalogDisplayOrder.CategoryTitle(x.Variant.Product.Category);
            if (title != lastCategory)
            {
                rows.Add([title, "", "", "", ""]);
                lastCategory = title;
            }

            var value = x.Variant.StandardPurchasePrice is { } price ? price * x.OnHand : (decimal?)null;
            rows.Add([
                x.Warehouse.Name,
                x.Variant.Product.Name,
                PackLabel(x.Variant),
                x.OnHand.ToString("0.##"),
                value is { } amount ? decimal.Round(amount, 2, MidpointRounding.AwayFromZero).ToString() : ""
            ]);
        }

        var qtyTotal = decimal.Round(ordered.Sum(x => x.OnHand), 2, MidpointRounding.AwayFromZero);
        var valueTotal = decimal.Round(ordered.Sum(x => (x.Variant.StandardPurchasePrice ?? 0m) * x.OnHand), 2, MidpointRounding.AwayFromZero);
        if (ordered.Count > 0)
        {
            rows.Add(["", "الإجمالي", "", qtyTotal.ToString("0.##"), valueTotal.ToString()]);
        }

        return new ReportDto(
            "المخزون",
            ["المخزن", "الصنف", "العبوة", "المتاح", "القيمة"],
            rows,
            [
                new ReportKpiDto("إجمالي الكمية", qtyTotal.ToString("0.##"), $"{ordered.Count} عبوة"),
                new ReportKpiDto("قيمة المخزون", valueTotal.ToString(), "بسعر الشركة على العبوة")
            ],
            "القيمة = الكمية × سعر الشركة الحالي على العبوة.");
    }

    public async Task<ReportDto> InventoryMovementsAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryMovements.AsNoTracking().Include(x => x.Warehouse).Include(x => x.Variant).ThenInclude(x => x.Product).AsQueryable();
        if (filters.From is { } from) query = query.Where(x => x.OccurredAt >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.OccurredAt <= to.Date);
        if (filters.WarehouseId is { } warehouseId) query = query.Where(x => x.WarehouseId == warehouseId);
        if (filters.VariantId is { } variantId) query = query.Where(x => x.VariantId == variantId);
        var items = await query.OrderBy(x => x.OccurredAt).ToListAsync(cancellationToken);
        return new ReportDto(
            "حركة المخزون",
            ["التاريخ", "المخزن", "الصنف", "العبوة", "النوع", "الاتجاه", "الكمية"],
            items.Select(x => (IReadOnlyList<string?>)[
                x.OccurredAt.ToString("yyyy-MM-dd"),
                x.Warehouse.Name,
                x.Variant.Product.Name,
                PackLabel(x.Variant),
                x.MovementType,
                x.Direction,
                x.Quantity.ToString()]).ToList());
    }

    public async Task<ReportDto> PurchasingAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var query = _db.PurchaseInvoices.AsNoTracking().Include(x => x.Supplier).Where(x => x.Status == "posted");
        if (filters.From is { } from) query = query.Where(x => x.InvoiceDate >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.InvoiceDate <= to.Date);
        if (filters.SupplierId is { } supplierId) query = query.Where(x => x.SupplierId == supplierId);
        var items = await query.OrderBy(x => x.InvoiceDate).ToListAsync(cancellationToken);
        return new ReportDto(
            "المشتريات",
            ["التاريخ", "المورد", "الإجمالي", "الحالة"],
            items.Select(x => (IReadOnlyList<string?>)[x.InvoiceDate.ToString("yyyy-MM-dd"), x.Supplier.Name, x.GoodsTotal.ToString(), x.Status]).ToList());
    }

    public async Task<ReportDto> SupplierBalancesAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var suppliers = await _db.Suppliers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var rows = new List<IReadOnlyList<string?>>();
        foreach (var supplier in suppliers)
        {
            if (filters.SupplierId is { } id && supplier.Id != id) continue;
            var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Supplier, supplier.Id, cancellationToken);
            rows.Add([supplier.Code, supplier.Name, outstanding.ToString()]);
        }

        return new ReportDto("أرصدة الموردين", ["الكود", "المورد", "المستحق"], rows);
    }

    public async Task<ReportDto> CustomerBalancesAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var customers = await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var rows = new List<IReadOnlyList<string?>>();
        decimal owesTotal = 0m;
        decimal creditTotal = 0m;
        foreach (var customer in customers)
        {
            if (filters.CustomerId is { } id && customer.Id != id) continue;
            var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Customer, customer.Id, cancellationToken);
            var owes = outstanding > 0 ? outstanding : 0m;
            var credit = outstanding < 0 ? -outstanding : 0m;
            owesTotal += owes;
            creditTotal += credit;
            rows.Add([
                customer.Code,
                customer.Name,
                customer.Phone,
                owes.ToString(),
                credit.ToString(),
                outstanding.ToString()
            ]);
        }

        rows.Add(["", "الإجمالي", "", owesTotal.ToString(), creditTotal.ToString(), (owesTotal - creditTotal).ToString()]);
        return new ReportDto(
            "كشف حساب العملاء",
            ["الكود", "العميل", "الهاتف", "عليه", "له", "الصافي"],
            rows,
            [
                new ReportKpiDto("إجمالي عليه", owesTotal.ToString("0.##")),
                new ReportKpiDto("إجمالي له", creditTotal.ToString("0.##")),
                new ReportKpiDto("صافي المستحق لنا", (owesTotal - creditTotal).ToString("0.##"))
            ],
            "عليه = العميل مدين لنا. له = رصيد لصالح العميل (دفع زيادة أو مرتجع).");
    }

    public async Task<CustomerStatementDto?> CustomerStatementAsync(Guid customerId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null) return null;

        var entries = await _db.PartyLedgerEntries.AsNoTracking()
            .Where(x => x.PartyKind == PartyKinds.Customer && x.PartyId == customerId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var fromDate = from?.Date;
        var toDate = to?.Date;

        decimal opening = 0m;
        if (fromDate is { } start)
        {
            opening = entries.Where(x => x.OccurredAt.Date < start).Sum(x => x.SignedAmount);
        }

        var period = entries.AsEnumerable();
        if (fromDate is { } f) period = period.Where(x => x.OccurredAt.Date >= f);
        if (toDate is { } t) period = period.Where(x => x.OccurredAt.Date <= t);
        var periodEntries = period.ToList();

        var invoiceIds = periodEntries
            .Where(x => x.EntryType == LedgerEntryTypes.SalesInvoice || x.SourceDocumentType == SourceDocumentTypes.SalesInvoice)
            .Select(x => x.SourceDocumentId)
            .Distinct()
            .ToList();
        var paymentIds = periodEntries
            .Where(x => x.EntryType == LedgerEntryTypes.Payment || x.SourceDocumentType == SourceDocumentTypes.Payment)
            .Select(x => x.SourceDocumentId)
            .Distinct()
            .ToList();

        var invoices = invoiceIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _db.SalesInvoices.AsNoTracking()
                .Where(x => invoiceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number, cancellationToken);

            var payments = paymentIds.Count == 0
            ? []
            : await _db.Payments.AsNoTracking()
                .Include(x => x.Invoice)
                .Include(x => x.Allocations).ThenInclude(x => x.Invoice)
                .Where(x => paymentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var paymentMap = payments.ToDictionary(x => x.Id);

        var running = opening;
        var lines = new List<CustomerStatementLineDto>();
        foreach (var entry in periodEntries)
        {
            running += entry.SignedAmount;
            var debit = entry.SignedAmount > 0 ? entry.SignedAmount : 0m;
            var credit = entry.SignedAmount < 0 ? -entry.SignedAmount : 0m;
            string document;
            string description;
            if (entry.EntryType == LedgerEntryTypes.SalesInvoice || entry.SourceDocumentType == SourceDocumentTypes.SalesInvoice)
            {
                var number = invoices.GetValueOrDefault(entry.SourceDocumentId);
                document = string.IsNullOrWhiteSpace(number) ? "فاتورة مبيعات" : number!;
                description = "فاتورة مبيعات";
            }
            else if (entry.EntryType == LedgerEntryTypes.Payment || entry.SourceDocumentType == SourceDocumentTypes.Payment)
            {
                if (paymentMap.TryGetValue(entry.SourceDocumentId, out var payment))
                {
                    var allocated = payment.Allocations
                        .Select(x => x.Invoice?.Number)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Cast<string>()
                        .Distinct()
                        .ToList();
                    if (allocated.Count == 1)
                    {
                        document = allocated[0];
                        description = "تحصيل على فاتورة";
                    }
                    else if (allocated.Count > 1)
                    {
                        document = string.Join(" + ", allocated);
                        description = "تحصيل موزع";
                    }
                    else if (!string.IsNullOrWhiteSpace(payment.Invoice?.Number))
                    {
                        document = payment.Invoice!.Number!;
                        description = "تحصيل على فاتورة";
                    }
                    else
                    {
                        document = "تحصيل";
                        description = "تحصيل";
                    }
                }
                else
                {
                    document = "تحصيل";
                    description = "تحصيل";
                }
            }
            else if (entry.EntryType == LedgerEntryTypes.SalesReturn || entry.SourceDocumentType == SourceDocumentTypes.SalesReturn)
            {
                document = "مرتجع مبيعات";
                description = string.IsNullOrWhiteSpace(entry.Notes) ? "مرتجع مبيعات" : entry.Notes;
            }
            else
            {
                document = string.IsNullOrWhiteSpace(entry.EntryType) ? "حركة" : entry.EntryType;
                description = string.IsNullOrWhiteSpace(entry.Notes) ? "تعديل يدوي / حركة حساب" : entry.Notes;
            }

            lines.Add(new CustomerStatementLineDto(
                entry.OccurredAt,
                document,
                description,
                debit,
                credit,
                decimal.Round(running, 4, MidpointRounding.AwayFromZero),
                entry.SourceDocumentId,
                entry.EntryType));
        }

        var totalDebits = lines.Sum(x => x.Debit);
        var totalCredits = lines.Sum(x => x.Credit);
        return new CustomerStatementDto(
            customer.Id,
            customer.Name,
            customer.Code,
            customer.Phone,
            fromDate,
            toDate,
            decimal.Round(opening, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalDebits, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalCredits, 4, MidpointRounding.AwayFromZero),
            decimal.Round(opening + totalDebits - totalCredits, 4, MidpointRounding.AwayFromZero),
            lines);
    }

    public async Task<SupplierStatementDto?> SupplierStatementAsync(Guid supplierId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var supplier = await _db.Suppliers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null) return null;

        var entries = await _db.PartyLedgerEntries.AsNoTracking()
            .Where(x => x.PartyKind == PartyKinds.Supplier && x.PartyId == supplierId)
            .OrderBy(x => x.OccurredAt)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var fromDate = from?.Date;
        var toDate = to?.Date;

        decimal opening = 0m;
        if (fromDate is { } start)
        {
            opening = entries.Where(x => x.OccurredAt.Date < start).Sum(x => x.SignedAmount);
        }

        var period = entries.AsEnumerable();
        if (fromDate is { } f) period = period.Where(x => x.OccurredAt.Date >= f);
        if (toDate is { } t) period = period.Where(x => x.OccurredAt.Date <= t);
        var periodEntries = period.ToList();

        var invoiceIds = periodEntries
            .Where(x => x.EntryType == LedgerEntryTypes.PurchaseInvoice || x.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice)
            .Select(x => x.SourceDocumentId)
            .Distinct()
            .ToList();
        var paymentIds = periodEntries
            .Where(x => x.EntryType == LedgerEntryTypes.SupplierPayment || x.SourceDocumentType == SourceDocumentTypes.SupplierPayment)
            .Select(x => x.SourceDocumentId)
            .Distinct()
            .ToList();

        var invoices = invoiceIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _db.PurchaseInvoices.AsNoTracking()
                .Where(x => invoiceIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number, cancellationToken);

        var payments = paymentIds.Count == 0
            ? new Dictionary<Guid, string?>()
            : await _db.SupplierPayments.AsNoTracking()
                .Where(x => paymentIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Reference, cancellationToken);

        var running = opening;
        var lines = new List<CustomerStatementLineDto>();
        foreach (var entry in periodEntries)
        {
            running += entry.SignedAmount;
            var debit = entry.SignedAmount > 0 ? entry.SignedAmount : 0m;
            var credit = entry.SignedAmount < 0 ? -entry.SignedAmount : 0m;
            string document;
            string description;
            if (entry.EntryType == LedgerEntryTypes.PurchaseInvoice || entry.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice)
            {
                var number = invoices.GetValueOrDefault(entry.SourceDocumentId);
                document = string.IsNullOrWhiteSpace(number) ? "فاتورة مشتريات" : number!;
                description = "فاتورة مشتريات";
            }
            else if (entry.EntryType == LedgerEntryTypes.SupplierPayment || entry.SourceDocumentType == SourceDocumentTypes.SupplierPayment)
            {
                var reference = payments.GetValueOrDefault(entry.SourceDocumentId);
                document = string.IsNullOrWhiteSpace(reference) ? "دفعة للمورد" : reference!;
                description = "دفعة للمورد";
            }
            else
            {
                document = string.IsNullOrWhiteSpace(entry.EntryType) ? "حركة" : entry.EntryType;
                description = string.IsNullOrWhiteSpace(entry.Notes) ? "تعديل يدوي / حركة حساب" : entry.Notes;
            }

            lines.Add(new CustomerStatementLineDto(
                entry.OccurredAt,
                document,
                description,
                debit,
                credit,
                decimal.Round(running, 4, MidpointRounding.AwayFromZero),
                entry.SourceDocumentId,
                entry.EntryType));
        }

        var totalDebits = lines.Sum(x => x.Debit);
        var totalCredits = lines.Sum(x => x.Credit);
        return new SupplierStatementDto(
            supplier.Id,
            supplier.Name,
            supplier.Code,
            supplier.Phone,
            fromDate,
            toDate,
            decimal.Round(opening, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalDebits, 4, MidpointRounding.AwayFromZero),
            decimal.Round(totalCredits, 4, MidpointRounding.AwayFromZero),
            decimal.Round(opening + totalDebits - totalCredits, 4, MidpointRounding.AwayFromZero),
            lines);
    }

    public async Task<ReportDto> ProductTrackAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var lines = await PostedSaleLines(filters, cancellationToken);
        var grouped = lines
            .GroupBy(x => x.VariantId)
            .Select(g =>
            {
                var first = g.First();
                var qty = g.Sum(x => x.Quantity);
                var sales = g.Sum(x => x.LineTotal ?? ((x.ResolvedUnitPrice ?? x.UnitPrice ?? 0m) * x.Quantity));
                var costEach = first.Variant.StandardPurchasePrice;
                var cost = costEach is { } price ? price * qty : (decimal?)null;
                var profit = cost is { } c ? sales - c : (decimal?)null;
                return new
                {
                    Category = first.Variant.Product.Category,
                    ProductName = first.Variant.Product.Name,
                    Pack = PackLabel(first.Variant),
                    Brand = first.Variant.Product.Brand,
                    Spec = first.Variant.Product.Specification,
                    Qty = qty,
                    Sales = sales,
                    Cost = cost,
                    Profit = profit
                };
            })
            .OrderBy(x => CatalogDisplayOrder.CategoryRank(x.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.ProductName, x.Brand, x.Spec))
            .ThenBy(x => CatalogDisplayOrder.PackagingRank(x.Pack))
            .ToList();

        var rows = new List<IReadOnlyList<string?>>();
        string? lastCategory = null;
        foreach (var row in grouped)
        {
            var title = CatalogDisplayOrder.CategoryTitle(row.Category);
            if (title != lastCategory)
            {
                rows.Add([title, "", "", "", "", ""]);
                lastCategory = title;
            }

            rows.Add([
                row.ProductName,
                row.Pack,
                row.Qty.ToString("0.##"),
                decimal.Round(row.Sales, 2, MidpointRounding.AwayFromZero).ToString(),
                row.Cost is { } cost ? decimal.Round(cost, 2, MidpointRounding.AwayFromZero).ToString() : "",
                row.Profit is { } profit ? decimal.Round(profit, 2, MidpointRounding.AwayFromZero).ToString() : ""
            ]);
        }

        var trackSales = decimal.Round(grouped.Sum(x => x.Sales), 2, MidpointRounding.AwayFromZero);
        var trackCost = decimal.Round(grouped.Where(x => x.Cost is not null).Sum(x => x.Cost!.Value), 2, MidpointRounding.AwayFromZero);
        var trackProfit = decimal.Round(grouped.Where(x => x.Profit is not null).Sum(x => x.Profit!.Value), 2, MidpointRounding.AwayFromZero);
        if (grouped.Count > 0)
        {
            rows.Add([
                "",
                "الإجمالي",
                grouped.Sum(x => x.Qty).ToString("0.##"),
                trackSales.ToString(),
                trackCost.ToString(),
                trackProfit.ToString()
            ]);
        }

        return new ReportDto(
            "تتبع الأصناف",
            ["الصنف", "العبوة", "الكمية", "المبيعات", "التكلفة", "المكسب"],
            rows,
            [
                new ReportKpiDto("العبوات", grouped.Count.ToString(), "كل سطر عبوة محددة مش الاسم العام"),
                new ReportKpiDto("المبيعات", trackSales.ToString()),
                new ReportKpiDto("التكلفة", trackCost.ToString(), "بسعر الشركة على العبوة"),
                new ReportKpiDto("المكسب", trackProfit.ToString())
            ],
            "كل سطر عبوة محددة (مثل 4 لتر × 3 أو 3×5) وليس الاسم العام فقط. التكلفة بسعر الشركة المسجّل على العبوة.");
    }

    public async Task<ReportDto> ProfitAsync(ReportFilters filters, CancellationToken cancellationToken = default)
    {
        var invoices = await PostedInvoices(filters)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .ToListAsync(cancellationToken);
        var saleLines = invoices.SelectMany(x => x.Lines).ToList();
        if (filters.VariantId is { } variantId)
        {
            saleLines = saleLines.Where(x => x.VariantId == variantId).ToList();
        }

        var returnsQuery = _db.SalesReturns.AsNoTracking()
            .Include(x => x.OriginalSalesInvoice).ThenInclude(x => x!.Lines)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .Where(x => x.Status == ReturnStatuses.Posted);
        if (filters.From is { } from) returnsQuery = returnsQuery.Where(x => x.ReturnDate >= from.Date);
        if (filters.To is { } to) returnsQuery = returnsQuery.Where(x => x.ReturnDate <= to.Date);
        if (filters.CustomerId is { } customerId) returnsQuery = returnsQuery.Where(x => x.CustomerId == customerId);
        var returns = await returnsQuery.ToListAsync(cancellationToken);

        var salesTotal = invoices.Sum(x => x.GoodsTotal);
        if (filters.VariantId is not null)
        {
            salesTotal = saleLines.Sum(x => x.LineTotal ?? ((x.ResolvedUnitPrice ?? x.UnitPrice ?? 0m) * x.Quantity));
        }

        var missingCost = 0;
        var cogs = 0m;
        foreach (var line in saleLines)
        {
            if (line.Variant.StandardPurchasePrice is { } price)
            {
                cogs += price * line.Quantity;
            }
            else
            {
                missingCost++;
            }
        }

        foreach (var ret in returns)
        {
            foreach (var line in ret.Lines)
            {
                if (filters.VariantId is { } vid && line.VariantId != vid)
                {
                    continue;
                }

                var orig = ret.OriginalSalesInvoice?.Lines.FirstOrDefault(x => x.VariantId == line.VariantId);
                var unit = orig is null
                    ? (line.Variant.StandardWholesalePrice ?? 0m)
                    : (orig.ResolvedUnitPrice ?? orig.UnitPrice ?? (orig.Quantity == 0 ? 0m : (orig.LineTotal ?? 0m) / orig.Quantity));
                salesTotal -= unit * line.Quantity;
                if (line.Variant.StandardPurchasePrice is { } price)
                {
                    cogs -= price * line.Quantity;
                }
            }
        }

        salesTotal = decimal.Round(salesTotal, 2, MidpointRounding.AwayFromZero);
        cogs = decimal.Round(cogs, 2, MidpointRounding.AwayFromZero);
        var gross = decimal.Round(salesTotal - cogs, 2, MidpointRounding.AwayFromZero);
        var margin = salesTotal == 0 ? 0m : decimal.Round(gross / salesTotal * 100m, 1, MidpointRounding.AwayFromZero);

        var treasury = await _db.TreasuryEntries.AsNoTracking().ToListAsync(cancellationToken);
        IEnumerable<TreasuryEntry> expenseRows = treasury.Where(x => x.Direction == TreasuryDirections.Out);
        if (filters.From is { } expFrom) expenseRows = expenseRows.Where(x => x.OccurredOn.Date >= expFrom.Date);
        if (filters.To is { } expTo) expenseRows = expenseRows.Where(x => x.OccurredOn.Date <= expTo.Date);
        var operatingCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TreasuryCategories.Salaries,
            TreasuryCategories.Rent,
            TreasuryCategories.OfficeExpense,
            TreasuryCategories.OfficePurchases,
            TreasuryCategories.WarehouseExpense,
            TreasuryCategories.CarExpense,
            TreasuryCategories.Other
        };
        var expenses = expenseRows
            .Where(x => x.Category is not null && operatingCats.Contains(x.Category))
            .GroupBy(x => x.Category!)
            .Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();
        var operating = decimal.Round(expenses.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        var net = decimal.Round(gross - operating, 2, MidpointRounding.AwayFromZero);

        var collectionsQuery = _db.Payments.AsNoTracking().Where(x => x.PartyKind == PartyKinds.Customer);
        if (filters.From is { } payFrom) collectionsQuery = collectionsQuery.Where(x => x.PaidOn >= payFrom.Date);
        if (filters.To is { } payTo) collectionsQuery = collectionsQuery.Where(x => x.PaidOn <= payTo.Date);
        if (filters.CustomerId is { } payCustomer) collectionsQuery = collectionsQuery.Where(x => x.PartyId == payCustomer);
        var collectionAmounts = await collectionsQuery.Select(x => x.Amount).ToListAsync(cancellationToken);
        var collections = decimal.Round(collectionAmounts.Sum(), 2, MidpointRounding.AwayFromZero);

        var recvQuery = _db.PartyLedgerEntries.AsNoTracking().Where(x => x.PartyKind == PartyKinds.Customer);
        if (filters.CustomerId is { } recvCustomer) recvQuery = recvQuery.Where(x => x.PartyId == recvCustomer);
        var recvAmounts = await recvQuery.Select(x => x.SignedAmount).ToListAsync(cancellationToken);
        var receivables = decimal.Round(recvAmounts.Sum(), 2, MidpointRounding.AwayFromZero);

        var productRows = saleLines
            .GroupBy(x => x.VariantId)
            .Select(g =>
            {
                var first = g.First();
                var qty = g.Sum(x => x.Quantity);
                var sales = g.Sum(x => x.LineTotal ?? ((x.ResolvedUnitPrice ?? x.UnitPrice ?? 0m) * x.Quantity));
                var cost = first.Variant.StandardPurchasePrice is { } price ? price * qty : (decimal?)null;
                var profit = cost is { } c ? sales - c : (decimal?)null;
                return new
                {
                    first.Variant.Product.Name,
                    Pack = PackLabel(first.Variant),
                    Qty = qty,
                    Sales = sales,
                    Cost = cost,
                    Profit = profit
                };
            })
            .OrderByDescending(x => x.Profit ?? x.Sales)
            .ToList();

        var expensePct = salesTotal == 0 ? 0m : decimal.Round(operating / salesTotal * 100m, 1, MidpointRounding.AwayFromZero);
        var netPct = salesTotal == 0 ? 0m : decimal.Round(net / salesTotal * 100m, 1, MidpointRounding.AwayFromZero);
        var winners = productRows.Count(x => (x.Profit ?? 0m) > 0m);
        var top = productRows.FirstOrDefault();
        var collectionPct = salesTotal == 0 ? 0m : decimal.Round(collections / salesTotal * 100m, 1, MidpointRounding.AwayFromZero);

        var tableRows = productRows.Select(x => (IReadOnlyList<string?>)[
            x.Name,
            x.Pack,
            x.Qty.ToString("0.##"),
            decimal.Round(x.Sales, 2, MidpointRounding.AwayFromZero).ToString(),
            x.Cost is { } cost ? decimal.Round(cost, 2, MidpointRounding.AwayFromZero).ToString() : "",
            x.Profit is { } profit ? decimal.Round(profit, 2, MidpointRounding.AwayFromZero).ToString() : ""
        ]).ToList();
        if (productRows.Count > 0)
        {
            tableRows.Add([
                "",
                "الإجمالي",
                productRows.Sum(x => x.Qty).ToString("0.##"),
                decimal.Round(productRows.Sum(x => x.Sales), 2, MidpointRounding.AwayFromZero).ToString(),
                decimal.Round(productRows.Where(x => x.Cost is not null).Sum(x => x.Cost!.Value), 2, MidpointRounding.AwayFromZero).ToString(),
                decimal.Round(productRows.Where(x => x.Profit is not null).Sum(x => x.Profit!.Value), 2, MidpointRounding.AwayFromZero).ToString()
            ]);
        }

        var expenseSection = new ReportDto(
            "مصروفات التشغيل",
            ["التصنيف", "المبلغ"],
            expenses.Select(x => (IReadOnlyList<string?>)[ExpenseLabel(x.Category), decimal.Round(x.Amount, 2, MidpointRounding.AwayFromZero).ToString()]).ToList());

        return new ReportDto(
            "مكسب الشركة",
            ["الصنف", "العبوة", "الكمية", "المبيعات", "التكلفة", "المكسب"],
            tableRows,
            [
                new ReportKpiDto("المبيعات المرحلة", salesTotal.ToString(), $"{invoices.Count} فاتورة"),
                new ReportKpiDto("تكلفة البضاعة", cogs.ToString(), "بسعر الشركة على العبوة"),
                new ReportKpiDto("مجمل الربح", gross.ToString(), $"{margin}٪ من المبيعات"),
                new ReportKpiDto("مصروفات التشغيل", operating.ToString(), $"{expensePct}٪ من المبيعات"),
                new ReportKpiDto("صافي الربح", net.ToString(), $"{netPct}٪ من المبيعات"),
                new ReportKpiDto("التحصيل", collections.ToString(), $"{collectionPct}٪ من المبيعات"),
                new ReportKpiDto("المتبقي على العملاء", receivables.ToString(), "الرصيد الحالي"),
                new ReportKpiDto("عبوات رابحة", winners.ToString(), $"{productRows.Count} عبوة في التقرير"),
                new ReportKpiDto(
                    "أعلى مكسب",
                    top?.Profit is { } topProfit ? topProfit.ToString() : "0",
                    top is null ? "لا توجد مبيعات في الفترة" : $"{top.Name} — {top.Pack}")
            ],
            missingCost > 0
                ? $"كل سطر عبوة محددة وليس الاسم العام. التكلفة من سعر الشركة الحالي على العبوة، مش تكلفة تاريخية لكل فاتورة. {missingCost} بند بدون سعر شركة فلم يدخل في التكلفة. سداد الموردين وإيداع البنك والمسحوبات مش مصروف تشغيل."
                : "كل سطر عبوة محددة وليس الاسم العام. التكلفة من سعر الشركة الحالي على العبوة، مش تكلفة تاريخية لكل فاتورة. سداد الموردين وإيداع البنك والمسحوبات مش مصروف تشغيل.",
            [expenseSection]);
    }

    private async Task<List<SalesInvoiceLine>> PostedSaleLines(ReportFilters filters, CancellationToken cancellationToken)
    {
        var query = _db.SalesInvoiceLines.AsNoTracking()
            .Include(x => x.Invoice)
            .Include(x => x.Variant).ThenInclude(x => x.Product)
            .Where(x => x.Invoice.Status == SalesStatuses.Posted);
        if (filters.From is { } from) query = query.Where(x => x.Invoice.InvoiceDate >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.Invoice.InvoiceDate <= to.Date);
        if (filters.CustomerId is { } customerId) query = query.Where(x => x.Invoice.CustomerId == customerId);
        if (filters.VariantId is { } variantId) query = query.Where(x => x.VariantId == variantId);
        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<SalesInvoice> PostedInvoices(ReportFilters filters)
    {
        var query = _db.SalesInvoices.AsNoTracking().Include(x => x.Customer).Where(x => x.Status == SalesStatuses.Posted);
        if (filters.From is { } from) query = query.Where(x => x.InvoiceDate >= from.Date);
        if (filters.To is { } to) query = query.Where(x => x.InvoiceDate <= to.Date);
        if (filters.CustomerId is { } customerId) query = query.Where(x => x.CustomerId == customerId);
        if (filters.VariantId is { } variantId) query = query.Where(x => x.Lines.Any(line => line.VariantId == variantId));
        return query;
    }

    private static string PackLabel(ProductVariant variant)
        => $"{variant.PackagingType} {variant.PackagingSize}".Trim();

    private static string ExpenseLabel(string category) => category switch
    {
        TreasuryCategories.Salaries => "رواتب",
        TreasuryCategories.Rent => "إيجار",
        TreasuryCategories.OfficeExpense => "مصروف مكتب",
        TreasuryCategories.OfficePurchases => "مشتريات مكتب",
        TreasuryCategories.WarehouseExpense => "مصروفات المخزن",
        TreasuryCategories.CarExpense => "مصاريف سيارة",
        TreasuryCategories.Other => "غير ذلك",
        _ => category
    };
}
