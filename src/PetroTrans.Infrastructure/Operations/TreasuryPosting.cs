using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Inventory;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

internal static class TreasuryPosting
{
    public static void Queue(
        AppDbContext db,
        Guid actorUserId,
        DateTime occurredOn,
        string direction,
        string category,
        string description,
        decimal amount,
        string sourceType,
        Guid sourceId,
        string? notes = null)
    {
        if (db.TreasuryEntries.Local.Any(x => x.SourceDocumentId == sourceId))
        {
            return;
        }

        if (db.TreasuryEntries.Any(x => x.SourceDocumentId == sourceId))
        {
            return;
        }

        db.TreasuryEntries.Add(new TreasuryEntry
        {
            Id = UuidV7.New(),
            OccurredOn = occurredOn.Date,
            Direction = direction,
            Category = category,
            Description = description.Length <= 256 ? description : description[..256],
            Amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero),
            Notes = notes,
            SourceDocumentType = sourceType,
            SourceDocumentId = sourceId,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        });
    }

    public static void RemoveBySource(AppDbContext db, string sourceType, Guid sourceId)
    {
        var rows = db.TreasuryEntries
            .Where(x => x.SourceDocumentType == sourceType && x.SourceDocumentId == sourceId)
            .ToList();
        db.TreasuryEntries.RemoveRange(rows);
    }

    public static void UpdateAmountBySource(AppDbContext db, string sourceType, Guid sourceId, decimal amount, Guid actorUserId)
    {
        var rows = db.TreasuryEntries
            .Where(x => x.SourceDocumentId == sourceId &&
                (x.SourceDocumentType == sourceType ||
                 (sourceType == SourceDocumentTypes.Payment && x.SourceDocumentType == SourceDocumentTypes.PaymentLink)))
            .ToList();
        foreach (var row in rows)
        {
            row.Amount = decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
            row.UpdatedByUserId = actorUserId;
        }
    }

    public static string? AttachExisting(AppDbContext db, Guid treasuryId, Guid paymentId, decimal amount, Guid actorUserId)
    {
        var entry = db.TreasuryEntries.FirstOrDefault(x => x.Id == treasuryId);
        if (entry is null)
        {
            return "حركة الخزينة غير موجودة.";
        }

        if (entry.Direction != TreasuryDirections.In)
        {
            return "يمكن ربط وارد الخزينة فقط بتحصيل عميل.";
        }

        if (decimal.Round(entry.Amount, 4) != decimal.Round(amount, 4))
        {
            return "مبلغ الخزينة لا يطابق مبلغ التحصيل.";
        }

        if (!string.IsNullOrWhiteSpace(entry.SourceDocumentType) || entry.SourceDocumentId is not null)
        {
            return "حركة الخزينة مربوطة مسبقاً.";
        }

        entry.SourceDocumentType = SourceDocumentTypes.PaymentLink;
        entry.SourceDocumentId = paymentId;
        entry.UpdatedByUserId = actorUserId;
        return null;
    }

    public static void UnlinkAttached(AppDbContext db, Guid paymentId)
    {
        var rows = db.TreasuryEntries
            .Where(x => x.SourceDocumentType == SourceDocumentTypes.PaymentLink && x.SourceDocumentId == paymentId)
            .ToList();
        foreach (var row in rows)
        {
            row.SourceDocumentType = null;
            row.SourceDocumentId = null;
        }
    }

    public static void QueueCustomerPayment(AppDbContext db, Guid actorUserId, DateTime paidOn, string customerName, decimal amount, Guid paymentId, string? methodName = null)
    {
        var bank = TreasuryCategories.LooksLikeBank(methodName);
        Queue(
            db,
            actorUserId,
            paidOn,
            TreasuryDirections.In,
            bank ? TreasuryCategories.BankDeposit : TreasuryCategories.Sales,
            bank ? $"تحصيل بنكي من {customerName}" : $"تحصيل من {customerName}",
            amount,
            SourceDocumentTypes.Payment,
            paymentId);
    }

    public static void QueueSupplierPayment(AppDbContext db, Guid actorUserId, DateTime paidOn, string supplierName, decimal amount, Guid paymentId, string? methodName = null)
    {
        var bank = TreasuryCategories.LooksLikeBank(methodName);
        Queue(
            db,
            actorUserId,
            paidOn,
            TreasuryDirections.Out,
            bank ? TreasuryCategories.BankDeposit : TreasuryCategories.SupplierPayment,
            bank ? $"سداد بنكي للمورد {supplierName}" : $"سداد للمورد {supplierName}",
            amount,
            SourceDocumentTypes.SupplierPayment,
            paymentId);
    }
}
