using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Parties;
using PetroTrans.Domain.Purchasing;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class SupplierService : ISupplierService
{
    private readonly AppDbContext _db;
    private readonly IPartyLedgerService _ledger;

    public SupplierService(AppDbContext db, IPartyLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<SupplierListItemDto>> ListAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = _db.Suppliers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Code.Contains(term));
        }

        var rows = await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        var result = new List<SupplierListItemDto>();
        foreach (var row in rows)
        {
            var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Supplier, row.Id, cancellationToken);
            result.Add(new SupplierListItemDto(row.Id, row.Code, row.Name, row.Phone, row.IsActive, outstanding));
        }

        return result;
    }

    public async Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Supplier, row.Id, cancellationToken);
        return new SupplierDto(row.Id, row.Code, row.Name, row.Phone, row.Address, row.IsActive, outstanding);
    }

    public async Task<OperationResult<SupplierDto>> SaveAsync(Guid? id, SaveSupplierRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<SupplierDto>.Fail("كود واسم المورد مطلوبان.");
        }

        var code = request.Code.Trim();
        var duplicateQuery = _db.Suppliers.Where(x => x.Code == code);
        if (id is { } existingId)
        {
            duplicateQuery = duplicateQuery.Where(x => x.Id != existingId);
        }

        if (await duplicateQuery.AnyAsync(cancellationToken))
        {
            return OperationResult<SupplierDto>.Fail("كود المورد مستخدم مسبقاً.");
        }

        Supplier supplier;
        if (id is { } existing)
        {
            supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == existing, cancellationToken)
                ?? null!;
            if (supplier is null)
            {
                return OperationResult<SupplierDto>.Fail("المورد غير موجود.");
            }

            supplier.Code = code;
            supplier.Name = request.Name.Trim();
            supplier.Phone = Trim(request.Phone);
            supplier.Address = Trim(request.Address);
            supplier.IsActive = request.IsActive;
            supplier.UpdatedByUserId = actorUserId;
        }
        else
        {
            supplier = new Supplier
            {
                Id = UuidV7.New(),
                Code = code,
                Name = request.Name.Trim(),
                Phone = Trim(request.Phone),
                Address = Trim(request.Address),
                IsActive = request.IsActive,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.Suppliers.Add(supplier);
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "purchasing.supplier", "supplier", supplier.Id, null, new { supplier.Code, supplier.Name }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<SupplierDto>.Ok((await GetAsync(supplier.Id, cancellationToken))!);
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

public sealed class PurchasingService : IPurchasingService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IPartyLedgerService _ledger;

    public PurchasingService(AppDbContext db, IInventoryService inventory, IPartyLedgerService ledger)
    {
        _db = db;
        _inventory = inventory;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<ReceiptListItemDto>> ListReceiptsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.GoodsReceipts.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .OrderByDescending(x => x.DocumentDate)
            .Select(x => new ReceiptListItemDto(x.Id, x.Number, x.DocumentDate, x.Supplier.Name, x.Warehouse.Name, x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReceiptDto?> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.GoodsReceipts.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : MapReceipt(row);
    }

    public async Task<OperationResult<ReceiptDto>> SaveReceiptAsync(Guid? id, SaveReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var error = ValidateReceipt(request);
        if (error is not null)
        {
            return OperationResult<ReceiptDto>.Fail(error);
        }

        GoodsReceipt receipt;
        if (id is { } existing)
        {
            receipt = await _db.GoodsReceipts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == existing, cancellationToken)
                ?? null!;
            if (receipt is null)
            {
                return OperationResult<ReceiptDto>.Fail("إذن الاستلام غير موجود.");
            }

            if (receipt.Status != PurchasingStatuses.Draft)
            {
                return OperationResult<ReceiptDto>.Fail("يمكن تعديل المسودة فقط.");
            }

            _db.GoodsReceiptLines.RemoveRange(receipt.Lines);
            receipt.Lines.Clear();
            receipt.SupplierId = request.SupplierId;
            receipt.WarehouseId = request.WarehouseId;
            receipt.DocumentDate = request.DocumentDate.Date;
            receipt.Notes = Trim(request.Notes);
            receipt.UpdatedByUserId = actorUserId;
        }
        else
        {
            receipt = new GoodsReceipt
            {
                Id = UuidV7.New(),
                SupplierId = request.SupplierId,
                WarehouseId = request.WarehouseId,
                DocumentDate = request.DocumentDate.Date,
                Status = PurchasingStatuses.Draft,
                Notes = Trim(request.Notes),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.GoodsReceipts.Add(receipt);
        }

        var n = 1;
        foreach (var line in request.Lines)
        {
            receipt.Lines.Add(new GoodsReceiptLine
            {
                Id = UuidV7.New(),
                ReceiptId = receipt.Id,
                LineNumber = n++,
                VariantId = line.VariantId,
                Quantity = decimal.Round(line.Quantity, 4, MidpointRounding.AwayFromZero),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ReceiptDto>.Ok((await GetReceiptAsync(receipt.Id, cancellationToken))!);
    }

    public async Task<OperationResult<ReceiptDto>> PostReceiptAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var receipt = await _db.GoodsReceipts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (receipt is null)
        {
            return OperationResult<ReceiptDto>.Fail("إذن الاستلام غير موجود.");
        }

        if (receipt.Status != PurchasingStatuses.Draft)
        {
            return OperationResult<ReceiptDto>.Fail("تم ترحيل إذن الاستلام مسبقاً.");
        }

        foreach (var line in receipt.Lines)
        {
            var error = await _inventory.ApplyAsync(
                receipt.WarehouseId,
                line.VariantId,
                MovementTypes.PurchaseReceipt,
                MovementDirections.In,
                line.Quantity,
                SourceDocumentTypes.GoodsReceipt,
                receipt.Id,
                receipt.DocumentDate,
                actorUserId,
                receipt.Notes,
                cancellationToken);
            if (error is not null)
            {
                return OperationResult<ReceiptDto>.Fail(error);
            }
        }

        receipt.Status = PurchasingStatuses.Posted;
        receipt.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "inventory.receive", "goods_receipt", receipt.Id, null, new { receipt.WarehouseId, receipt.SupplierId }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<ReceiptDto>.Ok((await GetReceiptAsync(receipt.Id, cancellationToken))!);
    }

    public async Task<IReadOnlyList<PurchaseInvoiceListItemDto>> ListBillsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier)
            .OrderByDescending(x => x.InvoiceDate)
            .Select(x => new PurchaseInvoiceListItemDto(x.Id, x.Number, x.InvoiceDate, x.Supplier.Name, x.GoodsTotal, x.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task<PurchaseInvoiceDto?> GetBillAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return row is null ? null : await MapBillAsync(row, cancellationToken);
    }

    public async Task<OperationResult<PurchaseInvoiceDto>> SaveBillAsync(Guid? id, SavePurchaseInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("أضف بنداً واحداً على الأقل.");
        }

        PurchaseInvoice invoice;
        if (id is { } existing)
        {
            invoice = await _db.PurchaseInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == existing, cancellationToken)
                ?? null!;
            if (invoice is null)
            {
                return OperationResult<PurchaseInvoiceDto>.Fail("فاتورة المشتريات غير موجودة.");
            }

            if (invoice.Status != PurchasingStatuses.Draft)
            {
                return OperationResult<PurchaseInvoiceDto>.Fail("يمكن تعديل المسودة فقط.");
            }

            _db.PurchaseInvoiceLines.RemoveRange(invoice.Lines);
            invoice.Lines.Clear();
            invoice.SupplierId = request.SupplierId;
            invoice.InvoiceDate = request.InvoiceDate.Date;
            invoice.Notes = Trim(request.Notes);
            invoice.UpdatedByUserId = actorUserId;
        }
        else
        {
            invoice = new PurchaseInvoice
            {
                Id = UuidV7.New(),
                SupplierId = request.SupplierId,
                InvoiceDate = request.InvoiceDate.Date,
                Status = PurchasingStatuses.Draft,
                Notes = Trim(request.Notes),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.PurchaseInvoices.Add(invoice);
        }

        var n = 1;
        decimal total = 0;
        foreach (var line in request.Lines)
        {
            if (line.Quantity <= 0 || line.UnitPrice < 0)
            {
                return OperationResult<PurchaseInvoiceDto>.Fail("الكمية يجب أن تكون أكبر من صفر.");
            }

            var lineTotal = decimal.Round(line.Quantity * line.UnitPrice, 4, MidpointRounding.AwayFromZero);
            total += lineTotal;
            invoice.Lines.Add(new PurchaseInvoiceLine
            {
                Id = UuidV7.New(),
                InvoiceId = invoice.Id,
                LineNumber = n++,
                VariantId = line.VariantId,
                Quantity = decimal.Round(line.Quantity, 4, MidpointRounding.AwayFromZero),
                UnitPrice = decimal.Round(line.UnitPrice, 4, MidpointRounding.AwayFromZero),
                LineTotal = lineTotal,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }

        invoice.GoodsTotal = total;
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<PurchaseInvoiceDto>.Ok((await GetBillAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PurchaseInvoiceDto>> PostBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await _db.PurchaseInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("فاتورة المشتريات غير موجودة.");
        }

        if (invoice.Status != PurchasingStatuses.Draft)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("تم ترحيل الفاتورة مسبقاً.");
        }

        var stockError = await ApplyBillStockAsync(invoice, actorUserId, cancellationToken);
        if (stockError is not null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail(stockError);
        }

        invoice.Status = PurchasingStatuses.Posted;
        invoice.UpdatedByUserId = actorUserId;
        await _ledger.AddAsync(
            PartyKinds.Supplier,
            invoice.SupplierId,
            LedgerEntryTypes.PurchaseInvoice,
            invoice.GoodsTotal,
            SourceDocumentTypes.PurchaseInvoice,
            invoice.Id,
            invoice.InvoiceDate,
            actorUserId,
            cancellationToken);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "purchasing.post", "purchase_invoice", invoice.Id, null, new { invoice.GoodsTotal }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PurchaseInvoiceDto>.Ok((await GetBillAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PurchaseInvoiceDto>> SetPostedVerifiedHeaderAsync(
        Guid id,
        string number,
        decimal goodsTotal,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var trimmedNumber = number.Trim();
        if (string.IsNullOrWhiteSpace(trimmedNumber) || trimmedNumber.Length > 32)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("رقم فاتورة المشتريات غير صالح.");
        }

        if (goodsTotal < 0)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("إجمالي فاتورة المشتريات غير صالح.");
        }

        var roundedTotal = decimal.Round(goodsTotal, 4, MidpointRounding.AwayFromZero);
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await _db.PurchaseInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("فاتورة المشتريات غير موجودة.");
        }

        if (invoice.Status != PurchasingStatuses.Posted)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("يمكن تصحيح إجمالي الفاتورة المرحلة فقط.");
        }

        var numberTaken = await _db.PurchaseInvoices.AnyAsync(
            x => x.Id != invoice.Id && x.Number == trimmedNumber,
            cancellationToken);
        if (numberTaken)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("رقم فاتورة المشتريات مستخدم مسبقاً.");
        }

        if (invoice.Number == trimmedNumber && invoice.GoodsTotal == roundedTotal)
        {
            await tx.CommitAsync(cancellationToken);
            return OperationResult<PurchaseInvoiceDto>.Ok((await GetBillAsync(invoice.Id, cancellationToken))!);
        }

        var ledger = await _db.PartyLedgerEntries
            .Where(x => x.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice && x.SourceDocumentId == invoice.Id)
            .ToListAsync(cancellationToken);
        if (ledger.Count != 1)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("قيد المورد المرتبط بفاتورة المشتريات غير موجود أو مكرر.");
        }

        var before = new { invoice.Number, invoice.GoodsTotal, ledgerAmount = ledger[0].SignedAmount };
        invoice.Number = trimmedNumber;
        invoice.GoodsTotal = roundedTotal;
        invoice.UpdatedByUserId = actorUserId;
        ledger[0].SignedAmount = roundedTotal;
        ledger[0].UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(
            actorUserId,
            "purchasing.verified_header",
            "purchase_invoice",
            invoice.Id,
            before,
            new { invoice.Number, invoice.GoodsTotal, lineCount = invoice.Lines.Count }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PurchaseInvoiceDto>.Ok((await GetBillAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PurchaseInvoiceDto>> UnpostBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await _db.PurchaseInvoices.Include(x => x.Lines).Include(x => x.Supplier).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("فاتورة المشتريات غير موجودة.");
        }

        if (invoice.Status != PurchasingStatuses.Posted)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("يمكن إرجاع فاتورة المشتريات المرحلة فقط.");
        }

        var stockError = await _inventory.ReverseSourceAsync(SourceDocumentTypes.PurchaseInvoice, invoice.Id, cancellationToken);
        if (stockError is not null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail(stockError);
        }

        var ledger = await _db.PartyLedgerEntries
            .Where(x => x.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice && x.SourceDocumentId == invoice.Id)
            .ToListAsync(cancellationToken);
        _db.PartyLedgerEntries.RemoveRange(ledger);
        var before = new { invoice.Status, invoice.Number, invoice.GoodsTotal };
        invoice.Status = PurchasingStatuses.Draft;
        invoice.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "purchasing.unpost", "purchase_invoice", invoice.Id, before, new { status = "draft", invoice.Number }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PurchaseInvoiceDto>.Ok((await GetBillAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PurchaseInvoiceDto>> DeleteBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await _db.PurchaseInvoices
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .Include(x => x.Supplier)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<PurchaseInvoiceDto>.Fail("فاتورة المشتريات غير موجودة.");
        }

        var dto = await MapBillAsync(invoice, cancellationToken);
        var before = new { invoice.Status, invoice.Number, invoice.GoodsTotal };
        if (invoice.Status == PurchasingStatuses.Posted)
        {
            var stockError = await _inventory.ReverseSourceAsync(SourceDocumentTypes.PurchaseInvoice, invoice.Id, cancellationToken);
            if (stockError is not null)
            {
                return OperationResult<PurchaseInvoiceDto>.Fail(stockError);
            }

            var ledger = await _db.PartyLedgerEntries
                .Where(x => x.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice && x.SourceDocumentId == invoice.Id)
                .ToListAsync(cancellationToken);
            _db.PartyLedgerEntries.RemoveRange(ledger);
        }

        _db.PurchaseInvoices.Remove(invoice);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "purchasing.delete", "purchase_invoice", invoice.Id, before, new { deleted = true, invoice.Number }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PurchaseInvoiceDto>.Ok(dto);
    }

    private async Task<string?> ApplyBillStockAsync(PurchaseInvoice invoice, Guid actorUserId, CancellationToken cancellationToken)
    {
        var alreadyMoved = await _db.InventoryMovements.AnyAsync(
            x => x.SourceDocumentType == SourceDocumentTypes.PurchaseInvoice && x.SourceDocumentId == invoice.Id,
            cancellationToken);
        if (alreadyMoved)
        {
            return null;
        }

        var warehouseId = await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (warehouseId == Guid.Empty)
        {
            return "أضف مخزناً من الإعدادات";
        }

        foreach (var line in invoice.Lines)
        {
            var error = await _inventory.ApplyAsync(
                warehouseId,
                line.VariantId,
                MovementTypes.PurchaseReceipt,
                MovementDirections.In,
                line.Quantity,
                SourceDocumentTypes.PurchaseInvoice,
                invoice.Id,
                invoice.InvoiceDate,
                actorUserId,
                invoice.Notes,
                cancellationToken);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static string? ValidateReceipt(SaveReceiptRequest request)
    {
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

    private static ReceiptDto MapReceipt(GoodsReceipt row)
    {
        return new ReceiptDto(
            row.Id,
            row.Number,
            row.SupplierId,
            row.Supplier.Name,
            row.WarehouseId,
            row.DocumentDate,
            row.Status,
            row.Notes,
            row.Lines.OrderBy(x => x.LineNumber).Select(x => new ReceiptLineDto(
                x.VariantId,
                x.Variant.Product.Name,
                x.Variant.PackagingType,
                x.Variant.PackagingSize,
                x.Quantity)).ToList());
    }

    private async Task<PurchaseInvoiceDto> MapBillAsync(PurchaseInvoice row, CancellationToken cancellationToken)
    {
        var ids = new[] { row.CreatedByUserId, row.UpdatedByUserId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var mapped = MapBill(row);
        return mapped with
        {
            CreatedByName = row.CreatedByUserId is { } created ? names.GetValueOrDefault(created) : null,
            UpdatedByName = row.UpdatedByUserId is { } updated ? names.GetValueOrDefault(updated) : null
        };
    }

    private static PurchaseInvoiceDto MapBill(PurchaseInvoice row)
    {
        var lines = row.Lines.OrderBy(x => x.LineNumber).Select(x => new PurchaseInvoiceLineDto(
            x.VariantId,
            x.Variant.Product.Name,
            x.Variant.PackagingType,
            x.Variant.PackagingSize,
            x.Quantity,
            x.UnitPrice,
            x.LineTotal)).ToList();
        var lineSum = lines.Sum(x => x.LineTotal);
        return new PurchaseInvoiceDto(
            row.Id,
            row.Number,
            row.SupplierId,
            row.Supplier.Name,
            row.InvoiceDate,
            row.Status,
            row.GoodsTotal,
            row.Notes,
            lines,
            lineSum,
            0m,
            row.Status == PurchasingStatuses.Cancelled ? 0m : row.GoodsTotal,
            "unpaid",
            row.CreatedAt,
            row.UpdatedAt);
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
