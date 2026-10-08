using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Identity;
using PetroTrans.Domain;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Identity;

public static class IdentityCatalogSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var utc = DateTime.UtcNow;

        var permissions = await db.Permissions.ToListAsync(cancellationToken);
        foreach (var code in PermissionCatalog.All)
        {
            if (permissions.Any(x => x.Code == code))
            {
                continue;
            }

            db.Permissions.Add(new Permission
            {
                Id = UuidV7.New(),
                Code = code,
                CreatedAt = utc,
                UpdatedAt = utc,
                RowVersion = 1
            });
        }

        var roles = await db.Roles.ToListAsync(cancellationToken);
        foreach (var (code, nameAr) in PermissionCatalog.Roles)
        {
            var existing = roles.FirstOrDefault(x => x.Code == code);
            if (existing is null)
            {
                db.Roles.Add(new Role
                {
                    Id = UuidV7.New(),
                    Code = code,
                    NameAr = nameAr,
                    CreatedAt = utc,
                    UpdatedAt = utc,
                    RowVersion = 1
                });
            }
            else if (existing.NameAr != nameAr)
            {
                existing.NameAr = nameAr;
                existing.UpdatedAt = utc;
                existing.RowVersion++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        permissions = await db.Permissions.ToListAsync(cancellationToken);
        roles = await db.Roles.Include(x => x.RolePermissions).ToListAsync(cancellationToken);
        var permissionByCode = permissions.ToDictionary(x => x.Code, StringComparer.Ordinal);

        foreach (var role in roles)
        {
            var intended = PermissionCatalog.PermissionsForRole(role.Code);
            var intendedIds = intended
                .Where(permissionByCode.ContainsKey)
                .Select(code => permissionByCode[code].Id)
                .ToHashSet();

            var extras = role.RolePermissions.Where(x => !intendedIds.Contains(x.PermissionId)).ToList();
            foreach (var extra in extras)
            {
                db.RolePermissions.Remove(extra);
            }

            var existingIds = role.RolePermissions.Select(x => x.PermissionId).ToHashSet();
            foreach (var permissionId in intendedIds.Where(id => !existingIds.Contains(id)))
            {
                db.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permissionId
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
