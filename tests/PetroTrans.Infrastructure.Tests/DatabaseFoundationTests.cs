using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Identity;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Infrastructure.Tests;

public class DatabaseFoundationTests
{
    [Fact]
    public async Task Migrate_CreatesAuthTables_AndSeedsRoles()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petrotrans-test-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(SqlitePaths.GetConnectionString(path))
                .Options;
            await using (var db = new AppDbContext(options))
            {
                await DatabaseInitializer.InitializeAsync(db);
                Assert.True(File.Exists(path));

                await using var connection = new SqliteConnection(SqlitePaths.GetConnectionString(path));
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
                await using var reader = await command.ExecuteReaderAsync();
                var names = new List<string>();
                while (await reader.ReadAsync())
                {
                    names.Add(reader.GetString(0));
                }

                Assert.Contains("users", names);
                Assert.Contains("roles", names);
                Assert.Contains("permissions", names);
                Assert.Contains("user_roles", names);
                Assert.Contains("role_permissions", names);
                Assert.Contains("audit_log", names);
                Assert.Contains("customers", names);
                Assert.Contains("sales_invoices", names);
                Assert.Contains("sales_invoice_lines", names);
                Assert.Contains("warehouses", names);
                Assert.Contains("inventory_movements", names);
                Assert.Contains("inventory_balances", names);
                Assert.Contains("payments", names);
                Assert.Contains("payment_methods", names);
                Assert.Contains("party_ledger_entries", names);
                Assert.Contains("suppliers", names);
                Assert.Contains("goods_receipts", names);
                Assert.Contains("purchase_invoices", names);
                Assert.Contains("sales_returns", names);
                Assert.Contains("company_settings", names);

                Assert.False(await db.Users.AnyAsync());
                var owner = await db.Roles.SingleAsync(x => x.Code == RoleCodes.OwnerManager);
                var op = await db.Roles.SingleAsync(x => x.Code == RoleCodes.Operator);
                var ownerPerms = await db.RolePermissions
                    .Where(x => x.RoleId == owner.Id)
                    .Select(x => x.Permission.Code)
                    .ToListAsync();
                var operatorPerms = await db.RolePermissions
                    .Where(x => x.RoleId == op.Id)
                    .Select(x => x.Permission.Code)
                    .ToListAsync();

                Assert.Contains(PermissionCodes.PricingOverride, ownerPerms);
                Assert.Contains(PermissionCodes.SalesEditPosted, ownerPerms);
                Assert.Contains(PermissionCodes.PaymentsVoid, ownerPerms);
                Assert.DoesNotContain(PermissionCodes.SalesEditPosted, operatorPerms);
                Assert.DoesNotContain(PermissionCodes.PaymentsVoid, operatorPerms);
                Assert.DoesNotContain(PermissionCodes.RolesManage, ownerPerms);
                Assert.DoesNotContain(PermissionCodes.RolesManage, operatorPerms);
            }

            SqliteConnection.ClearAllPools();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_IsNotPlaintext_AndVerifies()
    {
        var hasher = new AspNetPasswordHasher();
        var hash = hasher.Hash("secret-pass");

        Assert.NotEqual("secret-pass", hash);
        Assert.True(hasher.Verify(hash, "secret-pass"));
        Assert.False(hasher.Verify(hash, "other-pass"));
    }

    [Fact]
    public void Hash_UsesUniqueSalt()
    {
        var hasher = new AspNetPasswordHasher();
        var first = hasher.Hash("secret-pass");
        var second = hasher.Hash("secret-pass");
        Assert.NotEqual(first, second);
    }
}
