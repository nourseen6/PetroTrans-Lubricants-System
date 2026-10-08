using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Migration;

internal static class AdnocTotals
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true
    };

    private static readonly VerifiedBill[] Targets =
    [
        new("2026-07-09", "754920", 754920m),
        new("2026-07-11", "663782", 663782m),
        new("2026-07-26", "406498", 406498m),
        new("2026-08-21", "808364", 808364m),
        new("2026-08-24", "141300", 141300m),
        new("2026-08-25", "156362", 156362m),
        new("2026-08-31", "746516", 746516m)
    ];

    public static async Task<int> RunAsync(string repo)
    {
        var reportPath = Path.Combine(repo, "artifacts", "migration", "adnoc-totals-report.json");
        var dbPath = SqlitePaths.GetDefaultDatabasePath();
        var backupDir = Path.Combine(repo, "artifacts", "migration", "backups");
        Directory.CreateDirectory(backupDir);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var backupPath = Path.Combine(backupDir, $"petrotrans-pre-adnoc-totals-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        Console.WriteLine($"Live database: {dbPath}");
        Console.WriteLine($"Creating backup: {backupPath}");
        Checkpoint(dbPath);
        File.Copy(dbPath, backupPath, overwrite: false);
        var backupInfo = new FileInfo(backupPath);
        if (!backupInfo.Exists || backupInfo.Length == 0)
        {
            Console.Error.WriteLine("BACKUP FAILED — ADNOC totals aborted.");
            return 1;
        }

        Console.WriteLine($"BACKUP CONFIRMED exists={backupInfo.Exists} bytes={backupInfo.Length}");

        var services = new ServiceCollection();
        services.AddInfrastructure(SqlitePaths.GetConnectionString(dbPath), dbPath);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var purchasing = scope.ServiceProvider.GetRequiredService<IPurchasingService>();
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
            Console.Error.WriteLine("No active user. ADNOC totals aborted after backup.");
            return 1;
        }

        var before = await CaptureAsync(db);
        var report = new Report
        {
            BackupPath = backupPath,
            BackupBytes = backupInfo.Length,
            Before = before
        };

        var matches = new List<(VerifiedBill Target, Guid Id)>();
        foreach (var target in Targets)
        {
            var date = DateTime.Parse(target.Date);
            var marker = $"legacy:pi:adnoc:{target.Date}";
            var bills = await db.PurchaseInvoices.AsNoTracking()
                .Include(x => x.Supplier)
                .Where(x => x.InvoiceDate == date
                            && x.Supplier.Code == "ADNOC"
                            && x.Notes != null
                            && x.Notes.Contains(marker))
                .ToListAsync();
            if (bills.Count != 1)
            {
                report.Failures.Add($"{target.Date} {target.Number}: expected 1 ADNOC T.E bill, found {bills.Count}.");
                continue;
            }

            matches.Add((target, bills[0].Id));
        }

        if (report.Failures.Count > 0 || matches.Count != Targets.Length)
        {
            report.After = await CaptureAsync(db);
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, Json));
            Console.Error.WriteLine("Match failed. No verified totals were written.");
            return 1;
        }

        foreach (var (target, id) in matches)
        {
            var current = await purchasing.GetBillAsync(id);
            if (current is null)
            {
                report.Failures.Add($"{target.Date} {target.Number}: bill disappeared.");
                break;
            }

            var lineFingerprint = LineFingerprint(current);
            var result = await purchasing.SetPostedVerifiedHeaderAsync(id, target.Number, target.Total, owner.Id);
            if (!result.Succeeded || result.Value is null)
            {
                report.Failures.Add($"{target.Date} {target.Number}: {result.Error}");
                break;
            }

            var updated = result.Value;
            var unchanged = updated.Number == target.Number && updated.GoodsTotal == target.Total && current.Number == target.Number && current.GoodsTotal == target.Total;
            report.Updates.Add(new UpdateRow
            {
                Date = target.Date,
                InvoiceNo = target.Number,
                Id = id.ToString(),
                BeforeNumber = current.Number,
                BeforeTotal = current.GoodsTotal,
                AfterNumber = updated.Number,
                AfterTotal = updated.GoodsTotal,
                Unchanged = unchanged,
                LinesUnchanged = lineFingerprint == LineFingerprint(updated),
                LineCount = updated.Lines.Count
            });
        }

        var after = await CaptureAsync(db);
        report.After = after;
        report.Verification = Verify(after, report);
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, Json));
        Console.WriteLine($"Report: {reportPath}");
        foreach (var row in report.Updates)
        {
            Console.WriteLine($"{row.Date} {row.InvoiceNo} {row.BeforeTotal} -> {row.AfterTotal} linesUnchanged={row.LinesUnchanged} unchanged={row.Unchanged}");
        }

        if (report.Failures.Count > 0 || !report.Verification.Ok)
        {
            foreach (var failure in report.Failures.Concat(report.Verification.Problems))
            {
                Console.Error.WriteLine(failure);
            }

            return 1;
        }

        Console.WriteLine("ADNOC verified totals applied.");
        return 0;
    }

    private static Verification Verify(Fingerprint after, Report report)
    {
        var verification = new Verification();
        foreach (var target in Targets)
        {
            var bills = after.Bills.Where(x => x.Date == target.Date && x.Number == target.Number && x.Total == target.Total && x.SupplierCode == "ADNOC").ToList();
            if (bills.Count != 1)
            {
                verification.Problems.Add($"{target.Date} {target.Number} {target.Total}: found {bills.Count} matching bills.");
            }
        }

        var numbered = after.Bills.Where(x => Targets.Select(t => t.Number).Contains(x.Number ?? "")).ToList();
        if (numbered.Count != 7)
        {
            verification.Problems.Add($"Expected 7 uniquely numbered verified bills, found {numbered.Count}.");
        }

        if (after.BillCount != report.Before.BillCount)
        {
            verification.Problems.Add($"Bill count changed {report.Before.BillCount} -> {after.BillCount}.");
        }

        if (after.Aug19 != report.Before.Aug19)
        {
            verification.Problems.Add("19/08 purchase invoice changed.");
        }

        if (after.LineHash != report.Before.LineHash)
        {
            verification.Problems.Add("Purchase invoice lines changed.");
        }

        if (after.Unrelated != report.Before.Unrelated)
        {
            verification.Problems.Add("Unrelated records changed.");
        }

        if (report.Updates.Any(x => !x.LinesUnchanged))
        {
            verification.Problems.Add("A verified bill's lines were rewritten.");
        }

        verification.Ok = verification.Problems.Count == 0 && report.Failures.Count == 0;
        return verification;
    }

    private static async Task<Fingerprint> CaptureAsync(AppDbContext db)
    {
        var bills = await db.PurchaseInvoices.AsNoTracking()
            .Include(x => x.Supplier)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.Id)
            .Select(x => new BillSnap(
                x.Id.ToString(),
                x.InvoiceDate.ToString("yyyy-MM-dd"),
                x.Number,
                x.GoodsTotal,
                x.Status,
                x.Supplier.Code,
                x.Notes))
            .ToListAsync();

        var lines = await db.PurchaseInvoiceLines.AsNoTracking()
            .OrderBy(x => x.InvoiceId)
            .ThenBy(x => x.LineNumber)
            .Select(x => $"{x.Id}:{x.InvoiceId}:{x.VariantId}:{x.Quantity}:{x.UnitPrice}:{x.LineTotal}")
            .ToListAsync();

        var sales = await db.SalesInvoices.AsNoTracking()
            .OrderBy(x => x.Number)
            .Select(x => $"{x.Number}|{x.Status}|{x.GoodsTotal}|{x.InvoiceDate:yyyy-MM-dd}|{x.CustomerId}")
            .ToListAsync();
        var payments = await db.Payments.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.PartyId}|{x.InvoiceId}|{x.Amount}|{x.PaidOn:yyyy-MM-dd}")
            .ToListAsync();
        var treasury = await db.TreasuryEntries.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.Direction}|{x.Amount}|{x.OccurredOn:yyyy-MM-dd}|{x.Category}")
            .ToListAsync();
        var customers = await db.Customers.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.Name}|{x.IsActive}")
            .ToListAsync();
        var stock = await db.InventoryBalances.AsNoTracking()
            .OrderBy(x => x.WarehouseId)
            .ThenBy(x => x.VariantId)
            .Select(x => $"{x.WarehouseId}|{x.VariantId}|{x.OnHand}")
            .ToListAsync();
        var movements = await db.InventoryMovements.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.VariantId}|{x.Quantity}|{x.MovementType}|{x.SourceDocumentId}")
            .ToListAsync();
        var receipts = await db.GoodsReceipts.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.DocumentDate:yyyy-MM-dd}|{x.Status}|{x.Notes}")
            .ToListAsync();
        var receiptLines = await db.GoodsReceiptLines.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.ReceiptId}|{x.VariantId}|{x.Quantity}")
            .ToListAsync();
        var purchasePrices = await db.ProductVariants.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.StandardPurchasePrice}")
            .ToListAsync();
        var supplierPayments = await db.SupplierPayments.AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => $"{x.Id}|{x.SupplierId}|{x.Amount}|{x.PaidOn:yyyy-MM-dd}")
            .ToListAsync();

        var aug19 = bills.SingleOrDefault(x => x.Date == "2026-08-19");
        return new Fingerprint
        {
            BillCount = bills.Count,
            Bills = bills,
            LineHash = Sha(string.Join("|", lines)),
            Aug19 = aug19 is null ? "MISSING" : $"{aug19.Id}|{aug19.Number}|{aug19.Date}|{aug19.Total}|{aug19.Status}|{aug19.Notes}",
            Unrelated = Sha(string.Join("||",
                "sales:" + string.Join(";", sales),
                "payments:" + string.Join(";", payments),
                "treasury:" + string.Join(";", treasury),
                "customers:" + string.Join(";", customers),
                "stock:" + string.Join(";", stock),
                "movements:" + string.Join(";", movements),
                "receipts:" + string.Join(";", receipts),
                "receiptLines:" + string.Join(";", receiptLines),
                "purchasePrices:" + string.Join(";", purchasePrices),
                "supplierPayments:" + string.Join(";", supplierPayments)))
        };
    }

    private static string Sha(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string LineFingerprint(PurchaseInvoiceDto bill)
        => string.Join("|", bill.Lines.Select(x => $"{x.VariantId}:{x.Quantity}:{x.UnitPrice}:{x.LineTotal}"));

    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(SqlitePaths.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
    }

    private sealed record VerifiedBill(string Date, string Number, decimal Total);

    private sealed class Report
    {
        public string BackupPath { get; set; } = "";
        public long BackupBytes { get; set; }
        public List<string> Failures { get; set; } = [];
        public List<UpdateRow> Updates { get; set; } = [];
        public Fingerprint Before { get; set; } = new();
        public Fingerprint After { get; set; } = new();
        public Verification Verification { get; set; } = new();
    }

    private sealed class UpdateRow
    {
        public string Date { get; set; } = "";
        public string InvoiceNo { get; set; } = "";
        public string Id { get; set; } = "";
        public string? BeforeNumber { get; set; }
        public decimal BeforeTotal { get; set; }
        public string? AfterNumber { get; set; }
        public decimal AfterTotal { get; set; }
        public bool Unchanged { get; set; }
        public bool LinesUnchanged { get; set; }
        public int LineCount { get; set; }
    }

    private sealed class Fingerprint
    {
        public int BillCount { get; set; }
        public List<BillSnap> Bills { get; set; } = [];
        public string LineHash { get; set; } = "";
        public string Aug19 { get; set; } = "";
        public string Unrelated { get; set; } = "";
    }

    private sealed record BillSnap(string Id, string Date, string? Number, decimal Total, string Status, string SupplierCode, string? Notes);

    private sealed class Verification
    {
        public bool Ok { get; set; }
        public List<string> Problems { get; set; } = [];
    }
}
