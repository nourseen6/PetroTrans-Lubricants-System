using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Identity;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Returns;
using PetroTrans.Domain.Sales;
using PetroTrans.Domain.Settings;
using PetroTrans.Infrastructure.Operations;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Sales;

public sealed class SalesInvoiceService : ISalesInvoiceService
{
    private readonly AppDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IPartyLedgerService _ledger;

    public SalesInvoiceService(AppDbContext db, IInventoryService inventory, IPartyLedgerService ledger)
    {
        _db = db;
        _inventory = inventory;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(string? search, Guid? customerId = null, CancellationToken cancellationToken = default)
    {
        var query = _db.SalesInvoices.AsNoTracking().Include(x => x.Customer).AsQueryable();
        if (customerId is { } id)
        {
            query = query.Where(x => x.CustomerId == id);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Customer.Name.Contains(term) ||
                x.Customer.Code.Contains(term) ||
                (x.Number != null && x.Number.Contains(term)) ||
                (x.Notes != null && x.Notes.Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new InvoiceListItemDto(
                x.Id,
                x.Number,
                x.InvoiceDate,
                x.DueDate,
                x.Customer.Name,
                x.Customer.Code,
                x.GoodsTotal,
                x.PaidTotal,
                x.RemainingTotal,
                x.Status,
                x.PaymentStatus))
            .ToListAsync(cancellationToken);
    }

    public async Task<InvoiceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await LoadAsync(id, asNoTracking: true, cancellationToken);
        return invoice is null ? null : await MapAsync(invoice, cancellationToken);
    }

    public async Task<OperationResult<InvoiceDto>> CreateAsync(
        SaveInvoiceRequest request,
        Guid actorUserId,
        bool canOverridePrice,
        CancellationToken cancellationToken = default)
    {
        var built = await BuildLinesAsync(request, actorUserId, canOverridePrice, previousLines: null, cancellationToken);
        if (built.Error is not null)
        {
            return OperationResult<InvoiceDto>.Fail(built.Error, built.ErrorStatus);
        }

        var totals = ResolveTotals(built.Lines, request.DiscountAmount, request.ManualTotal, request.ManualTotalReason);
        if (totals.Error is not null)
        {
            return OperationResult<InvoiceDto>.Fail(totals.Error);
        }

        var invoice = new SalesInvoice
        {
            Id = UuidV7.New(),
            Number = null,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            InvoiceDate = request.InvoiceDate.Date,
            DueDate = request.DueDate?.Date,
            Status = SalesStatuses.Draft,
            Notes = TrimToNull(request.Notes),
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        ApplyTotals(invoice, totals);
        foreach (var line in built.Lines)
        {
            line.InvoiceId = invoice.Id;
            line.CreatedByUserId = actorUserId;
            line.UpdatedByUserId = actorUserId;
            invoice.Lines.Add(line);
        }

        _db.AuditLogs.Add(CreateAudit(actorUserId, "sales.create", invoice.Id, null, new { invoice.CustomerId, invoice.GoodsTotal, invoice.HasManualTotal, lineCount = invoice.Lines.Count }));
        foreach (var audit in built.OverrideAudits)
        {
            audit.EntityId = invoice.Id;
            _db.AuditLogs.Add(audit);
        }

        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<InvoiceDto>.Ok((await GetAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<InvoiceDto>> UpdateAsync(
        Guid id,
        SaveInvoiceRequest request,
        Guid actorUserId,
        bool canOverridePrice,
        CancellationToken cancellationToken = default)
    {
        var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<InvoiceDto>.Fail("الفاتورة غير موجودة.", StatusCodes404);
        }

        if (invoice.Status != SalesStatuses.Draft)
        {
            return OperationResult<InvoiceDto>.Fail("يمكن تعديل المسودة فقط.");
        }

        var previousLines = await _db.SalesInvoiceLines
            .AsNoTracking()
            .Where(x => x.InvoiceId == id)
            .Select(x => new SalesInvoiceLine
            {
                VariantId = x.VariantId,
                UnitPrice = x.UnitPrice,
                PriceSource = x.PriceSource
            })
            .ToListAsync(cancellationToken);
        var built = await BuildLinesAsync(request, actorUserId, canOverridePrice, previousLines, cancellationToken);
        if (built.Error is not null)
        {
            return OperationResult<InvoiceDto>.Fail(built.Error, built.ErrorStatus);
        }

        var totals = ResolveTotals(built.Lines, request.DiscountAmount, request.ManualTotal, request.ManualTotalReason);
        if (totals.Error is not null)
        {
            return OperationResult<InvoiceDto>.Fail(totals.Error);
        }

        var before = new { invoice.CustomerId, invoice.GoodsTotal, invoice.HasManualTotal, invoice.DueDate };
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var existingLines = await _db.SalesInvoiceLines.Where(x => x.InvoiceId == invoice.Id).ToListAsync(cancellationToken);
            if (existingLines.Count > 0)
            {
                _db.SalesInvoiceLines.RemoveRange(existingLines);
                await _db.SaveChangesAsync(cancellationToken);
            }

            _db.ChangeTracker.Clear();
            invoice = await _db.SalesInvoices.FirstAsync(x => x.Id == id, cancellationToken);
            invoice.CustomerId = request.CustomerId;
            invoice.WarehouseId = request.WarehouseId;
            invoice.InvoiceDate = request.InvoiceDate.Date;
            invoice.DueDate = request.DueDate?.Date;
            invoice.Notes = TrimToNull(request.Notes);
            invoice.UpdatedByUserId = actorUserId;
            ApplyTotals(invoice, totals);
            foreach (var line in built.Lines)
            {
                line.InvoiceId = invoice.Id;
                line.CreatedByUserId = actorUserId;
                line.UpdatedByUserId = actorUserId;
                invoice.Lines.Add(line);
                _db.SalesInvoiceLines.Add(line);
            }

            _db.AuditLogs.Add(CreateAudit(actorUserId, "sales.update", invoice.Id, before, new { invoice.CustomerId, invoice.GoodsTotal, invoice.HasManualTotal, lineCount = invoice.Lines.Count }));
            foreach (var audit in built.OverrideAudits)
            {
                audit.EntityId = invoice.Id;
                _db.AuditLogs.Add(audit);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return OperationResult<InvoiceDto>.Fail("الفاتورة اتعدلت من شاشة تانية. اقفلها وافتحها من جديد.", 409);
        }

        return OperationResult<InvoiceDto>.Ok((await GetAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<InvoiceDto>> PostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await LoadAsync(id, asNoTracking: false, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<InvoiceDto>.Fail("الفاتورة غير موجودة.", StatusCodes404);
        }

        if (invoice.Status != SalesStatuses.Draft)
        {
            return OperationResult<InvoiceDto>.Fail("تم ترحيل الفاتورة مسبقاً.");
        }

        if (invoice.WarehouseId is null)
        {
            return OperationResult<InvoiceDto>.Fail("أضف مخزناً من الإعدادات");
        }

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == invoice.WarehouseId && x.IsActive, cancellationToken);
        if (warehouse is null)
        {
            return OperationResult<InvoiceDto>.Fail("أضف مخزناً من الإعدادات");
        }

        var series = await _db.NumberSeries.FirstOrDefaultAsync(x => x.DocumentType == NumberSeriesTypes.SalesInvoice, cancellationToken);
        if (series is null || string.IsNullOrWhiteSpace(series.Prefix))
        {
            return OperationResult<InvoiceDto>.Fail("أضف ترقيم الفواتير من الإعدادات");
        }

        foreach (var line in invoice.Lines)
        {
            if (line.UnitPrice is null or <= 0)
            {
                return OperationResult<InvoiceDto>.Fail("لا يوجد سعر — لا يمكن الترحيل");
            }
        }

        var grouped = invoice.Lines.GroupBy(x => x.VariantId).Select(g => new { VariantId = g.Key, Qty = g.Sum(x => x.Quantity) });
        foreach (var group in grouped)
        {
            var onHand = await _inventory.GetOnHandAsync(warehouse.Id, group.VariantId, cancellationToken);
            if (onHand < group.Qty)
            {
                return OperationResult<InvoiceDto>.Fail($"الكمية المتاحة غير كافية. المتاح: {onHand}");
            }
        }

        foreach (var line in invoice.Lines)
        {
            var error = await _inventory.ApplyAsync(
                warehouse.Id,
                line.VariantId,
                MovementTypes.Sale,
                MovementDirections.Out,
                line.Quantity,
                SourceDocumentTypes.SalesInvoice,
                invoice.Id,
                invoice.InvoiceDate,
                actorUserId,
                invoice.Notes,
                cancellationToken);
            if (error is not null)
            {
                return OperationResult<InvoiceDto>.Fail(error);
            }
        }

        if (string.IsNullOrWhiteSpace(invoice.Number))
        {
            invoice.Number = $"{series.Prefix}-{series.NextValue.ToString().PadLeft(series.Padding, '0')}";
            series.NextValue++;
        }
        invoice.Status = SalesStatuses.Posted;
        invoice.PaidTotal = 0m;
        invoice.RemainingTotal = invoice.GoodsTotal;
        invoice.PaymentStatus = PaymentStatuses.Unpaid;
        invoice.UpdatedByUserId = actorUserId;
        await _ledger.AddAsync(
            PartyKinds.Customer,
            invoice.CustomerId,
            LedgerEntryTypes.SalesInvoice,
            invoice.GoodsTotal,
            SourceDocumentTypes.SalesInvoice,
            invoice.Id,
            invoice.InvoiceDate,
            actorUserId,
            cancellationToken);
        _db.AuditLogs.Add(CreateAudit(actorUserId, "sales.post", invoice.Id, new { status = "draft" }, new { invoice.Number, invoice.GoodsTotal, warehouse.Id }));
        if (invoice.Lines.Any(x => x.PriceSource == PriceSources.ManualOverride))
        {
            _db.AuditLogs.Add(CreateAudit(actorUserId, "pricing.override", invoice.Id, null, new { invoice.Id, invoice.Number }));
        }

        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<InvoiceDto>.Ok((await GetAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<InvoiceDto>> UnpostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await LoadAsync(id, asNoTracking: false, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<InvoiceDto>.Fail("الفاتورة غير موجودة.", StatusCodes404);
        }

        if (invoice.Status != SalesStatuses.Posted)
        {
            return OperationResult<InvoiceDto>.Fail("يمكن إرجاع الفاتورة المرحّلة فقط.");
        }

        if (await HasPostedReturnAsync(invoice.Id, cancellationToken))
        {
            return OperationResult<InvoiceDto>.Fail("لا يمكن إرجاع الفاتورة وعليها مرتجع مرحّل.");
        }

        var allocationPaymentIds = await _db.PaymentAllocations
            .Where(x => x.InvoiceId == invoice.Id)
            .Select(x => x.PaymentId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var payments = await _db.Payments
            .Where(x => x.InvoiceId == invoice.Id || allocationPaymentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var split = await _db.PaymentAllocations
            .AnyAsync(x => allocationPaymentIds.Contains(x.PaymentId) && x.InvoiceId != invoice.Id, cancellationToken);
        if (split)
        {
            return OperationResult<InvoiceDto>.Fail("لا يمكن إرجاع الفاتورة لأن عليها تحصيل موزع على فواتير أخرى. أعد توزيع التحصيل أو احذفه أولاً.");
        }

        var stockError = await _inventory.ReverseSourceAsync(SourceDocumentTypes.SalesInvoice, invoice.Id, cancellationToken);
        if (stockError is not null)
        {
            return OperationResult<InvoiceDto>.Fail(stockError);
        }

        var paymentIds = payments.Select(x => x.Id).ToList();
        var ledger = await _db.PartyLedgerEntries
            .Where(x =>
                (x.SourceDocumentType == SourceDocumentTypes.SalesInvoice && x.SourceDocumentId == invoice.Id) ||
                (x.SourceDocumentType == SourceDocumentTypes.Payment && paymentIds.Contains(x.SourceDocumentId)))
            .ToListAsync(cancellationToken);
        _db.PartyLedgerEntries.RemoveRange(ledger);
        foreach (var paymentId in paymentIds)
        {
            TreasuryPosting.RemoveBySource(_db, SourceDocumentTypes.Payment, paymentId);
        }

        _db.Payments.RemoveRange(payments);

        var before = new { invoice.Status, invoice.Number, invoice.GoodsTotal, invoice.PaidTotal, paymentCount = payments.Count };
        invoice.Status = SalesStatuses.Draft;
        invoice.PaidTotal = 0m;
        invoice.RemainingTotal = invoice.GoodsTotal;
        invoice.PaymentStatus = PaymentStatuses.Unpaid;
        invoice.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(CreateAudit(actorUserId, "sales.unpost", invoice.Id, before, new { status = "draft", invoice.Number }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<InvoiceDto>.Ok((await GetAsync(invoice.Id, cancellationToken))!);
    }

    public async Task<OperationResult<InvoiceDto>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var invoice = await LoadAsync(id, asNoTracking: false, cancellationToken);
        if (invoice is null)
        {
            return OperationResult<InvoiceDto>.Fail("الفاتورة غير موجودة.", StatusCodes404);
        }

        var allocationCount = await _db.PaymentAllocations.CountAsync(x => x.InvoiceId == invoice.Id, cancellationToken);
        var paymentCount = await _db.Payments.CountAsync(x => x.InvoiceId == invoice.Id, cancellationToken);
        if (allocationCount > 0 || paymentCount > 0)
        {
            return OperationResult<InvoiceDto>.Fail("لا يمكن حذف الفاتورة وعليها تحصيل. احذف التحصيل أولاً.");
        }

        if (await HasPostedReturnAsync(invoice.Id, cancellationToken))
        {
            return OperationResult<InvoiceDto>.Fail("لا يمكن حذف الفاتورة وعليها مرتجع مرحّل.");
        }

        var before = new { invoice.Status, invoice.Number, invoice.GoodsTotal, invoice.PaidTotal };
        if (invoice.Status == SalesStatuses.Posted)
        {
            var stockError = await _inventory.ReverseSourceAsync(SourceDocumentTypes.SalesInvoice, invoice.Id, cancellationToken);
            if (stockError is not null)
            {
                return OperationResult<InvoiceDto>.Fail(stockError);
            }

            var ledger = await _db.PartyLedgerEntries
                .Where(x => x.SourceDocumentType == SourceDocumentTypes.SalesInvoice && x.SourceDocumentId == invoice.Id)
                .ToListAsync(cancellationToken);
            _db.PartyLedgerEntries.RemoveRange(ledger);
        }

        var dto = await MapAsync(invoice, cancellationToken);
        _db.SalesInvoices.Remove(invoice);
        _db.AuditLogs.Add(CreateAudit(actorUserId, "sales.delete", invoice.Id, before, new { deleted = true, invoice.Number }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<InvoiceDto>.Ok(dto);
    }

    private async Task<bool> HasPostedReturnAsync(Guid invoiceId, CancellationToken cancellationToken)
        => await _db.SalesReturns.AnyAsync(
            x => x.OriginalSalesInvoiceId == invoiceId && x.Status == ReturnStatuses.Posted,
            cancellationToken);

    private async Task<SalesInvoice?> LoadAsync(Guid id, bool asNoTracking, CancellationToken cancellationToken)
    {
        IQueryable<SalesInvoice> query = _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Warehouse)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Variant)
                    .ThenInclude(x => x.Product);
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private async Task<BuiltLines> BuildLinesAsync(
        SaveInvoiceRequest request,
        Guid actorUserId,
        bool canOverridePrice,
        IReadOnlyList<SalesInvoiceLine>? previousLines,
        CancellationToken cancellationToken)
    {
        if (request.InvoiceDate == default)
        {
            return BuiltLines.Fail("تاريخ الفاتورة مطلوب.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            return BuiltLines.Fail("اختر العميل من القائمة.");
        }

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);
        if (customer is null)
        {
            return BuiltLines.Fail("العميل غير موجود.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            return BuiltLines.Fail("أضف بنداً واحداً على الأقل.");
        }

        var variantIds = request.Lines.Select(x => x.VariantId).Distinct().ToList();
        var variants = await _db.ProductVariants
            .Include(x => x.Product)
            .Where(x => variantIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        if (variants.Count != variantIds.Count)
        {
            return BuiltLines.Fail("العبوة غير موجودة.");
        }

        var customerPrices = await _db.CustomerVariantPrices
            .AsNoTracking()
            .Where(x => x.CustomerId == customer.Id && variantIds.Contains(x.VariantId))
            .ToDictionaryAsync(x => x.VariantId, x => x.UnitPrice, cancellationToken);

        var lines = new List<SalesInvoiceLine>();
        var audits = new List<AuditLog>();
        var remainingPrevious = previousLines?.ToList() ?? [];
        var lineNumber = 1;
        foreach (var item in request.Lines)
        {
            if (item.Quantity <= 0)
            {
                return BuiltLines.Fail("الكمية يجب أن تكون أكبر من صفر.");
            }

            if (item.UnitPrice is < 0)
            {
                return BuiltLines.Fail("السعر لا يمكن أن يكون سالباً.");
            }

            var variant = variants.Single(x => x.Id == item.VariantId);
            var previous = remainingPrevious.FirstOrDefault(x => x.VariantId == item.VariantId);
            if (previous is not null)
            {
                remainingPrevious.Remove(previous);
            }

            var basePrice = Round(variant.StandardWholesalePrice);
            var customerPrice = customerPrices.TryGetValue(variant.Id, out var special) ? Round(special) : null;
            var proposed = customerPrice ?? basePrice;
            var incoming = Round(item.UnitPrice);
            var isOverride = incoming is not null && !PricesEqual(incoming, proposed);
            if (isOverride && !canOverridePrice)
            {
                var preservesExisting = previous is not null
                    && previous.PriceSource == PriceSources.ManualOverride
                    && PricesEqual(incoming, previous.UnitPrice);
                if (!preservesExisting)
                {
                    return BuiltLines.Fail("غير مسموح بتغيير السعر.", 403);
                }
            }

            var unitPrice = incoming ?? proposed;
            string? priceSource = null;
            if (unitPrice is not null)
            {
                if (isOverride)
                {
                    priceSource = PriceSources.ManualOverride;
                }
                else if (customerPrice is not null && PricesEqual(unitPrice, customerPrice))
                {
                    priceSource = PriceSources.CustomerSpecific;
                }
                else
                {
                    priceSource = PriceSources.Standard;
                }
            }

            var line = new SalesInvoiceLine
            {
                Id = UuidV7.New(),
                LineNumber = lineNumber++,
                VariantId = variant.Id,
                Quantity = Round(item.Quantity)!.Value,
                UnitPrice = unitPrice,
                LineTotal = unitPrice is null ? null : Round(item.Quantity * unitPrice.Value),
                PriceSource = priceSource,
                ResolvedUnitPrice = proposed,
                OverrideReason = isOverride ? TrimToNull(item.OverrideReason) : null,
                OverrideByUserId = isOverride ? actorUserId : null,
            };
            lines.Add(line);

            if (isOverride && ShouldAuditOverride(previousLines, variant.Id, unitPrice))
            {
                audits.Add(CreateAudit(
                    actorUserId,
                    "pricing.override",
                    null,
                    new { resolved = proposed },
                    new
                    {
                        customerId = customer.Id,
                        customerName = customer.Name,
                        productId = variant.ProductId,
                        productName = variant.Product.Name,
                        variantId = variant.Id,
                        packagingType = variant.PackagingType,
                        packagingSize = variant.PackagingSize,
                        oldPrice = proposed,
                        newPrice = unitPrice
                    }));
            }
        }

        return new BuiltLines(null, 400, lines, audits);
    }

    private static bool ShouldAuditOverride(IReadOnlyList<SalesInvoiceLine>? previousLines, Guid variantId, decimal? unitPrice)
    {
        if (previousLines is null)
        {
            return true;
        }

        return !previousLines.Any(x =>
            x.VariantId == variantId &&
            x.PriceSource == PriceSources.ManualOverride &&
            PricesEqual(x.UnitPrice, unitPrice));
    }

    private static TotalsResult ResolveTotals(
        IReadOnlyList<SalesInvoiceLine> lines,
        decimal discountAmount,
        decimal? manualTotal,
        string? manualTotalReason)
    {
        var subtotal = Round(lines.Sum(x => x.LineTotal ?? 0m))!.Value;
        var discount = Round(discountAmount)!.Value;
        if (discount < 0)
        {
            return TotalsResult.Fail("الخصم لا يمكن أن يكون سالباً.");
        }

        if (discount > subtotal)
        {
            return TotalsResult.Fail("الخصم أكبر من إجمالي البنود.");
        }

        var afterDiscount = Round(subtotal - discount)!.Value;
        if (manualTotal is null)
        {
            return new TotalsResult(null, subtotal, discount, 0m, false, null, afterDiscount);
        }

        var finalTotal = Round(manualTotal.Value)!.Value;
        if (finalTotal < 0)
        {
            return TotalsResult.Fail("الإجمالي النهائي لا يمكن أن يكون سالباً.");
        }

        if (string.IsNullOrWhiteSpace(manualTotalReason))
        {
            return TotalsResult.Fail("سبب تعديل الإجمالي مطلوب.");
        }

        var adjustment = Round(finalTotal - afterDiscount)!.Value;
        return new TotalsResult(null, subtotal, discount, adjustment, true, TrimToNull(manualTotalReason), finalTotal);
    }

    private static void ApplyTotals(SalesInvoice invoice, TotalsResult totals)
    {
        invoice.LinesSubtotal = totals.LinesSubtotal;
        invoice.DiscountAmount = totals.DiscountAmount;
        invoice.ManualAdjustment = totals.ManualAdjustment;
        invoice.HasManualTotal = totals.HasManualTotal;
        invoice.ManualTotalReason = totals.ManualTotalReason;
        invoice.GoodsTotal = totals.GoodsTotal;
        invoice.PaidTotal = 0m;
        invoice.RemainingTotal = invoice.GoodsTotal;
        invoice.PaymentStatus = PaymentStatuses.Unpaid;
        invoice.Status = SalesStatuses.Draft;
    }

    private async Task<InvoiceDto> MapAsync(SalesInvoice invoice, CancellationToken cancellationToken)
    {
        var ids = new[] { invoice.CreatedByUserId, invoice.UpdatedByUserId }.Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
        var names = ids.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Users.AsNoTracking()
                .Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
        var dto = Map(invoice);
        return dto with
        {
            CreatedByName = invoice.CreatedByUserId is { } created ? names.GetValueOrDefault(created) : null,
            UpdatedByName = invoice.UpdatedByUserId is { } updated ? names.GetValueOrDefault(updated) : null
        };
    }

    private static InvoiceDto Map(SalesInvoice invoice)
    {
        return new InvoiceDto(
            invoice.Id,
            invoice.Number,
            invoice.CustomerId,
            invoice.Customer.Name,
            invoice.WarehouseId,
            invoice.Warehouse?.Name,
            invoice.InvoiceDate,
            invoice.DueDate,
            invoice.Status,
            invoice.LinesSubtotal,
            invoice.DiscountAmount,
            invoice.ManualAdjustment,
            invoice.HasManualTotal,
            invoice.ManualTotalReason,
            invoice.GoodsTotal,
            invoice.PaidTotal,
            invoice.RemainingTotal,
            invoice.PaymentStatus,
            invoice.Notes,
            invoice.Lines
                .OrderBy(x => x.LineNumber)
                .Select(x => new InvoiceLineDto(
                    x.Id,
                    x.LineNumber,
                    x.VariantId,
                    x.Variant.Product.Name,
                    x.Variant.PackagingType,
                    x.Variant.PackagingSize,
                    x.Quantity,
                    x.UnitPrice,
                    x.LineTotal,
                    x.PriceSource,
                    x.ResolvedUnitPrice,
                    x.OverrideReason))
                .ToList(),
            invoice.CreatedAt,
            invoice.UpdatedAt);
    }

    private static AuditLog CreateAudit(Guid actorUserId, string action, Guid? entityId, object? before, object after)
    {
        return new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = action,
            EntityType = "sales_invoice",
            EntityId = entityId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(after)
        };
    }

    private static bool PricesEqual(decimal? left, decimal? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Round(left) == Round(right);
    }

    private static decimal? Round(decimal? value)
    {
        return value is null ? null : decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private const int StatusCodes404 = 404;

    private sealed record BuiltLines(
        string? Error,
        int ErrorStatus,
        List<SalesInvoiceLine> Lines,
        List<AuditLog> OverrideAudits)
    {
        public static BuiltLines Fail(string error, int status = 400)
            => new(error, status, [], []);
    }

    private sealed record TotalsResult(
        string? Error,
        decimal LinesSubtotal,
        decimal DiscountAmount,
        decimal ManualAdjustment,
        bool HasManualTotal,
        string? ManualTotalReason,
        decimal GoodsTotal)
    {
        public static TotalsResult Fail(string error)
            => new(error, 0m, 0m, 0m, false, null, 0m);
    }
}
