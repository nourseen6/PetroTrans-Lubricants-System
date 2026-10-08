using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Application.Sales;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Migration;

public sealed class MigrationImporter
{
    public const string WarehouseName = "المخزن الرئيسي";
    public const string PaymentMethodName = "نقدي";
    public const string SupplierCode = "ADNOC";
    public const string SupplierName = "ADNOC";
    public const string InvoicePrefix = "INV";

    private readonly AppDbContext _db;
    private readonly ICustomerService _customers;
    private readonly ICatalogService _catalog;
    private readonly IWarehouseService _warehouses;
    private readonly IPaymentMethodService _methods;
    private readonly ISettingsService _settings;
    private readonly ISupplierService _suppliers;
    private readonly IPurchasingService _purchasing;
    private readonly ISalesInvoiceService _sales;
    private readonly IPaymentService _payments;
    private readonly IInventoryService _inventory;
    private readonly IPartyLedgerService _ledger;

    public MigrationImporter(
        AppDbContext db,
        ICustomerService customers,
        ICatalogService catalog,
        IWarehouseService warehouses,
        IPaymentMethodService methods,
        ISettingsService settings,
        ISupplierService suppliers,
        IPurchasingService purchasing,
        ISalesInvoiceService sales,
        IPaymentService payments,
        IInventoryService inventory,
        IPartyLedgerService ledger)
    {
        _db = db;
        _customers = customers;
        _catalog = catalog;
        _warehouses = warehouses;
        _methods = methods;
        _settings = settings;
        _suppliers = suppliers;
        _purchasing = purchasing;
        _sales = sales;
        _payments = payments;
        _inventory = inventory;
        _ledger = ledger;
    }

    public async Task<ImportResult> ImportAsync(PreviewRoot preview, Guid actorUserId, CancellationToken ct)
    {
        var result = new ImportResult();
        var customerIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var variantIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var invoiceIds = new Dictionary<string, Guid>(StringComparer.Ordinal);

        var warehouseId = await EnsureWarehouseAsync(actorUserId, result, ct);
        var methodId = await EnsurePaymentMethodAsync(actorUserId, result, ct);
        await EnsureInvoiceSeriesAsync(actorUserId, result, ct);
        var supplierId = await EnsureSupplierAsync(actorUserId, result, ct);

        await ImportProductsAsync(preview, actorUserId, variantIds, result, ct);
        await ImportCustomersAsync(preview, actorUserId, customerIds, result, ct);
        await ImportReceiptsAsync(preview, actorUserId, warehouseId, supplierId, variantIds, result, ct);
        await ImportBillsAsync(preview, actorUserId, supplierId, variantIds, result, ct);
        await ImportSalesAsync(preview, actorUserId, warehouseId, customerIds, variantIds, invoiceIds, result, ct);
        await ImportPaymentsAsync(preview, actorUserId, methodId, invoiceIds, result, ct);

        result.Unresolved.AddRange(preview.UnresolvedCustomerReferences.Select(x =>
            $"KEEP UNRESOLVED: {x.Name} — {x.Evidence} — {x.Action}"));
        foreach (var note in preview.WarehouseValidationNotes.Where(x =>
                     x.Status is "unresolved-missing-customer-master" or "unresolved-two-customers" or "unmapped-inbound"))
        {
            result.Unresolved.Add($"KEEP UNRESOLVED warehouse {note.Date} {note.Note} qty={note.NetCartons} ({note.Status}). Not posted as stock.");
        }

        return result;
    }

    public async Task<ReconSnapshot> CaptureAsync(PreviewRoot preview, CancellationToken ct)
    {
        var customers = await _db.Customers.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct);
        var balances = new List<ReconCustomer>();
        foreach (var customer in customers)
        {
            var outstanding = await _ledger.GetOutstandingAsync(PartyKinds.Customer, customer.Id, ct);
            var previewRow = preview.Customers.FirstOrDefault(x => x.FullName == customer.Name);
            balances.Add(new ReconCustomer
            {
                Code = customer.Code,
                Name = customer.Name,
                Nickname = customer.ContactPerson,
                SystemBalance = outstanding,
                ExcelSnap15 = previewRow?.ExcelSnap15,
                ExcelReplay = previewRow?.CalculatedBalance
            });
        }

