using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Migration;

internal static class ArLink
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<int> RunAsync(string repo)
    {
        var matchesPath = Path.Combine(repo, "artifacts", "migration", "ar-link-matches.json");
        var salesPath = Path.Combine(repo, "artifacts", "migration", "missing-sales-extract.json");
        var reportPath = Path.Combine(repo, "artifacts", "migration", "ar-link-report.json");
        if (!File.Exists(matchesPath))
        {
            Console.Error.WriteLine($"Missing {matchesPath}");
            return 1;
        }

        if (!File.Exists(salesPath))
        {
            Console.Error.WriteLine($"Missing {salesPath}");
            return 1;
        }

        var matches = JsonSerializer.Deserialize<MatchFile>(await File.ReadAllTextAsync(matchesPath), Json)
            ?? throw new InvalidOperationException("ar-link-matches.json could not be parsed.");
        var sales = JsonSerializer.Deserialize<SalesExtract>(await File.ReadAllTextAsync(salesPath), Json)
            ?? throw new InvalidOperationException("missing-sales-extract.json could not be parsed.");

        var dbPath = SqlitePaths.GetDefaultDatabasePath();
        var backupDir = Path.Combine(repo, "artifacts", "migration", "backups");
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, $"petrotrans-pre-ar-link-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        Console.WriteLine($"Live database: {dbPath}");
        Console.WriteLine($"Creating backup: {backupPath}");
        Checkpoint(dbPath);
        File.Copy(dbPath, backupPath, overwrite: false);
        var backupInfo = new FileInfo(backupPath);
        if (!backupInfo.Exists || backupInfo.Length == 0)
        {
            Console.Error.WriteLine("BACKUP FAILED — AR link aborted.");
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
            Console.Error.WriteLine("No active user. AR link aborted after backup.");
            return 1;
        }

        var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var salesSvc = scope.ServiceProvider.GetRequiredService<ISalesInvoiceService>();
        var ledger = scope.ServiceProvider.GetRequiredService<IPartyLedgerService>();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var methods = scope.ServiceProvider.GetRequiredService<IPaymentMethodService>();

        var method = (await methods.ListAsync(true)).FirstOrDefault();
        if (method is null)
        {
            Console.Error.WriteLine("No active payment method.");
            return 1;
        }

        var warehouseId = await db.Warehouses.AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Select(x => x.Id)
            .FirstAsync();

        var report = new ArLinkReport
        {
            BackupPath = backupPath,
            BackupBytes = backupInfo.Length,
            ActingAs = owner.UserName
        };

        var bookBefore = await treasury.GetBookAsync();
        report.TreasuryBefore = SnapshotBook(bookBefore);
        report.StockBefore = (await inventory.ListOnHandAsync(null)).Sum(x => x.OnHand);
        report.BalancesBefore = await CaptureBalancesAsync(customers);
        report.LiveInvoicesPreservedBefore = await CaptureLiveInvoicesAsync(db);
        report.HistoricalPriceHashBefore = await HashHistoricalPricesAsync(db);

        foreach (var row in matches.Rows)
        {
            if (row.MatchConfidence is "do-not-apply")
            {
                report.Skipped.Add($"{row.DbCustomer} {row.Date} {row.Amount}: {row.Action}");
                continue;
            }

            if (row.MatchConfidence is null || !row.MatchConfidence.StartsWith("certain", StringComparison.Ordinal))
            {
                report.UnresolvedDeposits.Add(Describe(row));
                continue;
            }

            if (!Guid.TryParse(row.TreasuryId, out var treasuryId))
            {
                report.Failures.Add($"{row.DbCustomer} {row.Date}: invalid treasury id");
                continue;
            }

            var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Name == row.DbCustomer);
            if (customer is null)
            {
                report.Failures.Add($"{row.DbCustomer}: customer not found");
                continue;
            }

            var note = $"AR-LINK excel:{row.Date} {row.DbCustomer} treasury:{treasuryId}";
            var already = await db.Payments.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(note));
            if (already)
            {
                report.PaymentsSkippedExisting++;
                continue;
            }

            var created = await payments.CreateAsync(
                new SavePaymentRequest(
                    null,
                    customer.Id,
                    method.Id,
                    row.Amount,
                    DateTime.Parse(row.Date),
                    $"excel-deposit:{row.Date}",
                    note,
                    treasuryId),
                owner.Id);
            if (!created.Succeeded)
            {
                report.Failures.Add($"{row.DbCustomer} {row.Date} {row.Amount}: {created.Error}");
                continue;
            }

            report.PaymentsCreated++;
            report.MatchedAmount += row.Amount;
            report.Allocated.Add($"{row.DbCustomer} {row.Date} {row.Amount} -> {treasuryId}");
        }

        foreach (var sale in sales.Targets)
        {
            if (sale.Date == "2026-08-09" && sale.Customer == "اسامه سلامه ( عشما)")
            {
                await PostExistingDraftAsync(db, salesSvc, sale, owner.Id, report);
                continue;
            }

            await AddMissingSaleAsync(db, salesSvc, inventory, sale, warehouseId, owner.Id, report);
        }

        var bookAfter = await treasury.GetBookAsync();
        report.TreasuryAfter = SnapshotBook(bookAfter);
        report.TreasuryMovementsCreated = bookAfter.Entries.Count - bookBefore.Entries.Count;
        report.StockAfter = (await inventory.ListOnHandAsync(null)).Sum(x => x.OnHand);
        report.BalancesAfter = await CaptureBalancesAsync(customers);
        report.LiveInvoicesPreservedAfter = await CaptureLiveInvoicesAsync(db);
        report.HistoricalPriceHashAfter = await HashHistoricalPricesAsync(db);
        report.HistoricalInvoicePricesUnchanged = report.HistoricalPriceHashBefore == report.HistoricalPriceHashAfter;
        report.TreasuryUnchanged =
            report.TreasuryBefore.TotalIn == report.TreasuryAfter.TotalIn
            && report.TreasuryBefore.TotalOut == report.TreasuryAfter.TotalOut
            && report.TreasuryBefore.Balance == report.TreasuryAfter.Balance;

        foreach (var before in report.BalancesBefore)
        {
            var after = report.BalancesAfter.FirstOrDefault(x => x.Name == before.Name);
            if (after is null || after.Outstanding == before.Outstanding)
            {
                continue;
            }

            report.ChangedCustomers.Add(new ChangedBalance
            {
                Name = before.Name,
                Before = before.Outstanding,
                After = after.Outstanding,
                Delta = after.Outstanding - before.Outstanding
            });
        }

        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, Json));
        Console.WriteLine($"AR allocations created={report.PaymentsCreated} skipped-existing={report.PaymentsSkippedExisting} unresolved={report.UnresolvedDeposits.Count}");
        Console.WriteLine($"treasury created={report.TreasuryMovementsCreated} unchanged={report.TreasuryUnchanged} in={report.TreasuryAfter.TotalIn} out={report.TreasuryAfter.TotalOut} bal={report.TreasuryAfter.Balance}");
        Console.WriteLine($"INV-0023={report.Inv0023Status} late-sales added={report.SalesAdded} already-existed={report.SalesAlreadyExisted} blocked={report.SalesBlocked.Count}");
        Console.WriteLine($"report: {reportPath}");
        return report.Failures.Count == 0 && report.TreasuryUnchanged ? 0 : 2;
    }

    private static async Task PostExistingDraftAsync(
        AppDbContext db,
        ISalesInvoiceService sales,
        SaleTarget sale,
        Guid actor,
        ArLinkReport report)
    {
        var draft = await db.SalesInvoices.AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Number == "INV-0023");
        if (draft is null)
        {
            report.Inv0023Status = "MISSING — draft INV-0023 not found; did not create a new invoice";
            report.SalesBlocked.Add("INV-0023 not found");
            return;
        }

        if (draft.Status == "posted")
        {
            report.Inv0023Status = "already posted — left untouched";
            report.SalesAlreadyExisted++;
            return;
        }

        if (draft.Status != "draft")
        {
            report.Inv0023Status = $"unexpected status {draft.Status}";
            report.SalesBlocked.Add($"INV-0023 status {draft.Status}");
            return;
        }

        if (Math.Abs(draft.GoodsTotal - sale.ExcelTotal) > 0.01m)
        {
            report.Inv0023Status = $"draft total {draft.GoodsTotal} != excel {sale.ExcelTotal}; not posted";
            report.SalesBlocked.Add(report.Inv0023Status);
            return;
        }

        var posted = await sales.PostAsync(draft.Id, actor);
        if (!posted.Succeeded)
        {
            report.Inv0023Status = $"post failed: {posted.Error}";
            report.Failures.Add(report.Inv0023Status);
            return;
        }

        report.Inv0023Status = $"posted existing draft {posted.Value!.Number} total={posted.Value.GoodsTotal}";
        report.SalesAdded++;
        report.SalesActions.Add(report.Inv0023Status);
    }

    private static async Task AddMissingSaleAsync(
        AppDbContext db,
        ISalesInvoiceService sales,
        IInventoryService inventory,
        SaleTarget sale,
        Guid warehouseId,
        Guid actor,
        ArLinkReport report)
    {
        if (sale.AlreadyPosted)
        {
            report.SalesAlreadyExisted++;
            report.SalesActions.Add($"{sale.Customer} {sale.Date} already posted — not duplicated");
            return;
        }

        if (sale.ExistingDraft is not null)
        {
            report.SalesBlocked.Add($"{sale.Customer} {sale.Date} exists as draft {sale.ExistingDraft.Number}; not creating another invoice");
            return;
        }

        if (sale.CustomerId is null || sale.CustomerId == Guid.Empty)
        {
            report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: customer id missing");
            return;
        }

        if (sale.Lines.Any(x => x.VariantId is null || x.Match != "certain"))
        {
            report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: unmatched product lines — not posted");
            return;
        }

        var needed = new Dictionary<Guid, decimal>();
        foreach (var line in sale.Lines)
        {
            var id = line.VariantId!.Value;
            needed[id] = needed.GetValueOrDefault(id) + line.QtyCartons;
        }

        foreach (var (variantId, qty) in needed)
        {
            var onHand = await inventory.GetOnHandAsync(warehouseId, variantId);
            if (onHand < qty)
            {
                report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: stock blocked on-hand={onHand} needed={qty}");
                return;
            }
        }

        var note = $"AR-SALE excel:{sale.Date} {sale.Customer}";
        if (await db.SalesInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(note)))
        {
            report.SalesAlreadyExisted++;
            return;
        }

        var lines = sale.Lines.Select(l => new SaveInvoiceLineRequest(
            l.VariantId!.Value,
            l.QtyCartons,
            l.UnitPrice,
            l.UnitPrice is { } price && l.CurrentSell is { } current && Math.Abs(price - current) > 0.01m
                ? "سعر تاريخي من دفتر العميل — بدون تعديل أسعار الفواتير السابقة"
                : null)).ToList();

        var created = await sales.CreateAsync(
            new SaveInvoiceRequest(
                sale.CustomerId.Value,
                warehouseId,
                DateTime.Parse(sale.Date),
                null,
                note,
                0m,
                null,
                null,
                lines),
            actor,
            canOverridePrice: true);
        if (!created.Succeeded)
        {
            report.Failures.Add($"{sale.Customer} {sale.Date} create: {created.Error}");
            return;
        }

        if (Math.Abs((created.Value!.GoodsTotal) - sale.ExcelTotal) > 0.01m)
        {
            report.Failures.Add($"{sale.Customer} {sale.Date}: created total {created.Value.GoodsTotal} != excel {sale.ExcelTotal}; left as draft, not posted");
            return;
        }

        var posted = await sales.PostAsync(created.Value.Id, actor);
        if (!posted.Succeeded)
        {
            report.Failures.Add($"{sale.Customer} {sale.Date} post: {posted.Error}");
            return;
        }

        report.SalesAdded++;
        report.SalesActions.Add($"added {posted.Value!.Number} {sale.Customer} {sale.Date} {posted.Value.GoodsTotal}");
    }

    private static async Task<List<BalanceRow>> CaptureBalancesAsync(ICustomerService customers)
        => (await customers.ListAsync(null, true))
            .Select(x => new BalanceRow { Name = x.Name, Outstanding = x.Outstanding })
            .OrderBy(x => x.Name)
            .ToList();

    private static async Task<List<string>> CaptureLiveInvoicesAsync(AppDbContext db)
        => await db.SalesInvoices.AsNoTracking()
            .Where(x => x.Number == "INV-0038" || x.Number == "INV-0039" || x.Number == "INV-0040")
            .OrderBy(x => x.Number)
            .Select(x => $"{x.Number} {x.Status} {x.GoodsTotal} {x.InvoiceDate:yyyy-MM-dd}")
            .ToListAsync();

    private static async Task<string> HashHistoricalPricesAsync(AppDbContext db)
    {
        var rows = await db.SalesInvoiceLines.AsNoTracking()
            .Where(x => x.Invoice.Number != null
                        && x.Invoice.Number.CompareTo("INV-0038") < 0
                        && x.Invoice.Number != "INV-0023")
            .OrderBy(x => x.Id)
            .Select(x => x.Id.ToString() + ":" + ((x.ResolvedUnitPrice ?? x.UnitPrice) ?? 0m))
            .ToListAsync();
        return string.Join("|", rows);
    }

    private static BookSnap SnapshotBook(TreasuryBookDto book)
        => new()
        {
            Count = book.Entries.Count,
            TotalIn = book.TotalIn,
            TotalOut = book.TotalOut,
            Balance = book.Balance
        };

    private static string Describe(MatchRow row)
        => $"{row.DbCustomer} {row.Date} {row.Amount} confidence={row.MatchConfidence} {row.Action}";

    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(SqlitePaths.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
    }

    private sealed class MatchFile
    {
        public List<MatchRow> Rows { get; set; } = [];
    }

    private sealed class MatchRow
    {
        public string DbCustomer { get; set; } = "";
        public string Date { get; set; } = "";
        public decimal Amount { get; set; }
        public string? TreasuryId { get; set; }
        public string? MatchConfidence { get; set; }
        public string? Action { get; set; }
    }

    private sealed class SalesExtract
    {
        public List<SaleTarget> Targets { get; set; } = [];
    }

    private sealed class SaleTarget
    {
        public string Customer { get; set; } = "";
        public Guid? CustomerId { get; set; }
        public string Date { get; set; } = "";
        public decimal ExcelTotal { get; set; }
        public bool AlreadyPosted { get; set; }
        public ExistingDraft? ExistingDraft { get; set; }
        public List<SaleLine> Lines { get; set; } = [];
    }

    private sealed class ExistingDraft
    {
        public string? Number { get; set; }
    }

    private sealed class SaleLine
    {
        public Guid? VariantId { get; set; }
        public string? Match { get; set; }
        public decimal QtyCartons { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? CurrentSell { get; set; }
    }

    private sealed class ArLinkReport
    {
        public string BackupPath { get; set; } = "";
        public long BackupBytes { get; set; }
        public string ActingAs { get; set; } = "";
        public BookSnap TreasuryBefore { get; set; } = new();
        public BookSnap TreasuryAfter { get; set; } = new();
        public bool TreasuryUnchanged { get; set; }
        public int TreasuryMovementsCreated { get; set; }
        public int PaymentsCreated { get; set; }
        public int PaymentsSkippedExisting { get; set; }
        public decimal MatchedAmount { get; set; }
        public decimal StockBefore { get; set; }
        public decimal StockAfter { get; set; }
        public string Inv0023Status { get; set; } = "";
        public int SalesAdded { get; set; }
        public int SalesAlreadyExisted { get; set; }
        public bool HistoricalInvoicePricesUnchanged { get; set; }
        public string HistoricalPriceHashBefore { get; set; } = "";
        public string HistoricalPriceHashAfter { get; set; } = "";
        public List<string> Allocated { get; set; } = [];
        public List<string> UnresolvedDeposits { get; set; } = [];
        public List<string> Skipped { get; set; } = [];
        public List<string> SalesActions { get; set; } = [];
        public List<string> SalesBlocked { get; set; } = [];
        public List<string> Failures { get; set; } = [];
        public List<BalanceRow> BalancesBefore { get; set; } = [];
        public List<BalanceRow> BalancesAfter { get; set; } = [];
        public List<ChangedBalance> ChangedCustomers { get; set; } = [];
        public List<string> LiveInvoicesPreservedBefore { get; set; } = [];
        public List<string> LiveInvoicesPreservedAfter { get; set; } = [];
    }

    private sealed class BookSnap
    {
        public int Count { get; set; }
        public decimal TotalIn { get; set; }
        public decimal TotalOut { get; set; }
        public decimal Balance { get; set; }
    }

    private sealed class BalanceRow
    {
        public string Name { get; set; } = "";
        public decimal Outstanding { get; set; }
    }

    private sealed class ChangedBalance
    {
        public string Name { get; set; } = "";
        public decimal Before { get; set; }
        public decimal After { get; set; }
        public decimal Delta { get; set; }
    }
}
