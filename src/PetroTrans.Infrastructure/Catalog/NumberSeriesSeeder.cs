using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Settings;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public static class NumberSeriesSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.NumberSeries.AnyAsync(x => x.DocumentType == NumberSeriesTypes.Customer, cancellationToken))
        {
            return;
        }

        var utc = DateTime.UtcNow;
        db.NumberSeries.Add(new NumberSeries
        {
            Id = UuidV7.New(),
            DocumentType = NumberSeriesTypes.Customer,
            Prefix = "CUS",
            Padding = 4,
            NextValue = 1,
            CreatedAt = utc,
            UpdatedAt = utc,
            RowVersion = 1
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
