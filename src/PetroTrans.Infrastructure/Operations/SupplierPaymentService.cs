using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class SupplierPaymentService : ISupplierPaymentService
{
    private readonly AppDbContext _db;
    private readonly IPartyLedgerService _ledger;

    public SupplierPaymentService(AppDbContext db, IPartyLedgerService ledger)
    {
        _db = db;
        _ledger = ledger;
    }

    public async Task<IReadOnlyList<SupplierPaymentListItemDto>> ListAsync(Guid? supplierId, CancellationToken cancellationToken = default)
    {
        var query = _db.SupplierPayments.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.PaymentMethod)
            .AsQueryable();
        if (supplierId is { } id)
        {
            query = query.Where(x => x.SupplierId == id);
        }

        return await query
            .OrderByDescending(x => x.PaidOn)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new SupplierPaymentListItemDto(
                x.Id,
                x.PaidOn,
                x.Supplier.Name,
                x.Amount,
                x.PaymentMethod.Name,
                x.Reference))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<SupplierPaymentDto>> CreateAsync(SaveSupplierPaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var methods = await _db.PaymentMethods.CountAsync(x => x.IsActive, cancellationToken);
        if (methods == 0)
        {
            return OperationResult<SupplierPaymentDto>.Fail("أضف طريقة دفع من الإعدادات");
        }

        var method = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == request.PaymentMethodId && x.IsActive, cancellationToken);
        if (method is null)
        {
            return OperationResult<SupplierPaymentDto>.Fail("أضف طريقة دفع من الإعدادات");
        }

        if (request.Amount <= 0)
        {
            return OperationResult<SupplierPaymentDto>.Fail("مبلغ الدفعة يجب أن يكون أكبر من صفر.");
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return OperationResult<SupplierPaymentDto>.Fail("المورد غير موجود.");
        }

        var amount = decimal.Round(request.Amount, 4, MidpointRounding.AwayFromZero);
        var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Supplier, supplier.Id, cancellationToken);
        if (amount > outstanding)
        {
            return OperationResult<SupplierPaymentDto>.Fail("المبلغ لا يمكن أن يتجاوز المستحق للمورد.");
        }

        var payment = new SupplierPayment
        {
            Id = UuidV7.New(),
            SupplierId = supplier.Id,
            PaymentMethodId = method.Id,
            Amount = amount,
            PaidOn = request.PaidOn == default ? DateTime.UtcNow.Date : request.PaidOn.Date,
            Reference = Trim(request.Reference),
            Notes = Trim(request.Notes),
            Status = "posted",
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.SupplierPayments.Add(payment);
        await _ledger.AddAsync(
            PartyKinds.Supplier,
            supplier.Id,
            LedgerEntryTypes.SupplierPayment,
            -amount,
            SourceDocumentTypes.SupplierPayment,
            payment.Id,
            payment.PaidOn,
            actorUserId,
            cancellationToken);
        TreasuryPosting.QueueSupplierPayment(_db, actorUserId, payment.PaidOn, supplier.Name, amount, payment.Id, method.Name);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "payments.supplier", "supplier_payment", payment.Id, null, new { supplier.Id, amount, remaining = outstanding - amount }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<SupplierPaymentDto>.Ok(new SupplierPaymentDto(payment.Id, payment.SupplierId, payment.PaymentMethodId, payment.Amount, payment.PaidOn, payment.Reference, payment.Notes));
    }

    public async Task<OperationResult<SupplierPaymentDto>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        var payment = await _db.SupplierPayments
            .Include(x => x.Supplier)
            .Include(x => x.PaymentMethod)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (payment is null)
        {
            return OperationResult<SupplierPaymentDto>.Fail("الدفعة غير موجودة.", 404);
        }

        var dto = new SupplierPaymentDto(payment.Id, payment.SupplierId, payment.PaymentMethodId, payment.Amount, payment.PaidOn, payment.Reference, payment.Notes);
        var ledger = await _db.PartyLedgerEntries
            .Where(x => x.SourceDocumentType == SourceDocumentTypes.SupplierPayment && x.SourceDocumentId == payment.Id)
            .ToListAsync(cancellationToken);
        _db.PartyLedgerEntries.RemoveRange(ledger);
        TreasuryPosting.RemoveBySource(_db, SourceDocumentTypes.SupplierPayment, payment.Id);
        _db.SupplierPayments.Remove(payment);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "payments.supplier_void", "supplier_payment", dto.Id, new { dto.Amount, payment.SupplierId }, new { voided = true }));
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return OperationResult<SupplierPaymentDto>.Ok(dto);
    }

    private static string? Trim(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
