using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

/// <summary>
/// Profit, months, products and customers for the analysis page. Uses the profit report's rules:
/// sales are posted invoices less returns, cost is the company price on the pack, and expenses are
/// every outgoing item that <see cref="TreasuryCategories.IsOperatingExpense"/> counts.
/// </summary>
public sealed class AnalyticsService : IAnalyticsService
{
    private const int MaxMonths = 24;
    private readonly AppDbContext _db;

    public AnalyticsService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AnalyticsDto> GetAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var start = from?.Date;
        var end = to?.Date;
        bool InPeriod(DateTime day) => (start is null || day.Date >= start) && (end is null || day.Date <= end);

        var allInvoices = await _db.SalesInvoices.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .Where(x => x.Status == SalesStatuses.Posted)
            .ToListAsync(cancellationToken);
        var invoices = allInvoices.Where(x => InPeriod(x.InvoiceDate)).ToList();

        var returns = (await _db.SalesReturns.AsNoTracking()
                .Include(x => x.OriginalSalesInvoice).ThenInclude(x => x!.Lines)
                .Include(x => x.Lines).ThenInclude(x => x.Variant)
                .Where(x => x.Status == ReturnStatuses.Posted)
                .ToListAsync(cancellationToken))
            .Where(x => InPeriod(x.ReturnDate))
            .ToList();

