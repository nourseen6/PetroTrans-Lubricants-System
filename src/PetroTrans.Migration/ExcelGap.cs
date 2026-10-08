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

internal static class ExcelGap
{
    private const string GrnNote = "AR-GRN excel:فاتورة 4 5x3";
    private const string BillNote = "AR-BILL excel:فاتورة 4 5x3";
    private const string SamehNote = "AR-SALE excel:2026-08-30 سامح سالم";
    private const string BasimaNote = "AR-SALE excel:2026-08-31 بسيمة سلكا";
    private const string SamehName = "سامح سالم";
    private const string BasimaName = "محمد ابراهيم حامد ( باسم)";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    public static async Task<int> RunAsync(string repo)
    {
        var salesPath = Path.Combine(repo, "artifacts", "migration", "missing-sales-extract.json");
        var reportPath = Path.Combine(repo, "artifacts", "migration", "excel-gap-report.json");
        if (!File.Exists(salesPath))
        {
            Console.Error.WriteLine($"Missing {salesPath}");
            return 1;
        }

        var sales = JsonSerializer.Deserialize<SalesExtract>(await File.ReadAllTextAsync(salesPath), Json)
            ?? throw new InvalidOperationException("missing-sales-extract.json could not be parsed.");

        var dbPath = SqlitePaths.GetDefaultDatabasePath();
        var backupDir = Path.Combine(repo, "artifacts", "migration", "backups");
        Directory.CreateDirectory(backupDir);
        var backupPath = Path.Combine(backupDir, $"petrotrans-pre-excel-gap-{DateTime.Now:yyyyMMdd-HHmmss}.db");

        Console.WriteLine($"Live database: {dbPath}");
        Console.WriteLine($"Creating backup: {backupPath}");
        Checkpoint(dbPath);
        File.Copy(dbPath, backupPath, overwrite: false);
        var backupInfo = new FileInfo(backupPath);
        if (!backupInfo.Exists || backupInfo.Length == 0)
        {
            Console.Error.WriteLine("BACKUP FAILED — excel gap aborted.");
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
            Console.Error.WriteLine("No active user. Excel gap aborted after backup.");
            return 1;
        }

        var catalog = scope.ServiceProvider.GetRequiredService<ICatalogService>();
        var purchasing = scope.ServiceProvider.GetRequiredService<IPurchasingService>();
        var suppliers = scope.ServiceProvider.GetRequiredService<ISupplierService>();
        var salesSvc = scope.ServiceProvider.GetRequiredService<ISalesInvoiceService>();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerService>();
        var inventory = scope.ServiceProvider.GetRequiredService<IInventoryService>();
        var treasury = scope.ServiceProvider.GetRequiredService<ITreasuryService>();

        var warehouseId = await db.Warehouses.AsNoTracking()
            .Where(x => x.DeletedAt == null)
            .Select(x => x.Id)
            .FirstAsync();

        var supplier = (await suppliers.ListAsync("ADNOC"))
            .FirstOrDefault(x => x.Code == "ADNOC" || x.Name == "ADNOC");
        if (supplier is null)
        {
            Console.Error.WriteLine("ADNOC supplier not found.");
            return 1;
        }

        var report = new GapReport
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

        await AddMissingFiveByThreeAsync(catalog, owner.Id, report);
        await AddMissingCompanyFiveByThreeAsync(db, catalog, purchasing, supplier.Id, warehouseId, owner.Id, report);
        ExplainSkippedCompanyInvoices(report);
        await AddWarehouseOnlySalesAsync(db, catalog, customers, salesSvc, inventory, warehouseId, owner.Id, report);
        await AddCustomerBookSalesAsync(db, salesSvc, inventory, sales, warehouseId, owner.Id, report);

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
        report.LiveInvoicesUnchanged = report.LiveInvoicesPreservedBefore.SequenceEqual(report.LiveInvoicesPreservedAfter);

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
        Console.WriteLine($"variants added={report.VariantsAdded} receipts={report.ReceiptsPosted} bills={report.BillsPosted}");
        Console.WriteLine($"sales added={report.SalesAdded} blocked={report.SalesBlocked.Count} skipped={report.Skipped.Count}");
        Console.WriteLine($"treasury created={report.TreasuryMovementsCreated} unchanged={report.TreasuryUnchanged} bal={report.TreasuryAfter.Balance}");
        Console.WriteLine($"historical prices unchanged={report.HistoricalInvoicePricesUnchanged} live invoices unchanged={report.LiveInvoicesUnchanged}");
        Console.WriteLine($"report: {reportPath}");
        return report.Failures.Count == 0 && report.TreasuryUnchanged && report.LiveInvoicesUnchanged ? 0 : 2;
    }

