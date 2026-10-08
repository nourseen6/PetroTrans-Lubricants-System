using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Sales;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPartyLedgerService _ledger;

    public PaymentService(AppDbContext db, IPartyLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<PaymentListItemDto>> ListAsync(Guid? customerId, CancellationToken cancellationToken = default)
    {
        var query = _db.Payments.AsNoTracking()
            .Include(x => x.Invoice)
            .Include(x => x.PaymentMethod)
            .Include(x => x.Allocations).ThenInclude(x => x.Invoice)
            .AsQueryable();
        if (customerId is { } id)
        {
            query = query.Where(x => x.PartyId == id);
        }

        var rows = await query
            .OrderByDescending(x => x.PaidOn)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var customerIds = rows.Select(x => x.PartyId).Distinct().ToList();
        var names = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return rows.Select(x => new PaymentListItemDto(
            x.Id,
            x.PaidOn,
            names.GetValueOrDefault(x.PartyId, "—"),
            InvoiceLabel(x),
            x.Amount,
            x.PaymentMethod.Name,
            x.Reference)).ToList();
    }

    public async Task<PaymentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var payment = await _db.Payments.AsNoTracking()
            .Include(x => x.Invoice)
            .Include(x => x.PaymentMethod)
            .Include(x => x.Allocations).ThenInclude(x => x.Invoice)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return payment is null ? null : await MapAsync(payment, cancellationToken);
    }

    public async Task<OperationResult<PaymentDto>> CreateAsync(SavePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, actorUserId, cancellationToken);
        if (prepared.Error is not null)
        {
            return OperationResult<PaymentDto>.Fail(prepared.Error);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var payment = new Payment
        {
            Id = UuidV7.New(),
            PartyKind = PartyKinds.Customer,
            PartyId = prepared.CustomerId,
            InvoiceId = prepared.PrimaryInvoiceId,
            PaymentMethodId = prepared.MethodId,
            Amount = prepared.Amount,
            PaidOn = prepared.PaidOn,
            Reference = Trim(request.Reference),
            Notes = Trim(request.Notes),
            Status = "posted",
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.Payments.Add(payment);

        var allocError = await ReplaceAllocationsAsync(payment, prepared.Allocations, actorUserId, cancellationToken);
        if (allocError is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return OperationResult<PaymentDto>.Fail(allocError);
        }

        await _ledger.AddAsync(
            PartyKinds.Customer,
            payment.PartyId,
            LedgerEntryTypes.Payment,
            -prepared.Amount,
            SourceDocumentTypes.Payment,
            payment.Id,
            payment.PaidOn,
            actorUserId,
            cancellationToken);

        var customerName = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == payment.PartyId)
            .Select(x => x.Name)
            .FirstAsync(cancellationToken);
        var cashError = AttachOrQueueTreasury(request.ExistingTreasuryEntryId, actorUserId, payment.PaidOn, customerName, prepared.Amount, payment.Id, prepared.MethodId);
        if (cashError is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return OperationResult<PaymentDto>.Fail(cashError);
        }

        _db.AuditLogs.Add(Audits.Create(
            actorUserId,
            "payments.create",
            "payment",
            payment.Id,
            null,
            new { payment.PartyId, payment.Amount, allocations = prepared.Allocations.Select(x => new { x.InvoiceId, x.Amount }), linkedTreasury = request.ExistingTreasuryEntryId }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PaymentDto>.Ok((await GetAsync(payment.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PaymentDto>> UpdateAsync(Guid id, SavePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var prepared = await PrepareAsync(request, actorUserId, cancellationToken, id);
        if (prepared.Error is not null)
        {
            return OperationResult<PaymentDto>.Fail(prepared.Error);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await _db.Payments.Include(x => x.Allocations).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null)
        {
            return OperationResult<PaymentDto>.Fail("الدفعة غير موجودة.", 404);
        }

        if (payment.Status != "posted")
        {
            return OperationResult<PaymentDto>.Fail("يمكن تعديل دفعة مرحلة فقط.");
        }

        if (payment.PartyId != prepared.CustomerId)
        {
            return OperationResult<PaymentDto>.Fail("لا يمكن نقل التحصيل إلى عميل آخر.");
        }

        var before = new
        {
            payment.Amount,
            payment.InvoiceId,
            allocations = payment.Allocations.Select(x => new { x.InvoiceId, x.Amount }).ToList()
        };

        var amountChanged = payment.Amount != prepared.Amount;
        payment.Amount = prepared.Amount;
        payment.PaidOn = prepared.PaidOn;
        payment.PaymentMethodId = prepared.MethodId;
        payment.Reference = Trim(request.Reference);
        payment.Notes = Trim(request.Notes);
        payment.InvoiceId = prepared.PrimaryInvoiceId;
        payment.UpdatedByUserId = actorUserId;

        var allocError = await ReplaceAllocationsAsync(payment, prepared.Allocations, actorUserId, cancellationToken);
        if (allocError is not null)
        {
            await tx.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return OperationResult<PaymentDto>.Fail(allocError);
        }

        if (amountChanged)
        {
            var ledger = await _db.PartyLedgerEntries
                .Where(x => x.SourceDocumentType == SourceDocumentTypes.Payment && x.SourceDocumentId == payment.Id)
                .ToListAsync(cancellationToken);
            foreach (var entry in ledger)
            {
                entry.SignedAmount = -prepared.Amount;
                entry.UpdatedByUserId = actorUserId;
            }

            TreasuryPosting.UpdateAmountBySource(_db, SourceDocumentTypes.Payment, payment.Id, prepared.Amount, actorUserId);
        }

        _db.AuditLogs.Add(Audits.Create(
            actorUserId,
            "payments.update",
            "payment",
            payment.Id,
            before,
            new { payment.Amount, payment.InvoiceId, allocations = prepared.Allocations.Select(x => new { x.InvoiceId, x.Amount }) }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PaymentDto>.Ok((await GetAsync(payment.Id, cancellationToken))!);
    }

    public async Task<OperationResult<PaymentDto>> VoidAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await _db.Payments
            .Include(x => x.Allocations).ThenInclude(x => x.Invoice)
            .Include(x => x.PaymentMethod)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null)
        {
            return OperationResult<PaymentDto>.Fail("الدفعة غير موجودة.", 404);
        }

        var affected = payment.Allocations.Select(x => x.InvoiceId).ToList();
        if (payment.InvoiceId is { } linked)
        {
            affected.Add(linked);
        }

        var dto = await MapAsync(payment, cancellationToken);
        var ledger = await _db.PartyLedgerEntries
            .Where(x => x.SourceDocumentType == SourceDocumentTypes.Payment && x.SourceDocumentId == payment.Id)
            .ToListAsync(cancellationToken);
        _db.PartyLedgerEntries.RemoveRange(ledger);
        TreasuryPosting.UnlinkAttached(_db, payment.Id);
        TreasuryPosting.RemoveBySource(_db, SourceDocumentTypes.Payment, payment.Id);
        _db.PaymentAllocations.RemoveRange(payment.Allocations);
        _db.Payments.Remove(payment);
        await RecalculateInvoicesAsync(affected, actorUserId, cancellationToken);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "payments.void", "payment", dto.Id, new { dto.Amount, allocations = dto.Allocations }, new { voided = true }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<PaymentDto>.Ok(dto);
    }

    private async Task<PreparedPayment> PrepareAsync(
        SavePaymentRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken,
        Guid? existingPaymentId = null)
    {
        _ = actorUserId;
        var methods = await _db.PaymentMethods.CountAsync(x => x.IsActive, cancellationToken);
        if (methods == 0)
        {
            return PreparedPayment.Fail("أضف طريقة دفع من الإعدادات");
        }

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == request.PaymentMethodId && x.IsActive, cancellationToken);
        if (method is null)
        {
            return PreparedPayment.Fail("أضف طريقة دفع من الإعدادات");
        }

        if (request.Amount <= 0)
        {
            return PreparedPayment.Fail("مبلغ الدفعة يجب أن يكون أكبر من صفر.");
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var paidOn = request.PaidOn == default ? DateTime.UtcNow.Date : request.PaidOn.Date;
        Guid customerId;
        List<SavePaymentAllocationRequest> allocations;

        if (request.InvoiceId is { } explicitInvoice)
        {
            var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == explicitInvoice, cancellationToken);
            if (invoice is null)
            {
                return PreparedPayment.Fail("الفاتورة غير موجودة.");
            }

            if (invoice.Status != SalesStatuses.Posted)
            {
                return PreparedPayment.Fail("يمكن تسجيل الدفعة على فاتورة مرحّلة فقط.");
            }

            customerId = invoice.CustomerId;
            if (request.Allocations is { Count: > 0 })
            {
                allocations = request.Allocations.Select(x => new SavePaymentAllocationRequest(x.InvoiceId, decimal.Round(x.Amount, 4, MidpointRounding.AwayFromZero))).ToList();
            }
            else
            {
                allocations = [new SavePaymentAllocationRequest(invoice.Id, amount)];
            }
        }
        else
        {
            if (request.CustomerId is not { } id || id == Guid.Empty)
            {
                return PreparedPayment.Fail("اختر العميل أو الفاتورة.");
            }

            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (customer is null)
            {
                return PreparedPayment.Fail("العميل غير موجود.");
            }

            customerId = customer.Id;
            if (request.Allocations is { Count: > 0 })
            {
                allocations = request.Allocations.Select(x => new SavePaymentAllocationRequest(x.InvoiceId, decimal.Round(x.Amount, 4, MidpointRounding.AwayFromZero))).ToList();
            }
            else
            {
                allocations = await FifoAllocationsAsync(customerId, amount, existingPaymentId, cancellationToken);
            }
        }

        if (allocations.Any(x => x.Amount <= 0))
        {
            return PreparedPayment.Fail("مبلغ التوزيع يجب أن يكون أكبر من صفر.");
        }

        var allocated = allocations.Sum(x => x.Amount);
        if (allocated > amount)
        {
            return PreparedPayment.Fail("مجموع التوزيع أكبر من مبلغ التحصيل.");
        }

        allocations = allocations
            .GroupBy(x => x.InvoiceId)
            .Select(g => new SavePaymentAllocationRequest(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        Guid? primary = allocations.Count == 1 ? allocations[0].InvoiceId : null;
        return new PreparedPayment(null, customerId, method.Id, amount, paidOn, primary, allocations);
    }

    private async Task<List<SavePaymentAllocationRequest>> FifoAllocationsAsync(
        Guid customerId,
        decimal amount,
        Guid? excludePaymentId,
        CancellationToken cancellationToken)
    {
        var invoices = await _db.SalesInvoices
            .Where(x => x.CustomerId == customerId && x.Status == SalesStatuses.Posted)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.Number)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var result = new List<SavePaymentAllocationRequest>();
        var remainingCash = amount;
        foreach (var invoice in invoices)
        {
            if (remainingCash <= 0)
            {
                break;
            }

            var already = await AllocatedExcludingAsync(invoice.Id, excludePaymentId, cancellationToken);
            var open = invoice.GoodsTotal - already;
            if (open <= 0)
            {
                continue;
            }

            var take = Math.Min(remainingCash, open);
            result.Add(new SavePaymentAllocationRequest(invoice.Id, take));
            remainingCash -= take;
        }

        return result;
    }

    private async Task<string?> ReplaceAllocationsAsync(
        Payment payment,
        IReadOnlyList<SavePaymentAllocationRequest> allocations,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var previousInvoiceIds = payment.Allocations.Select(x => x.InvoiceId).ToList();
        var requestedIds = allocations.Select(x => x.InvoiceId).ToHashSet();
        var existing = payment.Allocations.ToList();
        foreach (var row in existing.Where(x => !requestedIds.Contains(x.InvoiceId)))
        {
            _db.PaymentAllocations.Remove(row);
        }

        foreach (var row in allocations)
        {
            var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == row.InvoiceId, cancellationToken);
            if (invoice is null)
            {
                return "الفاتورة غير موجودة.";
            }

            if (invoice.Status != SalesStatuses.Posted)
            {
                return "يمكن توزيع التحصيل على فاتورة مرحّلة فقط.";
            }

            if (invoice.CustomerId != payment.PartyId)
            {
                return "لا يمكن توزيع التحصيل على فاتورة عميل آخر.";
            }

            var already = await AllocatedExcludingAsync(invoice.Id, payment.Id, cancellationToken);
            if (already + row.Amount > invoice.GoodsTotal)
            {
                return "المبلغ لا يمكن أن يتجاوز المتبقي على الفاتورة.";
            }

            var current = existing.FirstOrDefault(x => x.InvoiceId == row.InvoiceId);
            if (current is null)
            {
                payment.Allocations.Add(new PaymentAllocation
                {
                    Id = UuidV7.New(),
                    PaymentId = payment.Id,
                    InvoiceId = invoice.Id,
                    Amount = row.Amount,
                    CreatedByUserId = actorUserId,
                    UpdatedByUserId = actorUserId
                });
            }
            else
            {
                current.Amount = row.Amount;
                current.UpdatedByUserId = actorUserId;
            }
        }

        var affected = previousInvoiceIds.Concat(allocations.Select(x => x.InvoiceId)).Distinct().ToList();
        await RecalculateInvoicesAsync(affected, actorUserId, cancellationToken);
        return null;
    }

    private async Task<decimal> AllocatedExcludingAsync(Guid invoiceId, Guid? excludePaymentId, CancellationToken cancellationToken)
    {
        var query = _db.PaymentAllocations.Where(x => x.InvoiceId == invoiceId);
        if (excludePaymentId is { } paymentId)
        {
            query = query.Where(x => x.PaymentId != paymentId);
        }

        var amounts = await query.Select(x => x.Amount).ToListAsync(cancellationToken);
        return amounts.Sum();
    }

    private async Task RecalculateInvoicesAsync(IEnumerable<Guid> invoiceIds, Guid actorUserId, CancellationToken cancellationToken)
    {
        var ids = invoiceIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var dbRows = await _db.PaymentAllocations.AsNoTracking()
            .Where(x => ids.Contains(x.InvoiceId))
            .Select(x => new { x.Id, x.InvoiceId, x.Amount })
            .ToListAsync(cancellationToken);
        var local = _db.ChangeTracker.Entries<PaymentAllocation>().ToList();
        var localIds = local.Select(x => x.Entity.Id).ToHashSet();
        var paidByInvoice = new Dictionary<Guid, decimal>();
        foreach (var row in dbRows.Where(x => !localIds.Contains(x.Id)))
        {
            paidByInvoice[row.InvoiceId] = paidByInvoice.GetValueOrDefault(row.InvoiceId) + row.Amount;
        }

        foreach (var entry in local.Where(x => x.State is not EntityState.Deleted and not EntityState.Detached))
        {
            paidByInvoice[entry.Entity.InvoiceId] = paidByInvoice.GetValueOrDefault(entry.Entity.InvoiceId) + entry.Entity.Amount;
        }

        var returnedByInvoice = await InvoiceSettlement.PostedReturnValuesAsync(_db, ids, null, cancellationToken);
        foreach (var invoiceId in ids)
        {
            var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
            if (invoice is null || invoice.Status != SalesStatuses.Posted)
            {
                continue;
            }

            var paid = decimal.Round(paidByInvoice.GetValueOrDefault(invoiceId), 4, MidpointRounding.AwayFromZero);
            InvoiceSettlement.ApplyRemaining(invoice, paid, returnedByInvoice.GetValueOrDefault(invoiceId));
            invoice.UpdatedByUserId = actorUserId;
        }
    }

    private async Task<PaymentDto> MapAsync(Payment payment, CancellationToken cancellationToken)
    {
        var customerName = await _db.Customers.AsNoTracking()
            .Where(x => x.Id == payment.PartyId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var allocations = payment.Allocations
            .OrderBy(x => x.Invoice.InvoiceDate)
            .Select(x => new PaymentAllocationDto(x.InvoiceId, x.Invoice.Number, x.Amount))
            .ToList();
        return new PaymentDto(
            payment.Id,
            payment.PartyId,
            payment.InvoiceId,
            payment.PaymentMethodId,
            payment.Amount,
            payment.PaidOn,
            payment.Reference,
            payment.Notes,
            customerName ?? "—",
            InvoiceLabel(payment),
            payment.PaymentMethod.Name,
            allocations);
    }

    private static string? InvoiceLabel(Payment payment)
    {
        var numbers = payment.Allocations
            .Select(x => x.Invoice?.Number)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct()
            .ToList();
        if (numbers.Count > 0)
        {
            return string.Join(" + ", numbers);
        }

        return payment.Invoice?.Number;
    }

    private string? AttachOrQueueTreasury(Guid? existingTreasuryId, Guid actorUserId, DateTime paidOn, string customerName, decimal amount, Guid paymentId, Guid methodId)
    {
        if (existingTreasuryId is { } treasuryId)
        {
            return TreasuryPosting.AttachExisting(_db, treasuryId, paymentId, amount, actorUserId);
        }

        var methodName = _db.PaymentMethods.AsNoTracking()
            .Where(x => x.Id == methodId)
            .Select(x => x.Name)
            .FirstOrDefault();
        TreasuryPosting.QueueCustomerPayment(_db, actorUserId, paidOn, customerName, amount, paymentId, methodName);
        return null;
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private sealed record PreparedPayment(
        string? Error,
        Guid CustomerId,
        Guid MethodId,
        decimal Amount,
        DateTime PaidOn,
        Guid? PrimaryInvoiceId,
        List<SavePaymentAllocationRequest> Allocations)
    {
        public static PreparedPayment Fail(string error)
            => new(error, Guid.Empty, Guid.Empty, 0, default, null, []);
    }
}
