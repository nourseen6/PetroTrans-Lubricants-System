using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

internal static class InvoiceSettlement
{
    public static async Task<decimal> PostedReturnValueAsync(AppDbContext db, Guid invoiceId, Guid? exceptReturnId, CancellationToken cancellationToken)
    {
        var map = await PostedReturnValuesAsync(db, [invoiceId], exceptReturnId, cancellationToken);
        return map.GetValueOrDefault(invoiceId);
    }

    public static async Task<Dictionary<Guid, decimal>> PostedReturnValuesAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> invoiceIds,
        Guid? exceptReturnId,
        CancellationToken cancellationToken)
    {
        var ids = invoiceIds.Distinct().ToList();
        var result = ids.ToDictionary(x => x, _ => 0m);
        if (ids.Count == 0)
        {
            return result;
        }

        var invoices = await db.SalesInvoices
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (invoices.Count == 0)
        {
            return result;
        }

        var query = db.SalesReturns.AsNoTracking()
            .Where(x => x.Status == ReturnStatuses.Posted
                && x.OriginalSalesInvoiceId != null
                && ids.Contains(x.OriginalSalesInvoiceId.Value));
        if (exceptReturnId is { } skip)
        {
            query = query.Where(x => x.Id != skip);
        }

        var headers = await query
            .Select(x => new { x.Id, InvoiceId = x.OriginalSalesInvoiceId!.Value })
            .ToListAsync(cancellationToken);
        var returnIds = headers.Select(x => x.Id).ToList();
        var lineRows = returnIds.Count == 0
            ? []
            : await db.SalesReturnLines.AsNoTracking()
                .Where(x => returnIds.Contains(x.ReturnId))
                .Select(x => new { x.ReturnId, x.VariantId, x.Quantity })
                .ToListAsync(cancellationToken);
        var invoiceByReturn = headers.ToDictionary(x => x.Id, x => x.InvoiceId);
        var returnLines = lineRows
            .Where(x => invoiceByReturn.ContainsKey(x.ReturnId))
            .Select(x => new { InvoiceId = invoiceByReturn[x.ReturnId], x.VariantId, x.Quantity })
            .ToList();

        foreach (var invoice in invoices)
        {
            var lines = returnLines.Where(x => x.InvoiceId == invoice.Id).Select(x => (x.VariantId, x.Quantity));
            result[invoice.Id] = CreditForReturnLines(invoice, lines);
        }

        return result;
    }

    public static decimal CreditForReturnLines(SalesInvoice invoice, IEnumerable<(Guid VariantId, decimal Quantity)> returnLines)
    {
        var unitValues = UnitValues(invoice);
        decimal total = 0m;
        foreach (var (variantId, quantity) in returnLines)
        {
            if (quantity <= 0 || !unitValues.TryGetValue(variantId, out var unit))
            {
                continue;
            }

            total += unit * quantity;
        }

        return decimal.Round(total, 4, MidpointRounding.AwayFromZero);
    }

    public static Dictionary<Guid, decimal> UnitValues(SalesInvoice invoice)
    {
        var scale = 1m;
        if (invoice.HasManualTotal && invoice.LinesSubtotal > 0)
        {
            scale = invoice.GoodsTotal / invoice.LinesSubtotal;
        }

        var map = new Dictionary<Guid, decimal>();
        foreach (var group in invoice.Lines.GroupBy(x => x.VariantId))
        {
            var qty = group.Sum(x => x.Quantity);
            if (qty <= 0)
            {
                continue;
            }

            var value = group.Sum(LineValue) * scale;
            map[group.Key] = value / qty;
        }

        return map;
    }

    public static void ApplyRemaining(SalesInvoice invoice, decimal paid, decimal returned)
    {
        paid = decimal.Round(paid, 4, MidpointRounding.AwayFromZero);
        returned = decimal.Round(returned, 4, MidpointRounding.AwayFromZero);
        invoice.PaidTotal = paid;
        var remaining = decimal.Round(invoice.GoodsTotal - paid - returned, 4, MidpointRounding.AwayFromZero);
        if (remaining < 0)
        {
            remaining = 0m;
        }

        invoice.RemainingTotal = remaining;
        invoice.PaymentStatus = remaining == 0
            ? PaymentStatuses.Paid
            : paid == 0 && returned == 0
                ? PaymentStatuses.Unpaid
                : PaymentStatuses.Partial;
    }

    private static decimal LineValue(SalesInvoiceLine line)
    {
        if (line.LineTotal.HasValue)
        {
            return line.LineTotal.Value;
        }

        var unit = line.UnitPrice ?? line.ResolvedUnitPrice ?? 0m;
        return unit * line.Quantity;
    }
}