        var treasury = (await _db.TreasuryEntries.AsNoTracking().ToListAsync(cancellationToken))
            .Where(x => InPeriod(x.OccurredOn))
            .ToList();
        var itemNames = await _db.TreasuryCategories.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Name, cancellationToken);

        // Sales and cost per line, with returns as negative lines on their own date.
        var lines = new List<Line>();
        var withoutCost = 0;
        foreach (var invoice in invoices)
        {
            foreach (var line in invoice.Lines)
            {
                var lineCost = line.Variant.StandardPurchasePrice is { } price ? price * line.Quantity : (decimal?)null;
                if (lineCost is null) withoutCost++;
                lines.Add(new Line(invoice.InvoiceDate, invoice.Id, invoice.CustomerId, line.VariantId, line.Quantity, LineSales(line), lineCost));
            }
        }

        foreach (var ret in returns)
        {
            foreach (var line in ret.Lines)
            {
                var original = ret.OriginalSalesInvoice?.Lines.FirstOrDefault(x => x.VariantId == line.VariantId);
                var unit = original is null
                    ? line.Variant.StandardWholesalePrice ?? 0m
                    : original.ResolvedUnitPrice ?? original.UnitPrice ?? (original.Quantity == 0 ? 0m : (original.LineTotal ?? 0m) / original.Quantity);
                var lineCost = line.Variant.StandardPurchasePrice is { } price ? -price * line.Quantity : (decimal?)null;
                lines.Add(new Line(ret.ReturnDate, null, ret.CustomerId, line.VariantId, -line.Quantity, -unit * line.Quantity, lineCost));
            }
        }

        var expenseRows = treasury
            .Where(x => x.Direction == TreasuryDirections.Out && TreasuryCategories.IsOperatingExpense(x.Category))
            .ToList();

        // Invoice totals carry discounts and manual adjustments, so sales use them; returns come off.
        var returnSales = lines.Where(x => x.InvoiceId is null).Sum(x => x.Sales);
        var sales = invoices.Sum(x => x.GoodsTotal) + returnSales;
        var cost = lines.Sum(x => x.Cost ?? 0m);
        var expenses = expenseRows.Sum(x => x.Amount);
        var gross = sales - cost;
        var net = gross - expenses;

        var collections = (await _db.Payments.AsNoTracking()
                .Where(x => x.PartyKind == PartyKinds.Customer)
                .Select(x => new { x.PaidOn, x.Amount })
                .ToListAsync(cancellationToken))
            .Where(x => InPeriod(x.PaidOn))
            .Sum(x => x.Amount);
        var balances = (await _db.PartyLedgerEntries.AsNoTracking()
                .Where(x => x.PartyKind == PartyKinds.Customer)
                .Select(x => new { x.PartyId, x.SignedAmount })
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.PartyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SignedAmount));

        var totals = new AnalyticsTotalsDto(
            Money(sales),
            Money(cost),
            Money(gross),
            Money(expenses),
            Money(net),
            invoices.Count,
            invoices.Count == 0 ? 0m : Money(invoices.Sum(x => x.GoodsTotal) / invoices.Count),
            invoices.Select(x => x.CustomerId).Distinct().Count(),
            Money(collections),
            Money(balances.Values.Sum()));

        var months = BuildMonths(start, end, invoices, lines, expenseRows);

        var variantInfo = allInvoices.SelectMany(x => x.Lines).Select(x => x.Variant)
            .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        var products = lines
            .GroupBy(x => x.VariantId)
            .Select(g =>
            {
                var variant = variantInfo.GetValueOrDefault(g.Key);
                var productSales = g.Sum(x => x.Sales);
                var hasCost = g.All(x => x.Cost is not null);
                var profit = hasCost ? productSales - g.Sum(x => x.Cost!.Value) : (decimal?)null;
                return new AnalyticsProductDto(
                    g.Key,
                    variant?.Product.Name ?? "—",
                    variant is null ? string.Empty : $"{variant.PackagingType} {variant.PackagingSize}".Trim(),
                    g.Sum(x => x.Quantity),
                    Money(productSales),
                    g.Where(x => x.InvoiceId is not null).Select(x => x.InvoiceId).Distinct().Count(),
                    g.Select(x => x.CustomerId).Distinct().Count(),
                    profit is null ? null : Money(profit.Value),
                    profit is null || productSales == 0 ? null : decimal.Round(profit.Value / productSales * 100m, 1),
                    g.Max(x => x.Day));
            })
            .Where(x => x.Quantity > 0 || x.Sales != 0)
            .OrderByDescending(x => x.Sales)
            .ToList();

        var soldInPeriod = products.Select(x => x.VariantId).ToHashSet();
        var lastSoldEver = allInvoices.SelectMany(x => x.Lines.Select(line => new { line.VariantId, x.InvoiceDate }))
            .GroupBy(x => x.VariantId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.InvoiceDate));
        var onHand = (await _db.InventoryBalances.AsNoTracking().Select(x => new { x.VariantId, x.OnHand }).ToListAsync(cancellationToken))
            .GroupBy(x => x.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.OnHand));
        var unsold = (await _db.ProductVariants.AsNoTracking()
                .Include(x => x.Product)
                .Where(x => x.IsActive && x.Product.IsActive)
                .ToListAsync(cancellationToken))
            .Where(x => !soldInPeriod.Contains(x.Id))
            .Select(x => new AnalyticsUnsoldDto(
                x.Id,
                x.Product.Name,
                $"{x.PackagingType} {x.PackagingSize}".Trim(),
                onHand.GetValueOrDefault(x.Id),
                lastSoldEver.TryGetValue(x.Id, out var last) ? last : null))
            .OrderByDescending(x => x.OnHand)
            .ThenBy(x => x.Name)
            .ToList();

        var today = DateTime.Today;
        var customerNames = allInvoices.Select(x => x.Customer).GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
        var customers = invoices
            .GroupBy(x => x.CustomerId)
            .Select(g =>
            {
                var customerLines = lines.Where(x => x.CustomerId == g.Key).ToList();
                var customerSales = g.Sum(x => x.GoodsTotal) + customerLines.Where(x => x.InvoiceId is null).Sum(x => x.Sales);
                var hasCost = customerLines.All(x => x.Cost is not null);
                var profit = hasCost ? customerSales - customerLines.Sum(x => x.Cost!.Value) : (decimal?)null;
                var lastPurchase = g.Max(x => x.InvoiceDate);
                return new AnalyticsCustomerDto(
                    g.Key,
                    customerNames.GetValueOrDefault(g.Key) ?? "—",
                    g.Count(),
                    Money(customerSales),
                    profit is null ? null : Money(profit.Value),
                    Money(g.Sum(x => x.GoodsTotal) / g.Count()),
                    lastPurchase,
                    (int)(today - lastPurchase.Date).TotalDays,
                    Money(balances.GetValueOrDefault(g.Key)));
            })
            .OrderByDescending(x => x.Sales)
            .ToList();

        var expenseItems = expenseRows
            .GroupBy(x => x.Category ?? TreasuryCategories.Other)
            .Select(g => new AnalyticsExpenseDto(g.Key, itemNames.GetValueOrDefault(g.Key) ?? g.Key, Money(g.Sum(x => x.Amount)), g.Count()))
            .OrderByDescending(x => x.Amount)
            .ToList();

        return new AnalyticsDto(from, to, totals, months, products, unsold, customers, expenseItems, withoutCost);
    }

    private static List<AnalyticsMonthDto> BuildMonths(DateTime? start, DateTime? end, List<SalesInvoice> invoices, List<Line> lines, List<TreasuryEntry> expenses)
    {
        // Without a start date the chart begins at the first month that has any activity (at most
        // 24 months back); with one, every month in the period (up to 24).
        var last = end ?? DateTime.Today;
        var activity = invoices.Select(x => x.InvoiceDate).Concat(lines.Select(x => x.Day)).Concat(expenses.Select(x => x.OccurredOn)).ToList();
        var first = start ?? (activity.Count > 0 ? activity.Min() : last);
        var cursor = new DateTime(first.Year, first.Month, 1);
        var stop = new DateTime(last.Year, last.Month, 1);
        if ((stop.Year - cursor.Year) * 12 + stop.Month - cursor.Month >= MaxMonths)
        {
            cursor = stop.AddMonths(-(MaxMonths - 1));
        }

        var months = new List<AnalyticsMonthDto>();
        for (; cursor <= stop; cursor = cursor.AddMonths(1))
        {
            bool Same(DateTime day) => day.Year == cursor.Year && day.Month == cursor.Month;
            var monthInvoices = invoices.Where(x => Same(x.InvoiceDate)).ToList();
            var monthLines = lines.Where(x => Same(x.Day)).ToList();
            var sales = monthInvoices.Sum(x => x.GoodsTotal) + monthLines.Where(x => x.InvoiceId is null).Sum(x => x.Sales);
            var cost = monthLines.Sum(x => x.Cost ?? 0m);
            var spent = expenses.Where(x => Same(x.OccurredOn)).Sum(x => x.Amount);
            months.Add(new AnalyticsMonthDto(cursor.Year, cursor.Month, Money(sales), Money(cost), Money(spent), Money(sales - cost - spent), monthInvoices.Count));
        }

        return months;
    }

    private static decimal LineSales(SalesInvoiceLine line)
        => line.LineTotal ?? (line.ResolvedUnitPrice ?? line.UnitPrice ?? 0m) * line.Quantity;

    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record Line(DateTime Day, Guid? InvoiceId, Guid CustomerId, Guid VariantId, decimal Quantity, decimal Sales, decimal? Cost);
}