    private static async Task AddMissingFiveByThreeAsync(ICatalogService catalog, Guid actor, GapReport report)
    {
        foreach (var name in new[] { "فوايجر 20w-50 HPSD", "فوايجر HD  50" })
        {
            var product = await FindProductAsync(catalog, name);
            if (product is null)
            {
                report.Failures.Add($"product missing: {name}");
                continue;
            }

            if (product.Variants.Any(v => v.PackagingSize == "5 لتر × 3"))
            {
                report.Skipped.Add($"variant already present: {name} 5 لتر × 3");
                continue;
            }

            var sibling = product.Variants.FirstOrDefault(v => v.PackagingSize == "5 لتر × 4");
            if (sibling is null)
            {
                report.Failures.Add($"{name}: 5 لتر × 4 sibling missing; cannot derive 5×3 price");
                continue;
            }

            var sell = ScaleThreeOfFour(sibling.StandardWholesalePrice);
            var cost = ScaleThreeOfFour(sibling.StandardPurchasePrice);
            var added = await catalog.AddVariantAsync(product.Id, new SaveVariantRequest(
                sibling.PackagingType,
                "5 لتر × 3",
                null,
                null,
                sibling.MinStock,
                sell,
                true,
                cost), actor);
            if (!added.Succeeded)
            {
                report.Failures.Add($"{name} 5×3: {added.Error}");
                continue;
            }

            report.VariantsAdded++;
            report.Actions.Add($"{name} 5 لتر × 3 added sell={sell} cost={cost} from 5×4 × 3/4");
        }
    }

    private static async Task AddMissingCompanyFiveByThreeAsync(
        AppDbContext db,
        ICatalogService catalog,
        IPurchasingService purchasing,
        Guid supplierId,
        Guid warehouseId,
        Guid actor,
        GapReport report)
    {
        if (await db.GoodsReceipts.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(GrnNote)))
        {
            report.Skipped.Add("فاتورة 4 5×3 GRN already present");
            return;
        }

        var hpsd = await FindVariantAsync(catalog, "فوايجر 20w-50 HPSD", "5 لتر × 3");
        var hd = await FindVariantAsync(catalog, "فوايجر HD  50", "5 لتر × 3");
        if (hpsd is null || hd is null)
        {
            report.Failures.Add("cannot receive فاتورة 4 5×3 — variants missing");
            return;
        }

        if (hpsd.StandardPurchasePrice is null || hd.StandardPurchasePrice is null)
        {
            report.Failures.Add("cannot bill فاتورة 4 5×3 — derived purchase price missing");
            return;
        }

        var saved = await purchasing.SaveReceiptAsync(null, new SaveReceiptRequest(
            supplierId,
            warehouseId,
            new DateTime(2026, 8, 19),
            $"{GrnNote} — باقي فاتورة 4 موجود في استلام T.E 2026-08-21؛ الناقص عبوات 5×3 فقط",
            [
                new SaveReceiptLineRequest(hpsd.Id, 30m),
                new SaveReceiptLineRequest(hd.Id, 50m)
            ]), actor);
        if (!saved.Succeeded)
        {
            report.Failures.Add($"فاتورة 4 GRN save: {saved.Error}");
            return;
        }

        var posted = await purchasing.PostReceiptAsync(saved.Value!.Id, actor);
        if (!posted.Succeeded)
        {
            report.Failures.Add($"فاتورة 4 GRN post: {posted.Error}");
            return;
        }

