using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Inventory;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class InventoryService : IInventoryService
{
    private readonly AppDbContext _db;

    public InventoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> GetOnHandAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default)
    {
        var row = await _db.InventoryBalances.AsNoTracking()
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.VariantId == variantId, cancellationToken);
        return row?.OnHand ?? 0m;
    }

    public async Task<string?> ApplyAsync(
        Guid warehouseId,
        Guid variantId,
        string movementType,
        string direction,
        decimal quantity,
        string sourceType,
        Guid sourceId,
        DateTime occurredAt,
        Guid actorUserId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return "الكمية يجب أن تكون أكبر من صفر.";
        }

        quantity = decimal.Round(quantity, 4, MidpointRounding.AwayFromZero);
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId && x.IsActive, cancellationToken);
        if (warehouse is null)
        {
            return "أضف مخزناً من الإعدادات";
        }

        var variantExists = await _db.ProductVariants.AnyAsync(x => x.Id == variantId, cancellationToken);
        if (!variantExists)
        {
            return "العبوة غير موجودة.";
        }

        var balance = await _db.InventoryBalances
            .FirstOrDefaultAsync(x => x.WarehouseId == warehouseId && x.VariantId == variantId, cancellationToken);
        var onHand = balance?.OnHand ?? 0m;
        var next = direction == MovementDirections.In ? onHand + quantity : onHand - quantity;
        if (next < 0)
        {
            return $"الكمية المتاحة غير كافية. المتاح: {onHand}";
        }

        if (balance is null)
        {
            balance = new InventoryBalance
            {
                Id = UuidV7.New(),
                WarehouseId = warehouseId,
                VariantId = variantId,
                OnHand = next
            };
            _db.InventoryBalances.Add(balance);
        }
        else
        {
            balance.OnHand = next;
        }

        _db.InventoryMovements.Add(new InventoryMovement
        {
            Id = UuidV7.New(),
            VariantId = variantId,
            WarehouseId = warehouseId,
            MovementType = movementType,
            Quantity = quantity,
            Direction = direction,
            SourceDocumentType = sourceType,
            SourceDocumentId = sourceId,
            OccurredAt = occurredAt,
            Notes = notes,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        });
        return null;
    }

    public async Task<string?> ReverseSourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default)
    {
        var movements = await _db.InventoryMovements
            .Where(x => x.SourceDocumentType == sourceType && x.SourceDocumentId == sourceId)
            .ToListAsync(cancellationToken);
        foreach (var movement in movements)
        {
            var balance = await _db.InventoryBalances
                .FirstOrDefaultAsync(x => x.WarehouseId == movement.WarehouseId && x.VariantId == movement.VariantId, cancellationToken);
            var onHand = balance?.OnHand ?? 0m;
            var next = movement.Direction == MovementDirections.Out
                ? onHand + movement.Quantity
                : onHand - movement.Quantity;
            if (next < 0)
            {
                return $"الكمية المتاحة غير كافية. المتاح: {onHand}";
            }

            if (balance is null)
            {
                balance = new InventoryBalance
                {
                    Id = UuidV7.New(),
                    WarehouseId = movement.WarehouseId,
                    VariantId = movement.VariantId,
                    OnHand = next
                };
                _db.InventoryBalances.Add(balance);
            }
            else
            {
                balance.OnHand = next;
            }
        }

        _db.InventoryMovements.RemoveRange(movements);
        return null;
    }

    public async Task<IReadOnlyList<StockRowDto>> ListOnHandAsync(Guid? warehouseId, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryBalances.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Variant).ThenInclude(x => x.Product)
            .AsQueryable();
        if (warehouseId is { } id)
        {
            query = query.Where(x => x.WarehouseId == id);
        }

        var rows = await query
            .Select(x => new StockRowDto(
                x.WarehouseId,
                x.Warehouse.Name,
                x.VariantId,
                x.Variant.Product.Name,
                x.Variant.PackagingType,
                x.Variant.PackagingSize,
                x.OnHand,
                x.Variant.MinStock,
                x.OnHand < (x.Variant.MinStock ?? 1m),
                x.Variant.Product.Category,
                x.Variant.StandardPurchasePrice,
                null))
            .ToListAsync(cancellationToken);
        return rows
            .Select(x => x with { StockValue = x.CompanyCost is { } price ? price * x.OnHand : null })
            .OrderBy(x => x.WarehouseName, StringComparer.Ordinal)
            .ThenBy(x => CatalogDisplayOrder.CategoryRank(x.Category))
            .ThenBy(x => CatalogDisplayOrder.ProductRank(x.ProductName))
            .ThenBy(x => CatalogDisplayOrder.PackagingRank(x.PackagingSize))
            .ThenBy(x => x.PackagingSize, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<MovementRowDto>> ListMovementsAsync(Guid? warehouseId, Guid? variantId, CancellationToken cancellationToken = default)
    {
        var query = _db.InventoryMovements.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Variant).ThenInclude(x => x.Product)
            .AsQueryable();
        if (warehouseId is { } wh)
        {
            query = query.Where(x => x.WarehouseId == wh);
        }

        if (variantId is { } vid)
        {
            query = query.Where(x => x.VariantId == vid);
        }

        return await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => new MovementRowDto(
                x.Id,
                x.OccurredAt,
                x.Warehouse.Name,
                x.Variant.Product.Name,
                x.Variant.PackagingType,
                x.Variant.PackagingSize,
                x.MovementType,
                x.Direction,
                x.Quantity,
                x.SourceDocumentType,
                x.SourceDocumentId,
                x.Notes))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AdjustmentDto>> ListAdjustmentsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await AdjustmentQuery()
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return rows.Select(ToDto).ToList();
    }

    public async Task<AdjustmentDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await AdjustmentQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<OperationResult<AdjustmentDto>> AdjustAsync(SaveAdjustmentRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var invalid = ValidateAdjustment(request);
        if (invalid is not null)
        {
            return OperationResult<AdjustmentDto>.Fail(invalid);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = new InventoryAdjustment
        {
            Id = UuidV7.New(),
            CreatedByUserId = actorUserId
        };
        var applyError = await ApplyAdjustmentAsync(adjustment, request, actorUserId, cancellationToken);
        if (applyError is not null)
        {
            return OperationResult<AdjustmentDto>.Fail(applyError);
        }

        _db.InventoryAdjustments.Add(adjustment);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "inventory.adjust", "inventory_adjustment", adjustment.Id, null, new { adjustment.WarehouseId, adjustment.Direction, adjustment.Reason }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<AdjustmentDto>.Ok((await GetAdjustmentAsync(adjustment.Id, cancellationToken))!);
    }

    public async Task<OperationResult<AdjustmentDto>> UpdateAdjustmentAsync(Guid id, SaveAdjustmentRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var invalid = ValidateAdjustment(request);
        if (invalid is not null)
        {
            return OperationResult<AdjustmentDto>.Fail(invalid);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = await _db.InventoryAdjustments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<AdjustmentDto>.Fail("التسوية غير موجودة.", 404);
        }

        var lineSnapshot = await _db.InventoryAdjustmentLines.AsNoTracking()
            .Where(x => x.AdjustmentId == id)
            .Select(x => new { x.VariantId, x.Quantity })
            .ToListAsync(cancellationToken);
        var before = new { adjustment.WarehouseId, adjustment.Direction, adjustment.Reason, lines = lineSnapshot };
        var reverseError = await ReverseSourceAsync(SourceDocumentTypes.InventoryAdjustment, adjustment.Id, cancellationToken);
        if (reverseError is not null)
        {
            return OperationResult<AdjustmentDto>.Fail(reverseError);
        }

        await _db.InventoryAdjustmentLines.Where(x => x.AdjustmentId == id).ExecuteDeleteAsync(cancellationToken);
        var applyError = await ApplyAdjustmentAsync(adjustment, request, actorUserId, cancellationToken);
        if (applyError is not null)
        {
            return OperationResult<AdjustmentDto>.Fail(applyError);
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "inventory.adjust_update", "inventory_adjustment", adjustment.Id, before, new { adjustment.WarehouseId, adjustment.Direction, adjustment.Reason }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<AdjustmentDto>.Ok((await GetAdjustmentAsync(adjustment.Id, cancellationToken))!);
    }

    public async Task<OperationResult<bool>> DeleteAdjustmentAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var adjustment = await _db.InventoryAdjustments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (adjustment is null)
        {
            return OperationResult<bool>.Fail("التسوية غير موجودة.", 404);
        }

        var reverseError = await ReverseSourceAsync(SourceDocumentTypes.InventoryAdjustment, adjustment.Id, cancellationToken);
        if (reverseError is not null)
        {
            return OperationResult<bool>.Fail(reverseError);
        }

        _db.AuditLogs.Add(Audits.Create(
            actorUserId,
            "inventory.adjust_void",
            "inventory_adjustment",
            adjustment.Id,
            new { adjustment.WarehouseId, adjustment.Direction, adjustment.Reason, lines = adjustment.Lines.Select(x => new { x.VariantId, x.Quantity }).ToList() },
            new { deleted = true }));
        _db.InventoryAdjustments.Remove(adjustment);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<bool>.Ok(true);
    }

    public async Task<OperationResult<TransferDto>> TransferAsync(SaveTransferRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var warehouseCount = await _db.Warehouses.CountAsync(x => x.IsActive, cancellationToken);
        if (warehouseCount < 2)
        {
            return OperationResult<TransferDto>.Fail("التحويل متاح فقط عند وجود أكثر من مخزن.");
        }

        if (request.FromWarehouseId == request.ToWarehouseId)
        {
            return OperationResult<TransferDto>.Fail("المخزن المصدر والوجهة يجب أن يختلفا.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return OperationResult<TransferDto>.Fail("أضف بنداً واحداً على الأقل.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var transfer = new StockTransfer
        {
            Id = UuidV7.New(),
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Notes = Trim(request.Notes),
            OccurredAt = request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        foreach (var line in request.Lines)
        {
            var outError = await ApplyAsync(request.FromWarehouseId, line.VariantId, MovementTypes.TransferOut, MovementDirections.Out, line.Quantity, SourceDocumentTypes.StockTransfer, transfer.Id, transfer.OccurredAt, actorUserId, request.Notes, cancellationToken);
            if (outError is not null)
            {
                return OperationResult<TransferDto>.Fail(outError);
            }

            var inError = await ApplyAsync(request.ToWarehouseId, line.VariantId, MovementTypes.TransferIn, MovementDirections.In, line.Quantity, SourceDocumentTypes.StockTransfer, transfer.Id, transfer.OccurredAt, actorUserId, request.Notes, cancellationToken);
            if (inError is not null)
            {
                return OperationResult<TransferDto>.Fail(inError);
            }

            transfer.Lines.Add(new StockTransferLine
            {
                Id = UuidV7.New(),
                TransferId = transfer.Id,
                VariantId = line.VariantId,
                Quantity = decimal.Round(line.Quantity, 4, MidpointRounding.AwayFromZero),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }

        _db.StockTransfers.Add(transfer);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "inventory.transfer", "stock_transfer", transfer.Id, null, new { request.FromWarehouseId, request.ToWarehouseId }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        var fromName = await _db.Warehouses.Where(x => x.Id == request.FromWarehouseId).Select(x => x.Name).SingleAsync(cancellationToken);
        var toName = await _db.Warehouses.Where(x => x.Id == request.ToWarehouseId).Select(x => x.Name).SingleAsync(cancellationToken);
        return OperationResult<TransferDto>.Ok(new TransferDto(transfer.Id, fromName, toName, transfer.OccurredAt));
    }

    private IQueryable<InventoryAdjustment> AdjustmentQuery()
        => _db.InventoryAdjustments.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product);

    private static AdjustmentDto ToDto(InventoryAdjustment row)
        => new(
            row.Id,
            row.WarehouseId,
            row.Warehouse.Name,
            row.Direction,
            row.Reason,
            row.OccurredAt,
            row.Lines.Select(x => new AdjustmentLineDto(
                x.VariantId,
                x.Variant.Product.Name,
                x.Variant.PackagingType,
                x.Variant.PackagingSize,
                x.Quantity)).ToList(),
            row.Notes);

    private static string? ValidateAdjustment(SaveAdjustmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return "سبب التسوية مطلوب.";
        }

        if (request.Direction is not (MovementDirections.In or MovementDirections.Out))
        {
            return "اتجاه التسوية غير صالح.";
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return "أضف بنداً واحداً على الأقل.";
        }

        if (request.Lines.Any(x => x.Quantity <= 0))
        {
            return "الكمية يجب أن تكون أكبر من صفر.";
        }

        return null;
    }

    private async Task<string?> ApplyAdjustmentAsync(InventoryAdjustment adjustment, SaveAdjustmentRequest request, Guid actorUserId, CancellationToken cancellationToken)
    {
        adjustment.WarehouseId = request.WarehouseId;
        adjustment.Direction = request.Direction;
        adjustment.Reason = request.Reason.Trim();
        adjustment.Notes = Trim(request.Notes);
        adjustment.OccurredAt = request.OccurredAt == default ? DateTime.UtcNow : request.OccurredAt;
        adjustment.UpdatedByUserId = actorUserId;
        foreach (var lineRequest in request.Lines)
        {
            var type = request.Direction == MovementDirections.In ? MovementTypes.AdjustmentIn : MovementTypes.AdjustmentOut;
            var error = await ApplyAsync(
                request.WarehouseId,
                lineRequest.VariantId,
                type,
                request.Direction,
                lineRequest.Quantity,
                SourceDocumentTypes.InventoryAdjustment,
                adjustment.Id,
                adjustment.OccurredAt,
                actorUserId,
                request.Reason,
                cancellationToken);
            if (error is not null)
            {
                return error;
            }

            var line = new InventoryAdjustmentLine
            {
                Id = UuidV7.New(),
                AdjustmentId = adjustment.Id,
                VariantId = lineRequest.VariantId,
                Quantity = decimal.Round(lineRequest.Quantity, 4, MidpointRounding.AwayFromZero),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            adjustment.Lines.Add(line);
            _db.InventoryAdjustmentLines.Add(line);
        }

        return null;
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
