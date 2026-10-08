using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Identity;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Infrastructure.Tests;

public class CatalogMigrationTests
{
    [Fact]
    public async Task Migrate_FromAuthentication_KeepsUsers_AndAddsCatalogTables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"petrotrans-mig-{Guid.NewGuid():N}.db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(SqlitePaths.GetConnectionString(path))
                .Options;
            await using (var db = new AppDbContext(options))
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
                await migrator.MigrateAsync("Authentication");
                await IdentityCatalogSeeder.SeedAsync(db);

                var hasher = new AspNetPasswordHasher();
                var ownerRole = await db.Roles.SingleAsync(x => x.Code == RoleCodes.OwnerManager);
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    UserName = "father",
                    UserNameNormalized = "FATHER",
                    PasswordHash = hasher.Hash("father1"),
                    DisplayName = "الأب",
                    Locale = "ar",
                    Theme = "light",
                    IsActive = true
                };
                db.Users.Add(user);
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = ownerRole.Id });
                await db.SaveChangesAsync();

                await migrator.MigrateAsync();
                await PetroTrans.Infrastructure.Catalog.NumberSeriesSeeder.SeedAsync(db);

                Assert.True(await db.Users.AnyAsync(x => x.UserName == "father"));
                Assert.True(await db.Roles.AnyAsync(x => x.Code == RoleCodes.OwnerManager));
                var tables = await TableNamesAsync(path);
                Assert.Contains("customers", tables);
                Assert.Contains("customer_types", tables);
                Assert.Contains("products", tables);
                Assert.Contains("product_variants", tables);
                Assert.Contains("product_media", tables);
                Assert.Contains("number_series", tables);
                Assert.Contains("users", tables);
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

    private static async Task<List<string>> TableNamesAsync(string path)
    {
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

        return names;
    }
}
