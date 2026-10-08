using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public sealed class HomeService : IHomeService
{
    private readonly AppDbContext _db;

    public HomeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<HomeSummaryDto> GetSummaryAsync(string displayName, string userName, CancellationToken cancellationToken = default)
    {
        var customers = await _db.Customers.CountAsync(cancellationToken);
        var products = await _db.Products.CountAsync(cancellationToken);
        var variants = await _db.ProductVariants.CountAsync(cancellationToken);
        var drafts = await _db.SalesInvoices.CountAsync(x => x.Status == SalesStatuses.Draft, cancellationToken);
        var posted = await _db.SalesInvoices.CountAsync(x => x.Status == SalesStatuses.Posted, cancellationToken);
        var unpaid = await _db.SalesInvoices.CountAsync(x => x.Status == SalesStatuses.Posted && x.RemainingTotal > 0, cancellationToken);
        var balances = await _db.InventoryBalances
            .AsNoTracking()
            .Select(x => new { x.VariantId, x.OnHand, x.Variant.MinStock, x.Variant.IsActive })
            .ToListAsync(cancellationToken);
        var variantsWithBalance = balances.Select(x => x.VariantId).ToHashSet();
        var lowFromBalances = balances.Count(x => x.IsActive && x.OnHand < (x.MinStock ?? 1m));
        var activeWithoutBalance = await _db.ProductVariants
            .AsNoTracking()
            .CountAsync(x => x.IsActive && !variantsWithBalance.Contains(x.Id), cancellationToken);
        var lowStock = lowFromBalances + activeWithoutBalance;

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var salesTotal = (await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.Status == SalesStatuses.Posted)
            .Select(x => x.GoodsTotal)
            .ToListAsync(cancellationToken)).Sum();
        var todaySales = (await _db.SalesInvoices
            .AsNoTracking()
            .Where(x => x.Status == SalesStatuses.Posted && x.InvoiceDate >= today && x.InvoiceDate < tomorrow)
            .Select(x => x.GoodsTotal)
            .ToListAsync(cancellationToken)).Sum();
        var collectionsTotal = (await _db.Payments
            .AsNoTracking()
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken)).Sum();
        var todayCollections = (await _db.Payments
            .AsNoTracking()
            .Where(x => x.PaidOn >= today && x.PaidOn < tomorrow)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken)).Sum();
        var customerReceivables = (await _db.PartyLedgerEntries
            .AsNoTracking()
            .Where(x => x.PartyKind == PartyKinds.Customer)
            .Select(x => x.SignedAmount)
            .ToListAsync(cancellationToken)).Sum();
        var adnocPayable = (await _db.PartyLedgerEntries
            .AsNoTracking()
            .Where(x => x.PartyKind == PartyKinds.Supplier)
            .Select(x => x.SignedAmount)
            .ToListAsync(cancellationToken)).Sum();

        var stockRows = await _db.InventoryBalances
            .AsNoTracking()
            .Where(x => x.OnHand != 0)
            .Select(x => new { x.OnHand, Price = x.Variant.StandardPurchasePrice })
            .ToListAsync(cancellationToken);
        var inventoryValue = stockRows.Sum(x => x.OnHand * (x.Price ?? 0m));

        return new HomeSummaryDto(
            displayName,
            userName,
            customers,
            products,
            variants,
            drafts,
            posted,
            unpaid,
            lowStock,
            decimal.Round(salesTotal, 2, MidpointRounding.AwayFromZero),
            decimal.Round(collectionsTotal, 2, MidpointRounding.AwayFromZero),
            decimal.Round(customerReceivables, 2, MidpointRounding.AwayFromZero),
            decimal.Round(adnocPayable, 2, MidpointRounding.AwayFromZero),
            decimal.Round(inventoryValue, 2, MidpointRounding.AwayFromZero),
            decimal.Round(todaySales, 2, MidpointRounding.AwayFromZero),
            decimal.Round(todayCollections, 2, MidpointRounding.AwayFromZero));
    }
}
