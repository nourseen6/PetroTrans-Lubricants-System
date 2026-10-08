using Microsoft.EntityFrameworkCore;
using PetroTrans.Infrastructure.Catalog;
using PetroTrans.Infrastructure.Identity;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        await IdentityCatalogSeeder.SeedAsync(db, cancellationToken);
        await NumberSeriesSeeder.SeedAsync(db, cancellationToken);
        await PaymentAllocationBackfill.EnsureAsync(db, cancellationToken);
    }
}
