using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

/// <summary>Adds any default treasury item that is missing. Never renames or re-enables what the owner changed.</summary>
public static class TreasuryCategorySeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existing = await db.TreasuryCategories.Select(x => x.Code).ToListAsync(cancellationToken);
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var order = existing.Count == 0 ? 0 : await db.TreasuryCategories.MaxAsync(x => x.SortOrder, cancellationToken) + 1;
        var added = false;
        foreach (var item in TreasuryCategoryDefaults.All)
        {
            if (known.Contains(item.Code))
            {
                continue;
            }

            db.TreasuryCategories.Add(new TreasuryCategory
            {
                Id = UuidV7.New(),
                Code = item.Code,
                Name = item.Name,
                Direction = item.Direction,
                IsActive = item.IsActive,
                IsSystem = item.IsSystem,
                SortOrder = order++
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
