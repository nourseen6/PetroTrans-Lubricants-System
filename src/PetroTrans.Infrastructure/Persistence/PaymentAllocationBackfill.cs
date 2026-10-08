using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Persistence;

internal static class PaymentAllocationBackfill
{
    public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var missing = await db.Payments
            .Where(x => x.InvoiceId != null && x.Status == "posted")
            .Where(x => !db.PaymentAllocations.Any(a => a.PaymentId == x.Id))
            .Select(x => new { x.Id, InvoiceId = x.InvoiceId!.Value, x.Amount, x.CreatedByUserId, x.UpdatedByUserId })
            .ToListAsync(cancellationToken);
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var row in missing)
        {
            db.PaymentAllocations.Add(new PaymentAllocation
            {
                Id = UuidV7.New(),
                PaymentId = row.Id,
                InvoiceId = row.InvoiceId,
                Amount = row.Amount,
                CreatedByUserId = row.CreatedByUserId,
                UpdatedByUserId = row.UpdatedByUserId
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