        var suppliers = await _db.Suppliers.AsNoTracking().ToListAsync(ct);
        decimal adnocPayable = 0;
        foreach (var supplier in suppliers.Where(x => x.Code == SupplierCode || x.Name == SupplierName))
        {
            adnocPayable += await _ledger.GetOutstandingAsync(PartyKinds.Supplier, supplier.Id, ct);
        }

        var stock = await _inventory.ListOnHandAsync(null, ct);
        var invoices = await _db.SalesInvoices.AsNoTracking().Include(x => x.Lines).ToListAsync(ct);
        var payments = await _db.Payments.AsNoTracking().ToListAsync(ct);
        var receipts = await _db.GoodsReceipts.AsNoTracking().ToListAsync(ct);
        var bills = await _db.PurchaseInvoices.AsNoTracking().ToListAsync(ct);
        var movements = await _db.InventoryMovements.AsNoTracking().ToListAsync(ct);

        return new ReconSnapshot
        {
            Customers = balances,
            AdnocPayable = adnocPayable,
            Stock = stock.Select(x => new ReconStock
            {
                ProductName = x.ProductName,
                PackagingType = x.PackagingType,
                PackagingSize = x.PackagingSize,
                OnHand = x.OnHand
            }).ToList(),
            PostedSalesCount = invoices.Count(x => x.Status == "posted"),
            DraftSalesCount = invoices.Count(x => x.Status == "draft"),
            SalesLineCount = invoices.Sum(x => x.Lines.Count),
            SalesGoodsTotal = invoices.Where(x => x.Status == "posted").Sum(x => x.GoodsTotal),
            PaymentCount = payments.Count,
            PaymentTotal = payments.Sum(x => x.Amount),
            ReceiptCount = receipts.Count,
            PostedReceiptCount = receipts.Count(x => x.Status == "posted"),
            BillCount = bills.Count,
            PostedBillCount = bills.Count(x => x.Status == "posted"),
            BillTotal = bills.Where(x => x.Status == "posted").Sum(x => x.GoodsTotal),
            MovementCount = movements.Count,
            ProductCount = await _db.Products.CountAsync(ct),
            VariantCount = await _db.ProductVariants.CountAsync(ct),
            CustomerCount = customers.Count,
            WarehouseCount = await _db.Warehouses.CountAsync(ct),
            SupplierCount = suppliers.Count,
            PaymentMethodCount = await _db.PaymentMethods.CountAsync(ct)
        };
    }

    private async Task<Guid> EnsureWarehouseAsync(Guid actor, ImportResult result, CancellationToken ct)
    {
        var existing = (await _warehouses.ListAsync(false, ct)).FirstOrDefault(x => x.Name == WarehouseName);
        if (existing is not null)
        {
            result.Skip("warehouse", WarehouseName, "already present");
            return existing.Id;
        }

        var saved = await _warehouses.SaveAsync(null, new SaveNamedLookupRequest(WarehouseName, true), actor, ct);
        FailIf(saved, "warehouse");
        result.Counters.ConfigCreated++;
        result.Ok("warehouse", WarehouseName, "required initial configuration");
        return saved.Value!.Id;
    }

    private async Task<Guid> EnsurePaymentMethodAsync(Guid actor, ImportResult result, CancellationToken ct)
    {
        var existing = (await _methods.ListAsync(false, ct)).FirstOrDefault(x => x.Name == PaymentMethodName);
        if (existing is not null)
        {
            result.Skip("payment-method", PaymentMethodName, "already present");
            return existing.Id;
        }

        var saved = await _methods.SaveAsync(null, new SaveNamedLookupRequest(PaymentMethodName, true), actor, ct);
        FailIf(saved, "payment-method");
        result.Counters.ConfigCreated++;
        result.Ok("payment-method", PaymentMethodName, "required initial configuration — not taken from Excel");
        return saved.Value!.Id;
    }

    private async Task EnsureInvoiceSeriesAsync(Guid actor, ImportResult result, CancellationToken ct)
    {
        var existing = await _settings.GetInvoiceSeriesAsync(ct);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Prefix))
        {
            result.Skip("number-series", existing.Prefix, "already present");
            return;
        }

        var saved = await _settings.SaveInvoiceSeriesAsync(new SaveNumberSeriesRequest(InvoicePrefix, 4), actor, ct);
        FailIf(saved, "number-series");
        result.Counters.ConfigCreated++;
        result.Ok("number-series", $"{InvoicePrefix} padding 4", "assigned at post time; not invented from Excel");
    }

    private async Task<Guid> EnsureSupplierAsync(Guid actor, ImportResult result, CancellationToken ct)
    {
        var existing = (await _suppliers.ListAsync(null, ct)).FirstOrDefault(x => x.Code == SupplierCode);
        if (existing is not null)
        {
            result.Skip("supplier", SupplierCode, "already present");
            return existing.Id;
        }

        var saved = await _suppliers.SaveAsync(null, new SaveSupplierRequest(SupplierCode, SupplierName, null, null, true), actor, ct);
        FailIf(saved, "supplier");
        result.Counters.ConfigCreated++;
        result.Ok("supplier", SupplierName, "confirmed supplier = ADNOC");
        return saved.Value!.Id;
    }

    private async Task ImportProductsAsync(
        PreviewRoot preview,
        Guid actor,
        Dictionary<string, Guid> variantIds,
        ImportResult result,
        CancellationToken ct)
    {
        var groups = preview.Products.GroupBy(x => (x.ProposedBrand, x.ProposedSpecification, x.ProposedProductName));
        foreach (var group in groups)
        {
            var first = group.First();
            var existingProduct = (await _catalog.ListProductsAsync(first.ProposedProductName, false, ct))
                .FirstOrDefault(x => x.Name == first.ProposedProductName && x.Brand == first.ProposedBrand);
            ProductDto product;
            if (existingProduct is null)
            {
                var created = await _catalog.CreateProductAsync(new CreateProductRequest(
                    first.ProposedProductName,
                    first.ProposedBrand,
                    first.ProposedCategory,
                    first.ProposedSpecification,
                    true,
                    null,
                    group.Select(v => new SaveVariantRequest(
                        v.ProposedPackagingType,
                        v.ProposedPackagingSize,
                        null,
                        null,
                        null,
                        v.ProposedBaseSell,
                        true)).ToList()), actor, ct);
                FailIf(created, "product");
                product = created.Value!;
                result.Counters.ProductsCreated++;
                result.Counters.VariantsCreated += product.Variants.Count;
                result.Ok("product", first.ProposedProductName, $"{product.Variants.Count} variants");
            }
            else
            {
                product = (await _catalog.GetProductAsync(existingProduct.Id, ct))!;
                result.Skip("product", first.ProposedProductName, "already present");
                foreach (var pack in group)
                {
                    if (product.Variants.Any(v => v.PackagingType == pack.ProposedPackagingType && v.PackagingSize == pack.ProposedPackagingSize))
                    {
                        continue;
                    }

                    var added = await _catalog.AddVariantAsync(product.Id, new SaveVariantRequest(
                        pack.ProposedPackagingType,
                        pack.ProposedPackagingSize,
                        null,
                        null,
                        null,
                        pack.ProposedBaseSell,
                        true), actor, ct);
                    FailIf(added, "variant");
                    result.Counters.VariantsCreated++;
                    result.Ok("variant", pack.VariantKey, "added to existing product");
                }

                product = (await _catalog.GetProductAsync(product.Id, ct))!;
            }

            foreach (var pack in group)
            {
                var match = product.Variants.First(v =>
                    v.PackagingType == pack.ProposedPackagingType && v.PackagingSize == pack.ProposedPackagingSize);
                variantIds[pack.VariantKey] = match.Id;
            }
        }
    }

    private async Task ImportCustomersAsync(
        PreviewRoot preview,
        Guid actor,
        Dictionary<string, Guid> customerIds,
        ImportResult result,
        CancellationToken ct)
    {
        foreach (var row in preview.Customers)
        {
            var found = await FindCustomerAsync(row, ct);
            if (found is not null)
            {
                customerIds[row.Nickname] = found.Id;
                result.Counters.CustomersSkipped++;
                result.Skip("customer", row.FullName, "already present");
                continue;
            }

            var created = await _customers.CreateAsync(new SaveCustomerRequest(
                row.FullName,
                null,
                row.Nickname,
                null,
                null,
                null), actor, ct);
            FailIf(created, "customer");
            customerIds[row.Nickname] = created.Value!.Id;
            result.Counters.CustomersCreated++;
            result.Ok("customer", $"{created.Value.Code} {row.FullName}", $"nickname={row.Nickname}");
        }
    }

    private async Task<CustomerDto?> FindCustomerAsync(PreviewCustomer row, CancellationToken ct)
    {
        var listed = await _customers.ListAsync(row.FullName, false, ct);
        var sameName = new List<CustomerDto>();
        foreach (var item in listed.Where(x => x.Name == row.FullName))
        {
            var full = await _customers.GetAsync(item.Id, ct);
            if (full is null)
            {
                continue;
            }

            if (full.ContactPerson == row.Nickname)
            {
                return full;
            }

            sameName.Add(full);
        }

        // Sheet nickname may have been renamed in Excel (محمد نصار → محمد علام) without a new customer.
        if (sameName.Count == 1)
        {
            return sameName[0];
        }

        return null;
    }

    private async Task ImportReceiptsAsync(
        PreviewRoot preview,
        Guid actor,
        Guid warehouseId,
        Guid supplierId,
        Dictionary<string, Guid> variantIds,
        ImportResult result,
        CancellationToken ct)
    {
        foreach (var row in preview.AdnocReceipts.OrderBy(x => x.Date))
        {
            if (await _db.GoodsReceipts.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(row.LegacyKey), ct))
            {
                result.Skip("goods-receipt", row.LegacyKey, "already present");
                continue;
            }

            var lines = row.Lines
                .Where(l => variantIds.ContainsKey(l.VariantKey))
                .Select(l => new SaveReceiptLineRequest(variantIds[l.VariantKey], l.QtyCartons)).ToList();
            if (lines.Count == 0)
            {
                result.Skip("goods-receipt", row.LegacyKey, "no mapped variants");
                continue;
            }
            var saved = await _purchasing.SaveReceiptAsync(null, new SaveReceiptRequest(
                supplierId,
                warehouseId,
                ParseDate(row.Date),
                Note(row.LegacyKey, "استلام ADNOC من T.E"),
                lines), actor, ct);
            FailIf(saved, "goods-receipt");
            result.Counters.ReceiptsCreated++;
            var posted = await _purchasing.PostReceiptAsync(saved.Value!.Id, actor, ct);
            FailIf(posted, "goods-receipt-post");
            result.Counters.ReceiptsPosted++;
            result.Ok("goods-receipt", row.LegacyKey, $"posted {row.Lines.Count} lines");
        }
    }

    private async Task ImportBillsAsync(
        PreviewRoot preview,
        Guid actor,
        Guid supplierId,
        Dictionary<string, Guid> variantIds,
        ImportResult result,
        CancellationToken ct)
    {
        foreach (var row in preview.AdnocBills.OrderBy(x => x.Date))
        {
            if (await _db.PurchaseInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(row.LegacyKey), ct))
            {
                result.Skip("purchase-invoice", row.LegacyKey, "already present");
                continue;
            }

            var lines = row.Lines
                .Where(l => l.CostPrice is not null)
                .Select(l => new SavePurchaseLineRequest(
                    RequireVariant(variantIds, l.VariantKey),
                    l.QtyCartons,
                    l.CostPrice!.Value)).ToList();
            if (lines.Count == 0)
            {
                result.Skip("purchase-invoice", row.LegacyKey, "no lines with ADNOC cost");
                continue;
            }
            var saved = await _purchasing.SaveBillAsync(null, new SavePurchaseInvoiceRequest(
                supplierId,
                ParseDate(row.Date),
                Note(row.LegacyKey, "فاتورة مشتريات ADNOC من T.E — بدون حركة مخزن ثانية"),
                lines), actor, ct);
            FailIf(saved, "purchase-invoice");
            result.Counters.BillsCreated++;
            var posted = await _purchasing.PostBillAsync(saved.Value!.Id, actor, ct);
            FailIf(posted, "purchase-invoice-post");
            result.Counters.BillsPosted++;
            result.Ok("purchase-invoice", row.LegacyKey, $"posted {row.GoodsTotal}");
        }
    }

    private async Task ImportSalesAsync(
        PreviewRoot preview,
        Guid actor,
        Guid warehouseId,
        Dictionary<string, Guid> customerIds,
        Dictionary<string, Guid> variantIds,
        Dictionary<string, Guid> invoiceIds,
        ImportResult result,
        CancellationToken ct)
    {
        foreach (var row in preview.SalesInvoices.OrderBy(x => x.Date).ThenBy(x => x.CustomerNickname))
        {
            if (await _db.SalesInvoices.AsNoTracking().AnyAsync(x => x.Notes != null && x.Notes.Contains(row.LegacyKey), ct))
            {
                var existing = await _db.SalesInvoices.AsNoTracking().FirstAsync(x => x.Notes != null && x.Notes.Contains(row.LegacyKey), ct);
                invoiceIds[row.LegacyKey] = existing.Id;
                result.Skip("sales-invoice", row.LegacyKey, "already present");
                continue;
            }

            if (!customerIds.TryGetValue(row.CustomerNickname, out var customerId))
            {
                result.Fail("sales-invoice", row.LegacyKey, $"missing customer {row.CustomerNickname}");
                continue;
            }

            var needed = new Dictionary<Guid, decimal>();
            var stockBlocked = false;
            foreach (var line in row.Lines)
            {
                Guid variantId;
                try
                {
                    variantId = RequireVariant(variantIds, line.VariantKey);
                }
                catch (InvalidOperationException ex)
                {
                    result.Fail("sales-invoice", row.LegacyKey, ex.Message);
                    stockBlocked = true;
                    break;
                }

                needed[variantId] = needed.GetValueOrDefault(variantId) + line.QtyCartons;
            }

            if (stockBlocked)
            {
                continue;
            }

            foreach (var (variantId, qty) in needed)
            {
                var onHand = await _inventory.GetOnHandAsync(warehouseId, variantId, ct);
                if (onHand < qty)
                {
                    result.Skip("sales-invoice", row.LegacyKey, $"الكمية المتاحة غير كافية. المتاح: {onHand} المطلوب: {qty}");
                    result.Unresolved.Add($"STOCK BLOCKED {row.LegacyKey} on-hand={onHand} needed={qty}. Not posted. No invented adjustment.");
                    stockBlocked = true;
                    break;
                }
            }

            if (stockBlocked)
            {
                continue;
            }

            var lines = row.Lines.Select(l => new SaveInvoiceLineRequest(
                RequireVariant(variantIds, l.VariantKey),
                l.QtyCartons,
                l.HistoricalUnitPrice,
                l.OverrideReasonIfImported)).ToList();
            var created = await _sales.CreateAsync(new SaveInvoiceRequest(
                customerId,
                warehouseId,
                ParseDate(row.Date),
                null,
                Note(row.LegacyKey, $"فاتورة تاريخية {row.CustomerFullName} {row.Date}"),
                0m,
                null,
                null,
                lines), actor, canOverridePrice: true, ct);
            if (!created.Succeeded)
            {
                result.Fail("sales-invoice", row.LegacyKey, created.Error);
                continue;
            }

            result.Counters.InvoicesCreated++;
            result.Counters.InvoiceLinesCreated += row.Lines.Count;
            var posted = await _sales.PostAsync(created.Value!.Id, actor, ct);
            if (!posted.Succeeded)
            {
                result.Fail("sales-invoice-post", row.LegacyKey, posted.Error);
                continue;
            }

            invoiceIds[row.LegacyKey] = posted.Value!.Id;
            result.Counters.InvoicesPosted++;
            result.Ok("sales-invoice", row.LegacyKey, $"{posted.Value.Number} total={posted.Value.GoodsTotal}");
        }
    }

    private async Task ImportPaymentsAsync(
        PreviewRoot preview,
        Guid actor,
        Guid methodId,
        Dictionary<string, Guid> invoiceIds,
        ImportResult result,
        CancellationToken ct)
    {
        foreach (var row in preview.Payments)
        {
            if (await _db.Payments.AsNoTracking().AnyAsync(x =>
                    (x.Notes != null && x.Notes.Contains(row.LegacyKey)) ||
                    (x.Reference != null && x.Reference == row.LegacyKey), ct))
            {
                result.Skip("payment", row.LegacyKey, "already present");
                continue;
            }

            if (!row.ImportableUnderCurrentRules)
            {
                result.Counters.PaymentsSkipped++;
                result.Skip("payment", row.LegacyKey, row.BlockReason ?? "not importable under current Payment.InvoiceId rules");
                result.Unresolved.Add($"PAYMENT NOT IMPORTED {row.CustomerNickname} {row.Date} amount={row.Amount}: {row.BlockReason}");
                continue;
            }

            var invoiceKey = row.ProposedAllocation?.InvoiceLegacyKey;
            if (string.IsNullOrWhiteSpace(invoiceKey) || !invoiceIds.TryGetValue(invoiceKey, out var invoiceId))
            {
                result.Skip("payment", row.LegacyKey, $"same-day invoice {invoiceKey} was not posted");
                result.Unresolved.Add($"PAYMENT NOT IMPORTED {row.CustomerNickname} {row.Date} amount={row.Amount}: same-day invoice was not posted");
                result.Counters.PaymentsSkipped++;
                continue;
            }

            var created = await _payments.CreateAsync(new SavePaymentRequest(
                invoiceId,
                null,
                methodId,
                row.Amount,
                ParseDate(row.Date),
                Truncate(row.LegacyKey, 128),
                Note(row.LegacyKey, "إيداع عميل من عمود A")), actor, ct);
            if (!created.Succeeded)
            {
                result.Fail("payment", row.LegacyKey, created.Error);
                continue;
            }

            result.Counters.PaymentsCreated++;
            result.Ok("payment", row.LegacyKey, $"{row.Amount} on {invoiceKey}");
        }
    }

    private static Guid RequireVariant(Dictionary<string, Guid> variantIds, string key)
    {
        if (!variantIds.TryGetValue(key, out var id))
        {
            throw new InvalidOperationException($"Unmapped variant {key}");
        }

        return id;
    }

    private static DateTime ParseDate(string value) => DateTime.Parse(value).Date;

    private static string Note(string legacyKey, string text)
    {
        var note = $"[{legacyKey}] {text}";
        return note.Length <= 1024 ? note : note[..1024];
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static void FailIf<T>(OperationResult<T> result, string entity)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{entity}: {result.Error}");
        }
    }
}

