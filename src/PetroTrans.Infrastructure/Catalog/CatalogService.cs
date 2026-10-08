using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Assets;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public sealed class CatalogService : ICatalogService
{
    private readonly AppDbContext _db;

    public CatalogService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProductListItemDto>> ListProductsAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = includeInactive
            ? _db.Products.IgnoreQueryFilters().AsNoTracking().Include(x => x.Variants).Include(x => x.Media).AsQueryable()
            : _db.Products.AsNoTracking().Include(x => x.Variants).Include(x => x.Media).AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Name.Contains(term)
                || (x.Brand != null && x.Brand.Contains(term))
                || x.Variants.Any(v =>
                    (v.Sku != null && v.Sku.Contains(term))
                    || (v.Barcode != null && v.Barcode.Contains(term))));
        }

        var products = await query.ToListAsync(cancellationToken);
        return products
            .OrderBy(x => CatalogDisplayOrder.CategoryRank(x.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.Name, x.Brand, x.Specification))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => new ProductListItemDto(
                x.Id,
                x.Name,
                x.Brand,
                x.IsActive,
                x.Variants.Count,
                OfficialAssets.ToPublicUrl(x.Media.FirstOrDefault()?.RelativePath),
                x.Category)).ToList();
    }

    public async Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Variants)
            .Include(x => x.Media)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return product is null ? null : Map(product);
    }

    public async Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<ProductDto>.Fail("اسم الصنف مطلوب.");
        }

        if (request.Variants is null || request.Variants.Count == 0)
        {
            return OperationResult<ProductDto>.Fail("أضف عبوة واحدة على الأقل.");
        }

        foreach (var variant in request.Variants)
        {
            var variantError = ValidateVariant(variant);
            if (variantError is not null)
            {
                return OperationResult<ProductDto>.Fail(variantError);
            }
        }

        var skuError = await EnsureSkusUniqueAsync(request.Variants.Select(x => x.Sku), null, cancellationToken);
        if (skuError is not null)
        {
            return OperationResult<ProductDto>.Fail(skuError);
        }

        if (request.ImageRelativePath is not null && !OfficialAssets.IsAllowedProductImage(request.ImageRelativePath))
        {
            return OperationResult<ProductDto>.Fail("الصورة يجب أن تكون من ملفات الأصول الرسمية.");
        }

        var product = new Product
        {
            Id = UuidV7.New(),
            Name = request.Name.Trim(),
            Brand = TrimToNull(request.Brand),
            Category = TrimToNull(request.Category),
            Specification = TrimToNull(request.Specification),
            IsActive = request.IsActive,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        foreach (var item in request.Variants)
        {
            product.Variants.Add(CreateVariant(product.Id, item, actorUserId));
        }

        if (!string.IsNullOrWhiteSpace(request.ImageRelativePath))
        {
            product.Media.Add(new ProductMedia
            {
                Id = UuidV7.New(),
                ProductId = product.Id,
                RelativePath = OfficialAssets.NormalizeRelative(request.ImageRelativePath),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }

        _db.Products.Add(product);
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "products.create",
            EntityType = "product",
            EntityId = product.Id,
            AfterJson = JsonSerializer.Serialize(new { product.Name, variantCount = product.Variants.Count })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ProductDto>.Ok((await GetProductAsync(product.Id, cancellationToken))!);
    }

    public async Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.Include(x => x.Media).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductDto>.Fail("الصنف غير موجود.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<ProductDto>.Fail("اسم الصنف مطلوب.");
        }

        if (request.ImageRelativePath is not null && request.ImageRelativePath.Length > 0
            && !OfficialAssets.IsAllowedProductImage(request.ImageRelativePath))
        {
            return OperationResult<ProductDto>.Fail("الصورة يجب أن تكون من ملفات الأصول الرسمية.");
        }

        product.Name = request.Name.Trim();
        product.Brand = TrimToNull(request.Brand);
        product.Category = TrimToNull(request.Category);
        product.Specification = TrimToNull(request.Specification);
        product.IsActive = request.IsActive;
        product.UpdatedByUserId = actorUserId;

        var existing = product.Media.FirstOrDefault(x => x.VariantId is null);
        if (string.IsNullOrWhiteSpace(request.ImageRelativePath))
        {
            if (existing is not null)
            {
                _db.ProductMedia.Remove(existing);
            }
        }
        else
        {
            var path = OfficialAssets.NormalizeRelative(request.ImageRelativePath);
            if (existing is null)
            {
                product.Media.Add(new ProductMedia
                {
                    Id = UuidV7.New(),
                    ProductId = product.Id,
                    RelativePath = path,
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId
                });
            }
            else
            {
                existing.RelativePath = path;
                existing.UpdatedByUserId = actorUserId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ProductDto>.Ok((await GetProductAsync(product.Id, cancellationToken))!);
    }

    public async Task<OperationResult<VariantDto>> AddVariantAsync(Guid productId, SaveVariantRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
        if (product is null)
        {
            return OperationResult<VariantDto>.Fail("الصنف غير موجود.");
        }

        var error = ValidateVariant(request);
        if (error is not null)
        {
            return OperationResult<VariantDto>.Fail(error);
        }

        var skuError = await EnsureSkusUniqueAsync([request.Sku], null, cancellationToken);
        if (skuError is not null)
        {
            return OperationResult<VariantDto>.Fail(skuError);
        }

        var variant = CreateVariant(productId, request, actorUserId);
        _db.ProductVariants.Add(variant);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(MapVariant(variant));
    }

    public async Task<OperationResult<VariantDto>> UpdateVariantAsync(Guid variantId, SaveVariantRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<VariantDto>.Fail("العبوة غير موجودة.");
        }

        var error = ValidateVariant(request);
        if (error is not null)
        {
            return OperationResult<VariantDto>.Fail(error);
        }

        var skuError = await EnsureSkusUniqueAsync([request.Sku], variant.Id, cancellationToken);
        if (skuError is not null)
        {
            return OperationResult<VariantDto>.Fail(skuError);
        }

        variant.PackagingType = request.PackagingType.Trim();
        variant.PackagingSize = request.PackagingSize.Trim();
        variant.Sku = TrimToNull(request.Sku);
        variant.Barcode = TrimToNull(request.Barcode);
        variant.MinStock = request.MinStock;
        var roundedPrice = request.StandardWholesalePrice is { } price
            ? decimal.Round(price, 4, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        if (variant.StandardWholesalePrice != roundedPrice)
        {
            _db.CustomerVariantPriceHistories.Add(new CustomerVariantPriceHistory
            {
                Id = UuidV7.New(),
                CustomerId = Guid.Empty,
                VariantId = variant.Id,
                OldUnitPrice = variant.StandardWholesalePrice,
                NewUnitPrice = roundedPrice,
                ChangeKind = variant.StandardWholesalePrice is null ? PriceChangeKinds.Set : PriceChangeKinds.Update,
                Reason = "تعديل من شاشة الأصناف",
                ChangedAt = DateTime.UtcNow,
                ChangedByUserId = actorUserId
            });
        }

        variant.StandardWholesalePrice = roundedPrice;
        variant.StandardPurchasePrice = request.StandardPurchasePrice is { } cost
            ? decimal.Round(cost, 4, MidpointRounding.AwayFromZero)
            : null;
        variant.IsActive = request.IsActive;
        variant.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(MapVariant(variant));
    }

    public async Task<OperationResult<ProductDto>> ArchiveProductAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductDto>.Fail("الصنف غير موجود.", 404);
        }

        product.IsActive = false;
        product.DeletedAt = DateTime.UtcNow;
        product.DeletedByUserId = actorUserId;
        product.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "products.archive",
            EntityType = "product",
            EntityId = product.Id,
            AfterJson = JsonSerializer.Serialize(new { product.IsActive, product.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        var archived = await _db.Products.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Variants)
            .Include(x => x.Media)
            .FirstAsync(x => x.Id == id, cancellationToken);
        return OperationResult<ProductDto>.Ok(Map(archived));
    }

    public async Task<OperationResult<ProductDto>> RestoreProductAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null)
        {
            return OperationResult<ProductDto>.Fail("الصنف غير موجود.", 404);
        }

        product.IsActive = true;
        product.DeletedAt = null;
        product.DeletedByUserId = null;
        product.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "products.restore",
            EntityType = "product",
            EntityId = product.Id,
            AfterJson = JsonSerializer.Serialize(new { product.IsActive, product.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ProductDto>.Ok((await GetProductAsync(id, cancellationToken))!);
    }

    public async Task<OperationResult<VariantDto>> ArchiveVariantAsync(Guid variantId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants.FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<VariantDto>.Fail("العبوة غير موجودة.", 404);
        }

        variant.IsActive = false;
        variant.DeletedAt = DateTime.UtcNow;
        variant.DeletedByUserId = actorUserId;
        variant.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "variants.archive",
            EntityType = "product_variant",
            EntityId = variant.Id,
            AfterJson = JsonSerializer.Serialize(new { variant.IsActive, variant.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(MapVariant(variant));
    }

    public async Task<OperationResult<VariantDto>> RestoreVariantAsync(Guid variantId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var variant = await _db.ProductVariants.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == variantId, cancellationToken);
        if (variant is null)
        {
            return OperationResult<VariantDto>.Fail("العبوة غير موجودة.", 404);
        }

        variant.IsActive = true;
        variant.DeletedAt = null;
        variant.DeletedByUserId = null;
        variant.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "variants.restore",
            EntityType = "product_variant",
            EntityId = variant.Id,
            AfterJson = JsonSerializer.Serialize(new { variant.IsActive, variant.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<VariantDto>.Ok(MapVariant(variant));
    }

    public Task<IReadOnlyList<OfficialImageDto>> ListOfficialImagesAsync()
    {
        IReadOnlyList<OfficialImageDto> items = OfficialAssets.ListProductImages()
            .Select(item => new OfficialImageDto(item.RelativePath, OfficialAssets.ToPublicUrl(item.RelativePath)!))
            .ToList();
        return Task.FromResult(items);
    }

    public async Task<IReadOnlyList<VariantPickDto>> SearchVariantsAsync(string? search, Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.ProductVariants.AsNoTracking().Include(x => x.Product).Where(x => x.IsActive).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Product.Name.Contains(term) ||
                x.PackagingType.Contains(term) ||
                x.PackagingSize.Contains(term) ||
                (x.Sku != null && x.Sku.Contains(term)) ||
                (x.Barcode != null && x.Barcode.Contains(term)) ||
                (x.Product.Brand != null && x.Product.Brand.Contains(term)));
        }

        var rows = (await query
            .Select(x => new
            {
                x.Id,
                x.ProductId,
                ProductName = x.Product.Name,
                Brand = x.Product.Brand,
                Specification = x.Product.Specification,
                Category = x.Product.Category,
                x.PackagingType,
                x.PackagingSize,
                x.StandardWholesalePrice,
                x.IsActive,
                x.StandardPurchasePrice,
            })
            .ToListAsync(cancellationToken))
            .OrderBy(x => CatalogDisplayOrder.CategoryRank(x.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.ProductName, x.Brand, x.Specification))
            .ThenBy(x => CatalogDisplayOrder.PackagingRank(x.PackagingSize))
            .ThenBy(x => x.PackagingSize, StringComparer.Ordinal)
            .Take(80)
            .ToList();

        Dictionary<Guid, decimal>? customerPrices = null;
        if (customerId is Guid cid && rows.Count > 0)
        {
            var ids = rows.Select(x => x.Id).ToList();
            customerPrices = await _db.CustomerVariantPrices
                .AsNoTracking()
                .Where(x => x.CustomerId == cid && ids.Contains(x.VariantId))
                .ToDictionaryAsync(x => x.VariantId, x => x.UnitPrice, cancellationToken);
        }

        return rows.Select(x =>
        {
            decimal? customerPrice = null;
            if (customerPrices is not null && customerPrices.TryGetValue(x.Id, out var special))
            {
                customerPrice = special;
            }

            return new VariantPickDto(
                x.Id,
                x.ProductId,
                x.ProductName,
                x.PackagingType,
                x.PackagingSize,
                x.StandardWholesalePrice,
                x.IsActive,
                customerPrice,
                x.StandardPurchasePrice);
        }).ToList();
    }

    private async Task<string?> EnsureSkusUniqueAsync(IEnumerable<string?> skus, Guid? exceptVariantId, CancellationToken cancellationToken)
    {
        var values = skus
            .Select(TrimToNull)
            .Where(x => x is not null)
            .Cast<string>()
            .ToList();
        if (values.Count != values.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return "رمز SKU مكرر.";
        }

        foreach (var sku in values)
        {
            var exists = await _db.ProductVariants.AnyAsync(
                x => x.Sku == sku && (exceptVariantId == null || x.Id != exceptVariantId),
                cancellationToken);
            if (exists)
            {
                return "رمز SKU مستخدم مسبقاً.";
            }
        }

        return null;
    }

    private static string? ValidateVariant(SaveVariantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PackagingType) || string.IsNullOrWhiteSpace(request.PackagingSize))
        {
            return "نوع العبوة والحجم مطلوبان.";
        }

        if (request.MinStock is < 0 || request.StandardWholesalePrice is < 0 || request.StandardPurchasePrice is < 0)
        {
            return "القيمة لا يمكن أن تكون سالبة.";
        }

        return null;
    }

    private static ProductVariant CreateVariant(Guid productId, SaveVariantRequest request, Guid actorUserId)
    {
        return new ProductVariant
        {
            Id = UuidV7.New(),
            ProductId = productId,
            PackagingType = request.PackagingType.Trim(),
            PackagingSize = request.PackagingSize.Trim(),
            Sku = TrimToNull(request.Sku),
            Barcode = TrimToNull(request.Barcode),
            MinStock = request.MinStock,
            StandardWholesalePrice = request.StandardWholesalePrice,
            StandardPurchasePrice = request.StandardPurchasePrice,
            IsActive = request.IsActive,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
    }

    private static ProductDto Map(Product product)
    {
        var image = product.Media.FirstOrDefault(x => x.VariantId is null)?.RelativePath;
        return new ProductDto(
            product.Id,
            product.Name,
            product.Brand,
            product.Category,
            product.Specification,
            product.IsActive,
            image,
            OfficialAssets.ToPublicUrl(image),
            product.Variants
                .OrderBy(x => CatalogDisplayOrder.PackagingRank(x.PackagingSize))
                .ThenBy(x => x.PackagingSize, StringComparer.Ordinal)
                .Select(MapVariant)
                .ToList());
    }

    private static VariantDto MapVariant(ProductVariant variant)
    {
        return new VariantDto(
            variant.Id,
            variant.ProductId,
            variant.PackagingType,
            variant.PackagingSize,
            variant.Sku,
            variant.Barcode,
            variant.MinStock,
            variant.StandardWholesalePrice,
            variant.IsActive,
            variant.StandardPurchasePrice);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
