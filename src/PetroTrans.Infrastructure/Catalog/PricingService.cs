using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public sealed class PricingService : IPricingService
{
    private readonly AppDbContext _db;

    public PricingService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PricingMatrixRowDto>> ListMatrixAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var query = _db.ProductVariants.AsNoTracking().Include(x => x.Product).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Product.Name.Contains(term)
                || (x.Product.Brand != null && x.Product.Brand.Contains(term))
                || x.PackagingType.Contains(term)
                || x.PackagingSize.Contains(term)
                || (x.Sku != null && x.Sku.Contains(term)));
        }

        var variants = await query.ToListAsync(cancellationToken);
        variants = variants
            .OrderBy(x => CatalogDisplayOrder.CategoryRank(x.Product.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.Product.Name, x.Product.Brand, x.Product.Specification))
            .ThenBy(x => CatalogDisplayOrder.PackagingRank(x.PackagingSize))
            .ThenBy(x => x.PackagingSize, StringComparer.Ordinal)
            .ToList();

        var counts = await _db.CustomerVariantPrices.AsNoTracking()
            .GroupBy(x => x.VariantId)
            .Select(g => new { VariantId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VariantId, x => x.Count, cancellationToken);

        return variants.Select(x => new PricingMatrixRowDto(
            x.Id,
            x.ProductId,
            x.Product.Name,
            x.Product.Brand,
            x.PackagingType,
            x.PackagingSize,
            x.StandardWholesalePrice,
            counts.GetValueOrDefault(x.Id),
            x.IsActive,
            x.StandardPurchasePrice,
            x.Product.Category)).ToList();
    }

    public async Task<IReadOnlyList<CustomerPriceDto>> ListCustomerPricesAsync(Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.CustomerVariantPrices.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Variant).ThenInclude(x => x.Product)
            .AsQueryable();
        if (customerId is { } cid)
        {
            query = query.Where(x => x.CustomerId == cid);
        }

        var rows = await query
            .OrderBy(x => x.Customer.Name)
            .ThenBy(x => x.Variant.Product.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new CustomerPriceDto(
            x.Id,
            x.CustomerId,
            x.Customer.Name,
            x.VariantId,
            x.Variant.Product.Name,
            x.Variant.PackagingType,
            x.Variant.PackagingSize,
            x.UnitPrice)).ToList();
    }

    public async Task<OperationResult<CustomerPriceDto>> SetCustomerPriceAsync(
        Guid customerId,
        Guid variantId,
        decimal unitPrice,
        string? reason,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (unitPrice < 0)
        {
            return OperationResult<CustomerPriceDto>.Fail("السعر لا يمكن أن يكون سالباً.");
        }

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerPriceDto>.Fail("العميل غير موجود.", 404);
        }

        var variant = await _db.ProductVariants.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<CustomerPriceDto>.Fail("العبوة غير موجودة.", 404);
        }

        var rounded = decimal.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        var existing = await _db.CustomerVariantPrices
            .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.VariantId == variantId, cancellationToken);

        if (existing is null)
        {
            var created = new CustomerVariantPrice
            {
                Id = UuidV7.New(),
                CustomerId = customerId,
                VariantId = variantId,
                UnitPrice = rounded,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.CustomerVariantPrices.Add(created);
            AddHistory(created.Id, customerId, variantId, null, rounded, PriceChangeKinds.Set, reason, actorUserId);
            _db.AuditLogs.Add(Audits.Create(actorUserId, "pricing.customer.set", "customer_variant_price", created.Id, null, new { customerId, variantId, rounded, reason }));
            await _db.SaveChangesAsync(cancellationToken);
            return OperationResult<CustomerPriceDto>.Ok(new CustomerPriceDto(
                created.Id, customerId, customer.Name, variantId, variant.Product.Name, variant.PackagingType, variant.PackagingSize, rounded));
        }

        var before = existing.UnitPrice;
        existing.UnitPrice = rounded;
        existing.UpdatedByUserId = actorUserId;
        AddHistory(existing.Id, customerId, variantId, before, rounded, PriceChangeKinds.Update, reason, actorUserId);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "pricing.customer.update", "customer_variant_price", existing.Id, new { before }, new { rounded, reason }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerPriceDto>.Ok(new CustomerPriceDto(
            existing.Id, customerId, customer.Name, variantId, variant.Product.Name, variant.PackagingType, variant.PackagingSize, rounded));
    }

    public async Task<OperationResult<bool>> RemoveCustomerPriceAsync(
        Guid customerId,
        Guid variantId,
        string? reason,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.CustomerVariantPrices
            .FirstOrDefaultAsync(x => x.CustomerId == customerId && x.VariantId == variantId, cancellationToken);
        if (existing is null)
        {
            return OperationResult<bool>.Fail("لا يوجد سعر خاص لهذا العميل.", 404);
        }

        var before = existing.UnitPrice;
        var id = existing.Id;
        _db.CustomerVariantPrices.Remove(existing);
        AddHistory(id, customerId, variantId, before, null, PriceChangeKinds.Remove, reason, actorUserId);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "pricing.customer.remove", "customer_variant_price", id, new { before }, new { removed = true, reason }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<VariantDto>> UpdateBasePriceAsync(
        Guid variantId,
        decimal unitPrice,
        string? reason,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (unitPrice < 0)
        {
            return OperationResult<VariantDto>.Fail("السعر لا يمكن أن يكون سالباً.");
        }

        var variant = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<VariantDto>.Fail("العبوة غير موجودة.", 404);
        }

        var rounded = decimal.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        var before = variant.StandardWholesalePrice;
        variant.StandardWholesalePrice = rounded;
        variant.UpdatedByUserId = actorUserId;
        AddHistory(null, Guid.Empty, variantId, before, rounded, before is null ? PriceChangeKinds.Set : PriceChangeKinds.Update, reason, actorUserId);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "pricing.base.update", "product_variant", variant.Id, new { before }, new { rounded, reason }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(new VariantDto(
            variant.Id,
            variant.ProductId,
            variant.PackagingType,
            variant.PackagingSize,
            variant.Sku,
            variant.Barcode,
            variant.MinStock,
            variant.StandardWholesalePrice,
            variant.IsActive,
            variant.StandardPurchasePrice));
    }

    public async Task<OperationResult<VariantDto>> UpdatePurchasePriceAsync(
        Guid variantId,
        decimal unitPrice,
        string? reason,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (unitPrice < 0)
        {
            return OperationResult<VariantDto>.Fail("السعر لا يمكن أن يكون سالباً.");
        }

        var variant = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<VariantDto>.Fail("العبوة غير موجودة.", 404);
        }

        var rounded = decimal.Round(unitPrice, 4, MidpointRounding.AwayFromZero);
        var before = variant.StandardPurchasePrice;
        variant.StandardPurchasePrice = rounded;
        variant.UpdatedByUserId = actorUserId;
        AddHistory(null, Guid.Empty, variantId, before, rounded, before is null ? PriceChangeKinds.Set : PriceChangeKinds.Update, reason ?? "سعر الشركة / أدنوك (شراء)", actorUserId);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "pricing.purchase.update", "product_variant", variant.Id, new { before }, new { rounded, reason }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(new VariantDto(
            variant.Id,
            variant.ProductId,
            variant.PackagingType,
            variant.PackagingSize,
            variant.Sku,
            variant.Barcode,
            variant.MinStock,
            variant.StandardWholesalePrice,
            variant.IsActive,
            variant.StandardPurchasePrice));
    }

    public async Task<IReadOnlyList<PriceHistoryDto>> GetHistoryAsync(Guid? variantId = null, Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.CustomerVariantPriceHistories.AsNoTracking().AsQueryable();
        if (variantId is { } vid)
        {
            query = query.Where(x => x.VariantId == vid);
        }

        if (customerId is { } cid)
        {
            query = query.Where(x => x.CustomerId == cid);
        }

        var rows = await query.OrderByDescending(x => x.ChangedAt).Take(500).ToListAsync(cancellationToken);
        return rows.Select(x => new PriceHistoryDto(
            x.Id,
            x.CustomerVariantPriceId,
            x.CustomerId,
            x.VariantId,
            x.OldUnitPrice,
            x.NewUnitPrice,
            x.ChangeKind,
            x.Reason,
            x.ChangedAt,
            x.ChangedByUserId)).ToList();
    }

    private void AddHistory(
        Guid? customerVariantPriceId,
        Guid customerId,
        Guid variantId,
        decimal? oldPrice,
        decimal? newPrice,
        string changeKind,
        string? reason,
        Guid actorUserId)
    {
        _db.CustomerVariantPriceHistories.Add(new CustomerVariantPriceHistory
        {
            Id = UuidV7.New(),
            CustomerVariantPriceId = customerVariantPriceId,
            CustomerId = customerId,
            VariantId = variantId,
            OldUnitPrice = oldPrice,
            NewUnitPrice = newPrice,
            ChangeKind = changeKind,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = actorUserId
        });
    }
}
