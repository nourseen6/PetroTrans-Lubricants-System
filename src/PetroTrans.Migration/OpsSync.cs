using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Catalog;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Migration;

internal static class OpsSync
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string repo)
    {
        var treasuryPath = Path.Combine(repo, "artifacts", "migration", "treasury-preview.json");
        var pricesPath = Path.Combine(repo, "artifacts", "migration", "price-lists.json");
        var reportPath = Path.Combine(repo, "artifacts", "migration", "ops-sync-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

        if (!File.Exists(treasuryPath))
        {
            Console.Error.WriteLine($"Missing treasury preview: {treasuryPath}");
            return 1;
        }

        if (!File.Exists(pricesPath))
        {
            Console.Error.WriteLine($"Missing price lists: {pricesPath}");
            return 1;
        }

        var dbPath = SqlitePaths.GetDefaultDatabasePath();
        var backupDir = Path.Combine(repo, "artifacts", "migration", "backups");
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, $"petrotrans-pre-ops-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        Console.WriteLine($"Live database: {dbPath}");
        Console.WriteLine($"Creating backup: {backupPath}");
        Checkpoint(dbPath);
        File.Copy(dbPath, backupPath, overwrite: false);
        var backupInfo = new FileInfo(backupPath);
        if (!backupInfo.Exists || backupInfo.Length == 0)
        {
            Console.Error.WriteLine("BACKUP FAILED — ops sync aborted.");
            return 1;
        }

        Console.WriteLine($"BACKUP CONFIRMED exists={backupInfo.Exists} bytes={backupInfo.Length}");

        var services = new ServiceCollection();
        services.AddInfrastructure(SqlitePaths.GetConnectionString(dbPath), dbPath);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.InitializeAsync(db);

        var owner = await db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .Where(x => x.IsActive)
            .FirstOrDefaultAsync(x => x.UserRoles.Any(r => r.Role.Code == RoleCodes.OwnerManager));
        owner ??= await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.IsActive);
        if (owner is null)
        {
            Console.Error.WriteLine("No active user in the live database. Ops sync aborted after backup.");
            return 1;
        }

        Console.WriteLine($"Acting as {owner.DisplayName} ({owner.UserName})");

        var treasuryPreview = JsonSerializer.Deserialize<TreasuryPreview>(
            await File.ReadAllTextAsync(treasuryPath), Json)
            ?? throw new InvalidOperationException("treasury-preview.json could not be parsed.");
        var priceLists = JsonSerializer.Deserialize<PriceLists>(
            await File.ReadAllTextAsync(pricesPath), Json)
            ?? throw new InvalidOperationException("price-lists.json could not be parsed.");

        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
        var pricing = scope.ServiceProvider.GetRequiredService<IPricingService>();
        var report = new OpsSyncReport
        {
            BackupPath = backupPath,
            BackupBytes = backupInfo.Length,
            SourceTreasury = treasuryPreview.Source
        };

        var postedBefore = await db.SalesInvoices.AsNoTracking().CountAsync(x => x.Status == "posted");
        var linePriceHashBefore = await HashInvoiceLinePricesAsync(db);

        foreach (var row in treasuryPreview.Entries)
        {
            var existed = !string.IsNullOrWhiteSpace(row.Notes)
                && await db.TreasuryEntries.AnyAsync(x => x.Notes == row.Notes);
            var result = await treasury.CreateAsync(
                new SaveTreasuryEntryRequest(
                    DateTime.Parse(row.OccurredOn),
                    row.Direction,
                    row.Category,
                    row.Description,
                    row.Amount,
                    row.Notes),
                owner.Id);
            if (!result.Succeeded)
            {
                report.Failures.Add($"{row.LegacyKey}: {result.Error}");
                continue;
            }

            if (existed)
            {
                report.TreasurySkippedExisting++;
            }
            else
            {
                report.TreasuryImported++;
            }
        }

        var variants = await db.ProductVariants.AsNoTracking()
            .Include(x => x.Product)
            .Where(x => x.DeletedAt == null)
            .ToListAsync();

        ApplyLayer(priceLists.CustomerSell, variants, "sell", report);
        ApplyLayer(priceLists.CompanyBuy, variants, "buy", report);

        foreach (var change in report.PriceUpdates)
        {
            if (change.Layer == "sell")
            {
                var result = await pricing.UpdateBasePriceAsync(change.VariantId, change.NewPrice, change.Reason, owner.Id);
                if (!result.Succeeded)
                {
                    report.Failures.Add($"{change.Product} {change.Pack}: {result.Error}");
                }
            }
            else
            {
                var result = await pricing.UpdatePurchasePriceAsync(change.VariantId, change.NewPrice, change.Reason, owner.Id);
                if (!result.Succeeded)
                {
                    report.Failures.Add($"{change.Product} {change.Pack}: {result.Error}");
                }
            }
        }

        var postedAfter = await db.SalesInvoices.AsNoTracking().CountAsync(x => x.Status == "posted");
        var linePriceHashAfter = await HashInvoiceLinePricesAsync(db);
        report.PostedInvoiceCount = postedAfter;
        report.HistoricalInvoicePricesUnchanged = postedBefore == postedAfter && linePriceHashBefore == linePriceHashAfter;

        var book = await treasury.GetBookAsync();
        report.TreasuryCount = book.Entries.Count;
        report.TreasuryIn = book.TotalIn;
        report.TreasuryOut = book.TotalOut;
        report.TreasuryBalance = book.Balance;

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, Json));
        Console.WriteLine($"treasury imported={report.TreasuryImported} skipped-existing={report.TreasurySkippedExisting} book={report.TreasuryCount} in={report.TreasuryIn} out={report.TreasuryOut} bal={report.TreasuryBalance}");
        Console.WriteLine($"prices sell={report.PriceUpdates.Count(x => x.Layer == "sell")} buy={report.PriceUpdates.Count(x => x.Layer == "buy")} unmatched={report.UnmatchedPrices.Count} ambiguous={report.AmbiguousPrices.Count}");
        Console.WriteLine($"historical invoice prices unchanged={report.HistoricalInvoicePricesUnchanged}");
        Console.WriteLine($"report: {reportPath}");
        return report.Failures.Count == 0 ? 0 : 2;
    }

    private static void ApplyLayer(
        IReadOnlyList<PriceListRow> rows,
        IReadOnlyList<ProductVariant> variants,
        string layer,
        OpsSyncReport report)
    {
        foreach (var row in rows)
        {
            var hits = variants.Where(v => Matches(v, row)).ToList();
            if (hits.Count == 0)
            {
                report.UnmatchedPrices.Add($"{layer} {string.Join("+", row.Keys)} {row.Pack} = {row.Price}");
                continue;
            }

            if (hits.Count > 1)
            {
                report.AmbiguousPrices.Add($"{layer} {string.Join("+", row.Keys)} {row.Pack} -> {hits.Count} variants");
                continue;
            }

            var variant = hits[0];
            var current = layer == "sell" ? variant.StandardWholesalePrice : variant.StandardPurchasePrice;
            if (current == row.Price)
            {
                report.PricesAlreadyCurrent++;
                continue;
            }

            report.PriceUpdates.Add(new PriceUpdate
            {
                Layer = layer,
                VariantId = variant.Id,
                Product = variant.Product.Name,
                Pack = $"{variant.PackagingType} {variant.PackagingSize}",
                OldPrice = current,
                NewPrice = row.Price,
                Reason = layer == "sell"
                    ? "ورقة أسعار العملاء N"
                    : "قائمة أدنوك سعر التاجر 01/07/2026"
            });
        }
    }

    private static bool Matches(ProductVariant variant, PriceListRow row)
    {
        var blob = Normalize($"{variant.Product.Name} {variant.Product.Brand} {variant.Product.Specification} {variant.Product.Category}");
        if (row.Keys.Any(key => !blob.Contains(Normalize(key))))
        {
            return false;
        }

        var pack = Normalize($"{variant.PackagingType} {variant.PackagingSize}");
        return PackNeedles(row.Pack).Any(pack.Contains);
    }

    private static string Normalize(string value)
    {
        var text = (value ?? string.Empty)
            .Replace("أ", "ا")
            .Replace("إ", "ا")
            .Replace("آ", "ا")
            .Replace("ة", "ه")
            .Replace("ى", "ي")
            .Replace("ـ", "")
            .ToLowerInvariant();
        text = text.Replace("×", "x").Replace("*", "x").Replace("х", "x");
        text = Regex.Replace(text, @"[\s_\-./\\]+", "");
        return text;
    }

    private static IReadOnlyList<string> PackNeedles(string pack)
    {
        var key = Normalize(pack);
        return key switch
        {
            "12x1" => ["12x1", "1x12", "1لترx12"],
            "3x4" => ["3x4", "4x3", "4لترx3"],
            "6x4" => ["6x4", "4x6", "4لترx6"],
            "4x5" => ["4x5", "5x4", "5لترx4"],
            "3x5" => ["3x5", "5x3", "5لترx3"],
            "6x5" => ["6x5", "5x6", "5لترx6"],
            "20l" => ["20لتر", "20l", "جركن20لتر"],
            "16l" => ["16لتر", "16l", "جركن16لتر"],
            "15kg" => ["15كجم", "15kg", "15كيلو", "15كغ", "15كx1", "15ك"],
            "24x05" => ["24x05", "05x24", "05لترx24", "5لترx24", "24x0.5"],
            _ => [key]
        };
    }

    private static async Task<string> HashInvoiceLinePricesAsync(AppDbContext db)
    {
        var rows = await db.SalesInvoiceLines.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.Id.ToString() + ":" + ((x.ResolvedUnitPrice ?? x.UnitPrice) ?? 0m))
            .ToListAsync();
        return string.Join("|", rows);
    }

    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(SqlitePaths.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
    }

    private sealed class TreasuryPreview
    {
        public string Source { get; set; } = "";
        public List<TreasuryPreviewRow> Entries { get; set; } = [];
    }

    private sealed class TreasuryPreviewRow
    {
        public string LegacyKey { get; set; } = "";
        public string OccurredOn { get; set; } = "";
        public string Direction { get; set; } = "";
        public string? Category { get; set; }
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class PriceLists
    {
        public List<PriceListRow> CustomerSell { get; set; } = [];
        public List<PriceListRow> CompanyBuy { get; set; } = [];
    }

    private sealed class PriceListRow
    {
        public List<string> Keys { get; set; } = [];
        public string Pack { get; set; } = "";
        public decimal Price { get; set; }
    }

    private sealed class OpsSyncReport
    {
        public string BackupPath { get; set; } = "";
        public long BackupBytes { get; set; }
        public string? SourceTreasury { get; set; }
        public int TreasuryImported { get; set; }
        public int TreasurySkippedExisting { get; set; }
        public int TreasuryCount { get; set; }
        public decimal TreasuryIn { get; set; }
        public decimal TreasuryOut { get; set; }
        public decimal TreasuryBalance { get; set; }
        public int PricesAlreadyCurrent { get; set; }
        public int PostedInvoiceCount { get; set; }
        public bool HistoricalInvoicePricesUnchanged { get; set; }
        public List<PriceUpdate> PriceUpdates { get; set; } = [];
        public List<string> UnmatchedPrices { get; set; } = [];
        public List<string> AmbiguousPrices { get; set; } = [];
        public List<string> Failures { get; set; } = [];
    }

    private sealed class PriceUpdate
    {
        public string Layer { get; set; } = "";
        public Guid VariantId { get; set; }
        public string Product { get; set; } = "";
        public string Pack { get; set; } = "";
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public string Reason { get; set; } = "";
    }
}
