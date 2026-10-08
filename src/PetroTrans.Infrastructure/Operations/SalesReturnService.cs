using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class SalesReturnService : ISalesReturnService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IPartyLedgerService _ledger;

    public SalesReturnService(AppDbContext db, IInventoryService inventory, IPartyLedgerService ledger)
    {
        _db = db;
        _inventory = inventory;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<ReturnListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.SalesReturns.AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.OriginalSalesInvoice)
            .OrderByDescending(x => x.ReturnDate)
            .Select(x => new ReturnListItemDto(
                x.Id,
                x.ReturnDate,
                x.Customer.Name,
                x.OriginalSalesInvoice != null ? x.OriginalSalesInvoice.Number : null,
                x.Status,
                x.BalancePostingStatus))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReturnDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await LoadAsync(id, true, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<ReturnableInvoiceDto?> GetReturnableAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.SalesInvoices.AsNoTracking()
            .Include(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.Status != SalesStatuses.Posted || invoice.WarehouseId is null)
        {
            return null;
        }

        var alreadyByVariant = await PostedReturnedQtyAsync(invoice.Id, null, cancellationToken);
        var unitValues = InvoiceSettlement.UnitValues(invoice);
        var lines = invoice.Lines
            .GroupBy(x => x.VariantId)
            .Select(group =>
            {
                var sample = group.OrderBy(x => x.LineNumber).First();
                var sold = group.Sum(x => x.Quantity);
                var already = alreadyByVariant.GetValueOrDefault(group.Key);
                var returnable = sold - already;
                if (returnable < 0)
                {
                    returnable = 0;
                }

                return new ReturnableLineDto(
                    group.Key,
                    sample.Variant.Product.Name,
                    sample.Variant.PackagingType,
                    sample.Variant.PackagingSize,
                    decimal.Round(sold, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(already, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(returnable, 4, MidpointRounding.AwayFromZero),
                    decimal.Round(unitValues.GetValueOrDefault(group.Key), 4, MidpointRounding.AwayFromZero));
            })
            .OrderBy(x => x.ProductName)
            .ToList();

        return new ReturnableInvoiceDto(
            invoice.Id,
            invoice.Number,
            invoice.CustomerId,
            invoice.WarehouseId.Value,
            invoice.Warehouse?.Name ?? string.Empty,
            lines);
    }

    public async Task<OperationResult<ReturnDto>> SaveAsync(Guid? id, SaveReturnRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (request.OriginalSalesInvoiceId is not { } invoiceId)
        {
            return OperationResult<ReturnDto>.Fail("اختر فاتورة البيع المراد إرجاعها.");
        }

        var lines = (request.Lines ?? [])
            .Where(x => x.Quantity > 0)
            .GroupBy(x => x.VariantId)
            .Select(g => new SaveReturnLineRequest(g.Key, g.Sum(x => x.Quantity)))
            .ToList();
        if (lines.Count == 0)
        {
            return OperationResult<ReturnDto>.Fail("حدد كمية مرتجع لصنف واحد على الأقل.");
        }

        var invoice = await _db.SalesInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.Status != SalesStatuses.Posted)
        {
            return OperationResult<ReturnDto>.Fail("فاتورة الأصل يجب أن تكون مرحّلة.");
        }

        if (invoice.CustomerId != request.CustomerId)
        {
            return OperationResult<ReturnDto>.Fail("العميل لا يطابق فاتورة الأصل.");
        }

        if (invoice.WarehouseId is null)
        {
            return OperationResult<ReturnDto>.Fail("أضف مخزناً من الإعدادات");
        }

        var qtyError = await ValidateReturnableQtyAsync(invoice, lines, id, cancellationToken);
        if (qtyError is not null)
        {
            return OperationResult<ReturnDto>.Fail(qtyError);
        }

        SalesReturn doc;
        if (id is { } existing)
        {
            doc = await _db.SalesReturns.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == existing, cancellationToken)
                ?? null!;
            if (doc is null)
            {
                return OperationResult<ReturnDto>.Fail("المرتجع غير موجود.");
            }

            if (doc.Status != ReturnStatuses.Draft)
            {
                return OperationResult<ReturnDto>.Fail("يمكن تعديل المسودة فقط.");
            }

            _db.SalesReturnLines.RemoveRange(doc.Lines);
            doc.Lines.Clear();
            Fill(doc, request, invoice.WarehouseId.Value, actorUserId);
        }
        else
        {
            doc = new SalesReturn { Id = UuidV7.New(), CreatedByUserId = actorUserId };
            Fill(doc, request, invoice.WarehouseId.Value, actorUserId);
            _db.SalesReturns.Add(doc);
        }

        var n = 1;
        foreach (var line in lines)
        {
            doc.Lines.Add(new SalesReturnLine
            {
                Id = UuidV7.New(),
                ReturnId = doc.Id,
                LineNumber = n++,
                VariantId = line.VariantId,
                Quantity = decimal.Round(line.Quantity, 4, MidpointRounding.AwayFromZero),
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<ReturnDto>.Ok((await GetAsync(doc.Id, cancellationToken))!);
    }

    public async Task<OperationResult<ReturnDto>> PostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var doc = await _db.SalesReturns.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (doc is null)
        {
            return OperationResult<ReturnDto>.Fail("المرتجع غير موجود.");
        }

        if (doc.Status != ReturnStatuses.Draft)
        {
            return OperationResult<ReturnDto>.Fail("تم ترحيل المرتجع مسبقاً.");
        }

        if (doc.OriginalSalesInvoiceId is not { } invoiceId)
        {
            return OperationResult<ReturnDto>.Fail("اختر فاتورة البيع المراد إرجاعها.");
        }

        var invoice = await _db.SalesInvoices.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.Status != SalesStatuses.Posted)
        {
            return OperationResult<ReturnDto>.Fail("فاتورة الأصل يجب أن تكون مرحّلة.");
        }

        if (invoice.WarehouseId is null)
        {
            return OperationResult<ReturnDto>.Fail("أضف مخزناً من الإعدادات");
        }

        var qtyError = await ValidateReturnableQtyAsync(
            invoice,
            doc.Lines.Select(x => new SaveReturnLineRequest(x.VariantId, x.Quantity)).ToList(),
            doc.Id,
            cancellationToken);
        if (qtyError is not null)
        {
            return OperationResult<ReturnDto>.Fail(qtyError);
        }

        doc.WarehouseId = invoice.WarehouseId.Value;
        foreach (var line in doc.Lines)
        {
            var error = await _inventory.ApplyAsync(
                doc.WarehouseId,
                line.VariantId,
                MovementTypes.SalesReturn,
                MovementDirections.In,
                line.Quantity,
                SourceDocumentTypes.SalesReturn,
                doc.Id,
                doc.ReturnDate,
                actorUserId,
                doc.Notes,
                cancellationToken);
            if (error is not null)
            {
                return OperationResult<ReturnDto>.Fail(error);
            }
        }

        var credit = InvoiceSettlement.CreditForReturnLines(
            invoice,
            doc.Lines.Select(x => (x.VariantId, x.Quantity)));
        if (credit > 0)
        {
            await _ledger.AddAsync(
                PartyKinds.Customer,
                invoice.CustomerId,
                LedgerEntryTypes.SalesReturn,
                -credit,
                SourceDocumentTypes.SalesReturn,
                doc.Id,
                doc.ReturnDate,
                actorUserId,
                cancellationToken);
        }

        var returned = await InvoiceSettlement.PostedReturnValueAsync(_db, invoice.Id, doc.Id, cancellationToken) + credit;
        InvoiceSettlement.ApplyRemaining(invoice, invoice.PaidTotal, returned);

        doc.Status = ReturnStatuses.Posted;
        doc.BalancePostingStatus = credit > 0 ? BalancePostingStatuses.Posted : BalancePostingStatuses.NotPosted;
        doc.UpdatedByUserId = actorUserId;
        invoice.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(Audits.Create(actorUserId, "returns.create", "sales_return", doc.Id, null, new { doc.CustomerId, doc.OriginalSalesInvoiceId, credit, finance = doc.BalancePostingStatus }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<ReturnDto>.Ok((await GetAsync(doc.Id, cancellationToken))!);
    }

    private async Task<string?> ValidateReturnableQtyAsync(
        SalesInvoice invoice,
        IReadOnlyList<SaveReturnLineRequest> lines,
        Guid? exceptReturnId,
        CancellationToken cancellationToken)
    {
        var alreadyByVariant = await PostedReturnedQtyAsync(invoice.Id, exceptReturnId, cancellationToken);
        foreach (var line in lines)
        {
            var sold = invoice.Lines.Where(x => x.VariantId == line.VariantId).Sum(x => x.Quantity);
            if (sold <= 0)
            {
                return "الصنف غير موجود في فاتورة الأصل.";
            }

            var already = alreadyByVariant.GetValueOrDefault(line.VariantId);
            if (line.Quantity + already > sold)
            {
                return "كمية المرتجع أكبر من الكمية القابلة للإرجاع.";
            }
        }

        return null;
    }

    private async Task<Dictionary<Guid, decimal>> PostedReturnedQtyAsync(Guid invoiceId, Guid? exceptReturnId, CancellationToken cancellationToken)
    {
        var query = _db.SalesReturns
            .Where(x => x.OriginalSalesInvoiceId == invoiceId && x.Status == ReturnStatuses.Posted);
        if (exceptReturnId is { } skip)
        {
            query = query.Where(x => x.Id != skip);
        }

        var rows = await query
            .SelectMany(x => x.Lines)
            .Select(x => new { x.VariantId, x.Quantity })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(x => x.VariantId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    }

    private async Task<SalesReturn?> LoadAsync(Guid id, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<SalesReturn> query = _db.SalesReturns
            .Include(x => x.Customer)
            .Include(x => x.OriginalSalesInvoice)
            .Include(x => x.Lines).ThenInclude(x => x.Variant).ThenInclude(x => x.Product);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static void Fill(SalesReturn doc, SaveReturnRequest request, Guid warehouseId, Guid actorUserId)
    {
        doc.CustomerId = request.CustomerId;
        doc.OriginalSalesInvoiceId = request.OriginalSalesInvoiceId;
        doc.WarehouseId = warehouseId;
        doc.ReturnDate = request.ReturnDate.Date;
        doc.Status = ReturnStatuses.Draft;
        doc.BalancePostingStatus = BalancePostingStatuses.NotPosted;
        doc.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        doc.UpdatedByUserId = actorUserId;
    }

    private static ReturnDto Map(SalesReturn row)
    {
        return new ReturnDto(
            row.Id,
            row.Number,
            row.CustomerId,
            row.Customer.Name,
            row.OriginalSalesInvoiceId,
            row.WarehouseId,
            row.ReturnDate,
            row.Status,
            row.BalancePostingStatus,
            row.Notes,
            row.Lines.OrderBy(x => x.LineNumber).Select(x => new ReturnLineDto(
                x.VariantId,
                x.Variant.Product.Name,
                x.Variant.PackagingType,
                x.Variant.PackagingSize,
                x.Quantity)).ToList());
    }
}