public sealed class ImportResult
{
    public ImportCounters Counters { get; } = new();
    public List<ImportEvent> Events { get; } = [];
    public List<string> Unresolved { get; } = [];
    public List<string> Failures { get; } = [];

    public void Ok(string entity, string key, string? detail)
        => Events.Add(new ImportEvent { Status = "imported", Entity = entity, Key = key, Detail = detail });

    public void Skip(string entity, string key, string? detail)
        => Events.Add(new ImportEvent { Status = "skipped", Entity = entity, Key = key, Detail = detail });

    public void Fail(string entity, string key, string? detail)
    {
        Events.Add(new ImportEvent { Status = "failed", Entity = entity, Key = key, Detail = detail });
        Failures.Add($"{entity} {key}: {detail}");
    }
}

public sealed class ReconSnapshot
{
    public List<ReconCustomer> Customers { get; set; } = [];
    public decimal AdnocPayable { get; set; }
    public List<ReconStock> Stock { get; set; } = [];
    public int PostedSalesCount { get; set; }
    public int DraftSalesCount { get; set; }
    public int SalesLineCount { get; set; }
    public decimal SalesGoodsTotal { get; set; }
    public int PaymentCount { get; set; }
    public decimal PaymentTotal { get; set; }
    public int ReceiptCount { get; set; }
    public int PostedReceiptCount { get; set; }
    public int BillCount { get; set; }
    public int PostedBillCount { get; set; }
    public decimal BillTotal { get; set; }
    public int MovementCount { get; set; }
    public int ProductCount { get; set; }
    public int VariantCount { get; set; }
    public int CustomerCount { get; set; }
    public int WarehouseCount { get; set; }
    public int SupplierCount { get; set; }
    public int PaymentMethodCount { get; set; }
}

public sealed class ReconCustomer
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Nickname { get; set; }
    public decimal SystemBalance { get; set; }
    public decimal? ExcelSnap15 { get; set; }
    public decimal? ExcelReplay { get; set; }
}

public sealed class ReconStock
{
    public string ProductName { get; set; } = "";
    public string PackagingType { get; set; } = "";
    public string PackagingSize { get; set; } = "";
    public decimal OnHand { get; set; }
}