        report.ReceiptsPosted++;
        report.Actions.Add($"posted GRN {posted.Value!.Id} HPSD 5×3 ×30 + HD 5×3 ×50 on 2026-08-19");

        if (await db.PurchaseInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(BillNote)))
        {
            report.Skipped.Add("فاتورة 4 5×3 bill already present");
            return;
        }

        var bill = await purchasing.SaveBillAsync(null, new SavePurchaseInvoiceRequest(
            supplierId,
            new DateTime(2026, 8, 19),
            $"{BillNote} — تكلفة مشتقة 3/4 من عبوة 5×4 لأن T.E لم يحتوِ سعر 5×3",
            [
                new SavePurchaseLineRequest(hpsd.Id, 30m, hpsd.StandardPurchasePrice.Value),
                new SavePurchaseLineRequest(hd.Id, 50m, hd.StandardPurchasePrice.Value)
            ]), actor);
        if (!bill.Succeeded)
        {
            report.Failures.Add($"فاتورة 4 bill save: {bill.Error}");
            return;
        }

        var postedBill = await purchasing.PostBillAsync(bill.Value!.Id, actor);
        if (!postedBill.Succeeded)
        {
            report.Failures.Add($"فاتورة 4 bill post: {postedBill.Error}");
            return;
        }

        report.BillsPosted++;
        report.Actions.Add($"posted bill {postedBill.Value!.Id} total={postedBill.Value.GoodsTotal}");
    }

    private static void ExplainSkippedCompanyInvoices(GapReport report)
    {
        report.Skipped.Add("فاتورة 4 كاملة: نفس استلام T.E 2026-08-21 ما عدا عبوات 5×3 — لم تُكرر الأسطر المشتركة");
        report.Skipped.Add("فاتورة 5 الشركة 2026-08-29: Extra 4ل ×90 + SG 1ل ×14 موجودة مجزأة في T.E 8/24 و 8/25؛ برونز −1 ليست مشتريات");
        report.Skipped.Add("فاتورة 6 الشركة: HD 20ل ×200 + HPSD 20ل ×50 مطابقة لاستلام T.E 2026-08-31؛ Extra −1 ليست مشتريات");
    }

    private static async Task AddWarehouseOnlySalesAsync(
        AppDbContext db,
        ICatalogService catalog,
        ICustomerService customers,
        ISalesInvoiceService sales,
        IInventoryService inventory,
        Guid warehouseId,
        Guid actor,
        GapReport report)
    {
        var sameh = await EnsureCustomerAsync(customers, SamehName, "سامح سالم", actor, report);
        var basima = (await customers.ListAsync(BasimaName, true)).FirstOrDefault(x => x.Name == BasimaName);
        if (basima is null)
        {
            report.Failures.Add($"بسيمة mapped customer missing: {BasimaName}");
        }

        if (sameh is not null)
        {
            await PostCatalogPricedSaleAsync(
                db, sales, inventory, warehouseId, actor, report,
                sameh.Id,
                SamehName,
                new DateTime(2026, 8, 30),
                SamehNote,
                [
                    ("فوايجر 20w-50  Api SG", "1 لتر × 12", 1m),
                    ("اكسترا بلس SF  50", "1 لتر × 12", 1m),
                    ("فوايجر HD  50", "20 لتر × 1", 5m)
                ],
                catalog);
        }

        if (basima is not null)
        {
            await PostCatalogPricedSaleAsync(
                db, sales, inventory, warehouseId, actor, report,
                basima.Id,
                $"{BasimaName} / بسيمة سلكا",
                new DateTime(2026, 8, 31),
                BasimaNote,
                [
                    ("اكسترا بلس SF  50", "1 لتر × 12", 1m),
                    ("فوايجر HD  50", "20 لتر × 1", 1m)
                ],
                catalog);
        }
    }

    private static async Task<CustomerListItemDto?> EnsureCustomerAsync(
        ICustomerService customers,
        string name,
        string nickname,
        Guid actor,
        GapReport report)
    {
        var existing = (await customers.ListAsync(name, true)).FirstOrDefault(x => x.Name == name);
        if (existing is not null)
        {
            report.Skipped.Add($"customer already present: {name}");
            return existing;
        }

        var created = await customers.CreateAsync(new SaveCustomerRequest(name, null, nickname, null, null, null), actor);
        if (!created.Succeeded)
        {
            report.Failures.Add($"create {name}: {created.Error}");
            return null;
        }

        report.CustomersCreated++;
        report.Actions.Add($"created customer {created.Value!.Code} {name}");
        return (await customers.ListAsync(name, true)).First(x => x.Id == created.Value.Id);
    }

    private static async Task PostCatalogPricedSaleAsync(
        AppDbContext db,
        ISalesInvoiceService sales,
        IInventoryService inventory,
        Guid warehouseId,
        Guid actor,
        GapReport report,
        Guid customerId,
        string customerLabel,
        DateTime date,
        string note,
        IReadOnlyList<(string Product, string Pack, decimal Qty)> lines,
        ICatalogService catalog)
    {
        if (await db.SalesInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(note)))
        {
            report.SalesAlreadyExisted++;
            report.Skipped.Add($"{customerLabel} {date:yyyy-MM-dd} already present");
            return;
        }

        var resolved = new List<SaveInvoiceLineRequest>();
        foreach (var (product, pack, qty) in lines)
        {
            var variant = await FindVariantAsync(catalog, product, pack);
            if (variant is null || variant.StandardWholesalePrice is null)
            {
                report.SalesBlocked.Add($"{customerLabel} {date:yyyy-MM-dd}: missing catalog price for {product} {pack}");
                return;
            }

            var onHand = await inventory.GetOnHandAsync(warehouseId, variant.Id);
            if (onHand < qty)
            {
                report.SalesBlocked.Add($"{customerLabel} {date:yyyy-MM-dd}: stock blocked {product} {pack} on-hand={onHand} needed={qty}");
                return;
            }

            resolved.Add(new SaveInvoiceLineRequest(
                variant.Id,
                qty,
                variant.StandardWholesalePrice,
                "كمية من دفتر الجرد — سعر الكتالوج لعدم وجود سعر في الجرد"));
        }

        var created = await sales.CreateAsync(
            new SaveInvoiceRequest(customerId, warehouseId, date, null, note, 0m, null, null, resolved),
            actor,
            canOverridePrice: true);
        if (!created.Succeeded)
        {
            report.Failures.Add($"{customerLabel} {date:yyyy-MM-dd} create: {created.Error}");
            return;
        }

        var posted = await sales.PostAsync(created.Value!.Id, actor);
        if (!posted.Succeeded)
        {
            report.Failures.Add($"{customerLabel} {date:yyyy-MM-dd} post: {posted.Error}");
            return;
        }

        report.SalesAdded++;
        report.Actions.Add($"added {posted.Value!.Number} {customerLabel} {date:yyyy-MM-dd} {posted.Value.GoodsTotal} (jard qty / catalog price)");
    }

    private static async Task AddCustomerBookSalesAsync(
        AppDbContext db,
        ISalesInvoiceService sales,
        IInventoryService inventory,
        SalesExtract extract,
        Guid warehouseId,
        Guid actor,
        GapReport report)
    {
        foreach (var sale in extract.Targets)
        {
            if (sale.Date == "2026-08-09" && sale.Customer.Contains("عشما", StringComparison.Ordinal))
            {
                report.Skipped.Add("INV-0023 already posted in AR-link — not touched");
                continue;
            }

            if (sale.AlreadyPosted)
            {
                report.SalesAlreadyExisted++;
                report.Skipped.Add($"{sale.Customer} {sale.Date} already posted");
                continue;
            }

            if (sale.ExistingDraft is not null)
            {
                report.SalesBlocked.Add($"{sale.Customer} {sale.Date} exists as draft {sale.ExistingDraft.Number}; not creating another invoice");
                continue;
            }

            if (sale.CustomerId is null || sale.CustomerId == Guid.Empty)
            {
                report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: customer id missing");
                continue;
            }

            if (sale.Lines.Any(x => x.VariantId is null || x.Match != "certain"))
            {
                report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: unmatched product lines — not posted");
                continue;
            }

            var needed = new Dictionary<Guid, decimal>();
            foreach (var line in sale.Lines)
            {
                var id = line.VariantId!.Value;
                needed[id] = needed.GetValueOrDefault(id) + line.QtyCartons;
            }

            var blocked = false;
            foreach (var (variantId, qty) in needed)
            {
                var onHand = await inventory.GetOnHandAsync(warehouseId, variantId);
                if (onHand < qty)
                {
                    report.SalesBlocked.Add($"{sale.Customer} {sale.Date}: stock blocked on-hand={onHand} needed={qty}");
                    blocked = true;
                    break;
                }
            }

            if (blocked)
            {
                continue;
            }

            var note = $"AR-SALE excel:{sale.Date} {sale.Customer}";
            if (await db.SalesInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(note)))
            {
                report.SalesAlreadyExisted++;
                continue;
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
                continue;
            }

            if (Math.Abs(created.Value!.GoodsTotal - sale.ExcelTotal) > 0.01m)
            {
                report.Failures.Add($"{sale.Customer} {sale.Date}: created total {created.Value.GoodsTotal} != excel {sale.ExcelTotal}; left as draft, not posted");
                continue;
            }

            var posted = await sales.PostAsync(created.Value.Id, actor);
            if (!posted.Succeeded)
            {
                report.Failures.Add($"{sale.Customer} {sale.Date} post: {posted.Error}");
                continue;
            }

            report.SalesAdded++;
            report.Actions.Add($"added {posted.Value!.Number} {sale.Customer} {sale.Date} {posted.Value.GoodsTotal}");
        }
    }

    private static async Task<ProductDto?> FindProductAsync(ICatalogService catalog, string name)
    {
        var listed = await catalog.ListProductsAsync(name, true);
        var match = listed.FirstOrDefault(x => x.Name == name);
        return match is null ? null : await catalog.GetProductAsync(match.Id);
    }

    private static async Task<VariantDto?> FindVariantAsync(ICatalogService catalog, string productName, string packagingSize)
    {
        var product = await FindProductAsync(catalog, productName);
        return product?.Variants.FirstOrDefault(v => v.PackagingSize == packagingSize);
    }

    private static decimal? ScaleThreeOfFour(decimal? fourPack)
        => fourPack is { } value
            ? decimal.Round(value * 3m / 4m, 2, MidpointRounding.AwayFromZero)
            : null;

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

    private static void Checkpoint(string dbPath)
    {
        using var conn = new SqliteConnection(SqlitePaths.GetConnectionString(dbPath));
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
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

    private sealed class GapReport
    {
        public string BackupPath { get; set; } = "";
        public long BackupBytes { get; set; }
        public string ActingAs { get; set; } = "";
        public BookSnap TreasuryBefore { get; set; } = new();
        public BookSnap TreasuryAfter { get; set; } = new();
        public bool TreasuryUnchanged { get; set; }
        public int TreasuryMovementsCreated { get; set; }
        public decimal StockBefore { get; set; }
        public decimal StockAfter { get; set; }
        public int VariantsAdded { get; set; }
        public int ReceiptsPosted { get; set; }
        public int BillsPosted { get; set; }
        public int CustomersCreated { get; set; }
        public int SalesAdded { get; set; }
        public int SalesAlreadyExisted { get; set; }
        public bool HistoricalInvoicePricesUnchanged { get; set; }
        public bool LiveInvoicesUnchanged { get; set; }
        public string HistoricalPriceHashBefore { get; set; } = "";
        public string HistoricalPriceHashAfter { get; set; } = "";
        public List<string> Actions { get; set; } = [];
        public List<string> Skipped { get; set; } = [];
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
