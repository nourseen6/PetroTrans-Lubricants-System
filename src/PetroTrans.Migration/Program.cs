using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Migration;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var repo = FindRepoRoot() ?? throw new InvalidOperationException("Could not find PetroTrans.sln");
        if (args.Length > 0 && string.Equals(args[0], "--ops", StringComparison.OrdinalIgnoreCase))
        {
            return await OpsSync.RunAsync(repo);
        }

        if (args.Length > 0 && string.Equals(args[0], "--ar-link", StringComparison.OrdinalIgnoreCase))
        {
            return await ArLink.RunAsync(repo);
        }

        if (args.Length > 0 && string.Equals(args[0], "--excel-gap", StringComparison.OrdinalIgnoreCase))
        {
            return await ExcelGap.RunAsync(repo);
        }

        if (args.Length > 0 && string.Equals(args[0], "--adnoc-totals", StringComparison.OrdinalIgnoreCase))
        {
            return await AdnocTotals.RunAsync(repo);
        }

        if (args.Length > 0 && string.Equals(args[0], "--migrate", StringComparison.OrdinalIgnoreCase))
        {
            var migratePath = SqlitePaths.GetDefaultDatabasePath();
            var migrateServices = new ServiceCollection();
            migrateServices.AddInfrastructure(SqlitePaths.GetConnectionString(migratePath), migratePath);
            await using var migrateProvider = migrateServices.BuildServiceProvider();
            await using var migrateScope = migrateProvider.CreateAsyncScope();
            var migrateDb = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DatabaseInitializer.InitializeAsync(migrateDb);
            Console.WriteLine($"Migrated {migratePath}");
            return 0;
        }

        var previewPath = args.Length > 0
            ? (Path.IsPathRooted(args[0]) ? args[0] : Path.Combine(repo, args[0]))
            : Path.Combine(repo, "artifacts", "migration", "preview.json");
        var reportName = args.Length > 1 ? Path.GetFileName(args[1]) : "import-report.json";
        if (!File.Exists(previewPath))
        {
            Console.Error.WriteLine($"Missing preview dataset: {previewPath}");
            return 1;
        }

        var preview = JsonSerializer.Deserialize<PreviewRoot>(
            await File.ReadAllTextAsync(previewPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("preview.json could not be parsed.");

        var dbPath = SqlitePaths.GetDefaultDatabasePath();
        var backupDir = Path.Combine(repo, "artifacts", "migration", "backups");
        Directory.CreateDirectory(backupDir);
        var backupStamp = reportName.Contains("delta", StringComparison.OrdinalIgnoreCase)
            ? "pre-delta"
            : "pre-migration";
        var backupPath = Path.Combine(backupDir, $"petrotrans-{backupStamp}-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        Console.WriteLine($"Live database: {dbPath}");
        Console.WriteLine($"Creating backup: {backupPath}");
        Checkpoint(dbPath);
        File.Copy(dbPath, backupPath, overwrite: false);
        var backupInfo = new FileInfo(backupPath);
        if (!backupInfo.Exists || backupInfo.Length == 0)
        {
            Console.Error.WriteLine("BACKUP FAILED — import aborted.");
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
            Console.Error.WriteLine("No active user in the live database. Import aborted after backup.");
            return 1;
        }

        Console.WriteLine($"Acting as {owner.DisplayName} ({owner.UserName}) for historical price override.");

        var importer = ActivatorUtilities.CreateInstance<MigrationImporter>(scope.ServiceProvider);
        ImportResult imported;
        try
        {
            imported = await importer.ImportAsync(preview, owner.Id, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IMPORT STOPPED: {ex.Message}");
            await WriteReportAsync(repo, reportName, backupPath, backupInfo.Length, preview, null, null, fatal: ex.Message);
            return 1;
        }

        var snapshot = await importer.CaptureAsync(preview, CancellationToken.None);
        var recon = BuildRecon(preview, snapshot);
        await WriteReportAsync(repo, reportName, backupPath, backupInfo.Length, preview, imported, recon, fatal: null);

        Console.WriteLine();
        Console.WriteLine($"imported customers={imported.Counters.CustomersCreated} invoices={imported.Counters.InvoicesPosted} payments={imported.Counters.PaymentsCreated} receipts={imported.Counters.ReceiptsPosted} bills={imported.Counters.BillsPosted}");
        Console.WriteLine($"skipped payments={imported.Counters.PaymentsSkipped} failures={imported.Failures.Count}");
        Console.WriteLine($"recon mismatches={recon.Mismatches.Count}");
        Console.WriteLine($"report: {Path.Combine(repo, "artifacts", "migration", reportName)}");
        return imported.Failures.Count == 0 ? 0 : 2;
    }

    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(SqlitePaths.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
    }

    private static ReconReport BuildRecon(PreviewRoot preview, ReconSnapshot system)
    {
        var report = new ReconReport();
        var excelSales = preview.SalesInvoices.Sum(x => x.GoodsTotal);
        var excelPaymentsAll = preview.Payments.Sum(x => x.Amount);
        var excelPaymentsImportable = preview.Payments.Where(x => x.ImportableUnderCurrentRules).Sum(x => x.Amount);

        AddCount(report, "customers", preview.Customers.Count, system.CustomerCount);
        AddCount(report, "product variants", preview.Products.Count, system.VariantCount);
        AddCount(report, "sales invoices posted", preview.SalesInvoices.Count, system.PostedSalesCount);
        AddCount(report, "sales invoice lines", preview.SalesInvoices.Sum(x => x.Lines.Count), system.SalesLineCount);
        AddMoney(report, "sales goods total", excelSales, system.SalesGoodsTotal, "Reconstructed invoices vs posted sales_invoices.goods_total");
        AddCount(report, "customer payments imported", preview.Payments.Count(x => x.ImportableUnderCurrentRules), system.PaymentCount);
        AddMoney(report, "customer payments imported amount", excelPaymentsImportable, system.PaymentTotal, "Same-day importable column A vs payments.amount");
        AddMoney(report, "customer payments all Excel column A", excelPaymentsAll, system.PaymentTotal, "All Excel deposits vs imported payments. Blocked receipts are expected to be missing.");
        AddCount(report, "ADNOC goods receipts", preview.AdnocReceipts.Count, system.PostedReceiptCount);
        AddCount(report, "ADNOC purchase invoices", preview.AdnocBills.Count, system.PostedBillCount);
        AddMoney(report, "ADNOC payable / purchases", preview.AdnocBalance.CalculatedPayableAtCutoff, system.AdnocPayable, "T.E inbound cost vs supplier ledger");
        AddMoney(report, "ADNOC payments", preview.AdnocBalance.AdnocPaymentsInWindow, 0, "None in window; current Payment model cannot store supplier payments");
        AddQty(report, "inventory closing total", preview.Inventory.ExcelClosingFromJard, system.Stock.Sum(x => x.OnHand), "Excel جرد شهري vs inventory_balances.on_hand");
        AddQty(report, "inventory calculated closing", preview.Inventory.CalculatedClosing, system.Stock.Sum(x => x.OnHand), "Replay ADNOC in − sales out vs system on-hand");

        foreach (var customer in preview.Customers)
        {
            var row = system.Customers.FirstOrDefault(x => x.Name == customer.FullName);
            var systemValue = row?.SystemBalance;
            report.CustomerBalances.Add(new ReconLine
            {
                Entity = $"AR {customer.Nickname} / {customer.FullName}",
                ExcelValue = customer.ExcelSnap15,
                SystemValue = systemValue,
                Difference = systemValue is null || customer.ExcelSnap15 is null ? null : decimal.Round(systemValue.Value - customer.ExcelSnap15.Value, 2),
                Source = "Typed 15 Aug snapshot vs party_ledger outstanding"
            });
            if (customer.ExcelSnap15 is { } snap && systemValue is { } sys && snap != sys)
            {
                report.Mismatches.Add(report.CustomerBalances[^1]);
            }

            report.CustomerReplay.Add(new ReconLine
            {
                Entity = $"AR replay {customer.Nickname}",
                ExcelValue = customer.CalculatedBalance,
                SystemValue = systemValue,
                Difference = systemValue is null ? null : decimal.Round(systemValue.Value - customer.CalculatedBalance, 2),
                Source = "Excel qty×price − column A vs system ledger (blocked payments stay out)"
            });
        }

        foreach (var variant in preview.Inventory.ByVariant)
        {
            var pack = PackSize(variant.VariantKey);
            var name = ProductName(variant.VariantKey);
            var row = system.Stock.FirstOrDefault(x =>
                x.ProductName == name && x.PackagingSize == pack);
            var onHand = row?.OnHand ?? 0m;
            var vsExcel = new ReconLine
            {
                Entity = $"stock {variant.VariantKey}",
                ExcelValue = variant.ExcelClosingFromJard,
                SystemValue = onHand,
                Difference = decimal.Round(onHand - variant.ExcelClosingFromJard, 4),
                Source = "جرد شهري vs inventory_balances"
            };
            report.Inventory.Add(vsExcel);
            if (vsExcel.Difference != 0)
            {
                report.Mismatches.Add(vsExcel);
            }

            if (onHand != variant.CalculatedClosing)
            {
                report.Mismatches.Add(new ReconLine
                {
                    Entity = $"stock calc {variant.VariantKey}",
                    ExcelValue = variant.CalculatedClosing,
                    SystemValue = onHand,
                    Difference = decimal.Round(onHand - variant.CalculatedClosing, 4),
                    Source = "Replay calculated closing vs system on-hand"
                });
            }
        }

        report.MatchingBalances = report.CustomerBalances.Count(x => x.Difference is 0);
        report.CustomerCount = system.CustomerCount;
        report.ProductCount = system.ProductCount;
        report.VariantCount = system.VariantCount;
        report.PostedSalesCount = system.PostedSalesCount;
        report.DraftSalesCount = system.DraftSalesCount;
        report.SalesLineCount = system.SalesLineCount;
        report.PaymentCount = system.PaymentCount;
        report.ReceiptCount = system.ReceiptCount;
        report.BillCount = system.BillCount;
        report.MovementCount = system.MovementCount;
        report.WarehouseCount = system.WarehouseCount;
        report.SupplierCount = system.SupplierCount;
        report.PaymentMethodCount = system.PaymentMethodCount;
        return report;
    }

    private static void AddCount(ReconReport report, string entity, int excel, int system)
    {
        var line = new ReconLine
        {
            Entity = entity,
            ExcelValue = excel,
            SystemValue = system,
            Difference = system - excel,
            Source = "Preview count vs live database"
        };
        report.Counts.Add(line);
        if (line.Difference != 0)
        {
            report.Mismatches.Add(line);
        }
    }

    private static void AddMoney(ReconReport report, string entity, decimal excel, decimal system, string source)
    {
        var line = new ReconLine
        {
            Entity = entity,
            ExcelValue = decimal.Round(excel, 2),
            SystemValue = decimal.Round(system, 2),
            Difference = decimal.Round(system - excel, 2),
            Source = source
        };
        report.Totals.Add(line);
        if (line.Difference != 0)
        {
            report.Mismatches.Add(line);
        }
    }

    private static void AddQty(ReconReport report, string entity, decimal excel, decimal system, string source)
    {
        var line = new ReconLine
        {
            Entity = entity,
            ExcelValue = excel,
            SystemValue = system,
            Difference = decimal.Round(system - excel, 4),
            Source = source
        };
        report.Totals.Add(line);
        if (line.Difference != 0)
        {
            report.Mismatches.Add(line);
        }
    }

    private static string ProductName(string variantKey)
    {
        var parts = variantKey.Split(" | ");
        return parts.Length >= 2 ? $"{parts[1]} {parts[0]}".Trim() : variantKey;
    }

    private static string PackSize(string variantKey)
    {
        var parts = variantKey.Split(" | ");
        if (parts.Length < 4)
        {
            return variantKey;
        }

        return $"{parts[2]} × {parts[3]}";
    }

    private static async Task WriteReportAsync(
        string repo,
        string reportName,
        string backupPath,
        long backupBytes,
        PreviewRoot preview,
        ImportResult? imported,
        ReconReport? recon,
        string? fatal)
    {
        var payload = new
        {
            generatedAt = DateTime.Now.ToString("s"),
            backup = new { path = backupPath, exists = File.Exists(backupPath), bytes = backupBytes },
            excelUntouched = true,
            fatal,
            imported = imported is null ? null : new
            {
                counters = imported.Counters,
                failures = imported.Failures,
                unresolved = imported.Unresolved,
                events = imported.Events
            },
            records = recon is null ? null : new
            {
                recon.CustomerCount,
                recon.ProductCount,
                recon.VariantCount,
                recon.PostedSalesCount,
                recon.DraftSalesCount,
                recon.SalesLineCount,
                recon.PaymentCount,
                recon.ReceiptCount,
                recon.BillCount,
                recon.MovementCount,
                recon.WarehouseCount,
                recon.SupplierCount,
                recon.PaymentMethodCount
            },
            reconciliation = recon,
            previewUnresolved = preview.UnresolvedCustomerReferences
        };
        var path = Path.Combine(repo, "artifacts", "migration", reportName);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PetroTrans.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PetroTrans.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }
}

public sealed class ReconReport
{
    public int MatchingBalances { get; set; }
    public List<ReconLine> Counts { get; set; } = [];
    public List<ReconLine> Totals { get; set; } = [];
    public List<ReconLine> CustomerBalances { get; set; } = [];
    public List<ReconLine> CustomerReplay { get; set; } = [];
    public List<ReconLine> Inventory { get; set; } = [];
    public List<ReconLine> Mismatches { get; set; } = [];
    public int CustomerCount { get; set; }
    public int ProductCount { get; set; }
    public int VariantCount { get; set; }
    public int PostedSalesCount { get; set; }
    public int DraftSalesCount { get; set; }
    public int SalesLineCount { get; set; }
    public int PaymentCount { get; set; }
    public int ReceiptCount { get; set; }
    public int BillCount { get; set; }
    public int MovementCount { get; set; }
    public int WarehouseCount { get; set; }
    public int SupplierCount { get; set; }
    public int PaymentMethodCount { get; set; }
}

public sealed class ReconLine
{
    public string Entity { get; set; } = "";
    public decimal? ExcelValue { get; set; }
    public decimal? SystemValue { get; set; }
    public decimal? Difference { get; set; }
    public string Source { get; set; } = "";
}
