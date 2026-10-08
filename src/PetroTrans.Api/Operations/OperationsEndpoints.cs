using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Api.Operations;

public static class OperationsEndpoints
{
    public static void MapOperationsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/warehouses", (Delegate)ListWarehouses).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapGet("/api/warehouses/active", (Delegate)ListActiveWarehouses).RequireAuthorization();
        app.MapPost("/api/warehouses", (Delegate)CreateWarehouse).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapPut("/api/warehouses/{id:guid}", (Delegate)UpdateWarehouse).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");

        app.MapGet("/api/payment-methods", (Delegate)ListMethods).RequireAuthorization($"perm:{PermissionCodes.PaymentsView}");
        app.MapPost("/api/payment-methods", (Delegate)CreateMethod).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapPut("/api/payment-methods/{id:guid}", (Delegate)UpdateMethod).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");

        app.MapGet("/api/settings/company", (Delegate)GetCompany).RequireAuthorization();
        app.MapPut("/api/settings/company", (Delegate)SaveCompany).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapGet("/api/settings/invoice-series", (Delegate)GetSeries).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapPut("/api/settings/invoice-series", (Delegate)SaveSeries).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapGet("/api/settings/logos", (Delegate)ListLogos).RequireAuthorization($"perm:{PermissionCodes.SettingsManage}");
        app.MapGet("/api/audit", (Delegate)ListAudit).RequireAuthorization($"perm:{PermissionCodes.AuditView}");
        app.MapPost("/api/backup", (Delegate)Backup).RequireAuthorization($"perm:{PermissionCodes.BackupCreate}");
        app.MapPost("/api/restore", (Delegate)Restore).RequireAuthorization($"perm:{PermissionCodes.BackupRestore}");

        app.MapGet("/api/inventory/on-hand", (Delegate)OnHand).RequireAuthorization($"perm:{PermissionCodes.InventoryView}");
        app.MapGet("/api/inventory/movements", (Delegate)Movements).RequireAuthorization($"perm:{PermissionCodes.InventoryView}");
        app.MapGet("/api/inventory/adjustments", (Delegate)ListAdjustments).RequireAuthorization($"perm:{PermissionCodes.InventoryView}");
        app.MapGet("/api/inventory/adjustments/{id:guid}", (Delegate)GetAdjustment).RequireAuthorization($"perm:{PermissionCodes.InventoryView}");
        app.MapPost("/api/inventory/adjustments", (Delegate)Adjust).RequireAuthorization($"perm:{PermissionCodes.InventoryAdjust}");
        app.MapPut("/api/inventory/adjustments/{id:guid}", (Delegate)UpdateAdjustment).RequireAuthorization($"perm:{PermissionCodes.InventoryAdjust}");
        app.MapDelete("/api/inventory/adjustments/{id:guid}", (Delegate)DeleteAdjustment).RequireAuthorization($"perm:{PermissionCodes.InventoryAdjust}");
        app.MapPost("/api/inventory/transfers", (Delegate)Transfer).RequireAuthorization($"perm:{PermissionCodes.InventoryTransfer}");

        app.MapGet("/api/payments", (Delegate)ListPayments).RequireAuthorization($"perm:{PermissionCodes.PaymentsView}");
        app.MapGet("/api/payments/{id:guid}", (Delegate)GetPayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsView}");
        app.MapPost("/api/payments", (Delegate)CreatePayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsCreate}");
        app.MapPut("/api/payments/{id:guid}", (Delegate)UpdatePayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsCreate}");
        app.MapPost("/api/payments/{id:guid}/void", (Delegate)VoidPayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsVoid}");

        app.MapGet("/api/supplier-payments", (Delegate)ListSupplierPayments).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapPost("/api/supplier-payments", (Delegate)CreateSupplierPayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsCreate}");
        app.MapDelete("/api/supplier-payments/{id:guid}", (Delegate)DeleteSupplierPayment).RequireAuthorization($"perm:{PermissionCodes.PaymentsCreate}");

        app.MapGet("/api/suppliers", (Delegate)ListSuppliers).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapGet("/api/suppliers/{id:guid}", (Delegate)GetSupplier).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapPost("/api/suppliers", (Delegate)CreateSupplier).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");
        app.MapPut("/api/suppliers/{id:guid}", (Delegate)UpdateSupplier).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");

        app.MapGet("/api/goods-receipts", (Delegate)ListReceipts).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapGet("/api/goods-receipts/{id:guid}", (Delegate)GetReceipt).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapPost("/api/goods-receipts", (Delegate)CreateReceipt).RequireAuthorization($"perm:{PermissionCodes.InventoryReceive}");
        app.MapPut("/api/goods-receipts/{id:guid}", (Delegate)UpdateReceipt).RequireAuthorization($"perm:{PermissionCodes.InventoryReceive}");
        app.MapPost("/api/goods-receipts/{id:guid}/post", (Delegate)PostReceipt).RequireAuthorization($"perm:{PermissionCodes.InventoryReceive}");

        app.MapGet("/api/purchase-invoices", (Delegate)ListBills).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapGet("/api/purchase-invoices/{id:guid}", (Delegate)GetBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapPost("/api/purchase-invoices", (Delegate)CreateBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");
        app.MapPut("/api/purchase-invoices/{id:guid}", (Delegate)UpdateBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");
        app.MapPost("/api/purchase-invoices/{id:guid}/post", (Delegate)PostBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");
        app.MapPost("/api/purchase-invoices/{id:guid}/unpost", (Delegate)UnpostBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");
        app.MapDelete("/api/purchase-invoices/{id:guid}", (Delegate)DeleteBill).RequireAuthorization($"perm:{PermissionCodes.PurchasingEdit}");

        app.MapGet("/api/sales-returns", (Delegate)ListReturns).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");
        app.MapGet("/api/sales-returns/invoices/{invoiceId:guid}/lines", (Delegate)GetReturnable).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");
        app.MapGet("/api/sales-returns/{id:guid}", (Delegate)GetReturn).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");
        app.MapPost("/api/sales-returns", (Delegate)CreateReturn).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");
        app.MapPut("/api/sales-returns/{id:guid}", (Delegate)UpdateReturn).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");
        app.MapPost("/api/sales-returns/{id:guid}/post", (Delegate)PostReturn).RequireAuthorization($"perm:{PermissionCodes.ReturnsCreate}");

        app.MapGet("/api/reports/{type}", (Delegate)Report).RequireAuthorization($"perm:{PermissionCodes.ReportsView}");
        app.MapGet("/api/customers/{id:guid}/statement", (Delegate)Statement).RequireAuthorization($"perm:{PermissionCodes.ReportsView}");
        app.MapGet("/api/suppliers/{id:guid}/statement", (Delegate)SupplierStatement).RequireAuthorization($"perm:{PermissionCodes.PurchasingView}");
        app.MapGet("/api/treasury", (Delegate)TreasuryBook).RequireAuthorization($"perm:{PermissionCodes.TreasuryView}");
        app.MapPost("/api/treasury", (Delegate)CreateTreasury).RequireAuthorization($"perm:{PermissionCodes.TreasuryEdit}");
        app.MapDelete("/api/treasury/{id:guid}", (Delegate)DeleteTreasury).RequireAuthorization($"perm:{PermissionCodes.TreasuryEdit}");
    }

    private static async Task<IResult> ListWarehouses(IWarehouseService warehouses, CancellationToken ct)
        => Results.Json(await warehouses.ListAsync(false, ct));
    private static async Task<IResult> ListActiveWarehouses(IWarehouseService warehouses, CancellationToken ct)
        => Results.Json(await warehouses.ListAsync(true, ct));
    private static async Task<IResult> CreateWarehouse(SaveNamedLookupRequest request, HttpContext http, IWarehouseService warehouses, CancellationToken ct)
        => From(await warehouses.SaveAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateWarehouse(Guid id, SaveNamedLookupRequest request, HttpContext http, IWarehouseService warehouses, CancellationToken ct)
        => From(await warehouses.SaveAsync(id, request, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListMethods(bool activeOnly, IPaymentMethodService methods, CancellationToken ct)
        => Results.Json(await methods.ListAsync(activeOnly, ct));
    private static async Task<IResult> CreateMethod(SaveNamedLookupRequest request, HttpContext http, IPaymentMethodService methods, CancellationToken ct)
        => From(await methods.SaveAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateMethod(Guid id, SaveNamedLookupRequest request, HttpContext http, IPaymentMethodService methods, CancellationToken ct)
        => From(await methods.SaveAsync(id, request, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> GetCompany(ISettingsService settings, CancellationToken ct)
        => Results.Json(await settings.GetCompanyAsync(ct));
    private static async Task<IResult> SaveCompany(SaveCompanySettingsRequest request, HttpContext http, ISettingsService settings, CancellationToken ct)
        => From(await settings.SaveCompanyAsync(request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> GetSeries(ISettingsService settings, CancellationToken ct)
        => Results.Json(await settings.GetInvoiceSeriesAsync(ct));
    private static async Task<IResult> SaveSeries(SaveNumberSeriesRequest request, HttpContext http, ISettingsService settings, CancellationToken ct)
        => From(await settings.SaveInvoiceSeriesAsync(request, CurrentUser.RequireId(http), ct));
    private static IResult ListLogos()
        => Results.Json(OfficialLogoList());
    private static async Task<IResult> ListAudit(string? q, ISettingsService settings, CancellationToken ct)
        => Results.Json(await settings.ListAuditAsync(q, ct));
    private static async Task<IResult> Backup(BackupRequest request, HttpContext http, ISettingsService settings, CancellationToken ct)
        => From(await settings.BackupAsync(request.DestinationPath, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> Restore(RestoreRequest request, HttpContext http, ISettingsService settings, CancellationToken ct)
        => From(await settings.RestoreAsync(request.SourcePath, request.Confirm, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> OnHand(Guid? warehouseId, IInventoryService inventory, CancellationToken ct)
        => Results.Json(await inventory.ListOnHandAsync(warehouseId, ct));
    private static async Task<IResult> Movements(Guid? warehouseId, Guid? variantId, IInventoryService inventory, CancellationToken ct)
        => Results.Json(await inventory.ListMovementsAsync(warehouseId, variantId, ct));
    private static async Task<IResult> ListAdjustments(IInventoryService inventory, CancellationToken ct)
        => Results.Json(await inventory.ListAdjustmentsAsync(ct));
    private static async Task<IResult> GetAdjustment(Guid id, IInventoryService inventory, CancellationToken ct)
        => JsonOrNotFound(await inventory.GetAdjustmentAsync(id, ct), "التسوية غير موجودة.");
    private static async Task<IResult> Adjust(SaveAdjustmentRequest request, HttpContext http, IInventoryService inventory, CancellationToken ct)
        => From(await inventory.AdjustAsync(request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateAdjustment(Guid id, SaveAdjustmentRequest request, HttpContext http, IInventoryService inventory, CancellationToken ct)
        => From(await inventory.UpdateAdjustmentAsync(id, request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> DeleteAdjustment(Guid id, HttpContext http, IInventoryService inventory, CancellationToken ct)
        => From(await inventory.DeleteAdjustmentAsync(id, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> Transfer(SaveTransferRequest request, HttpContext http, IInventoryService inventory, CancellationToken ct)
        => From(await inventory.TransferAsync(request, CurrentUser.RequireId(http), ct), 201);

    private static async Task<IResult> ListPayments(Guid? customerId, IPaymentService payments, CancellationToken ct)
        => Results.Json(await payments.ListAsync(customerId, ct));
    private static async Task<IResult> GetPayment(Guid id, IPaymentService payments, CancellationToken ct)
        => JsonOrNotFound(await payments.GetAsync(id, ct), "الدفعة غير موجودة.");
    private static async Task<IResult> CreatePayment(SavePaymentRequest request, HttpContext http, IPaymentService payments, CancellationToken ct)
        => From(await payments.CreateAsync(request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdatePayment(Guid id, SavePaymentRequest request, HttpContext http, IPaymentService payments, CancellationToken ct)
        => From(await payments.UpdateAsync(id, request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> VoidPayment(Guid id, HttpContext http, IPaymentService payments, CancellationToken ct)
        => From(await payments.VoidAsync(id, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListSupplierPayments(Guid? supplierId, ISupplierPaymentService payments, CancellationToken ct)
        => Results.Json(await payments.ListAsync(supplierId, ct));
    private static async Task<IResult> CreateSupplierPayment(SaveSupplierPaymentRequest request, HttpContext http, ISupplierPaymentService payments, CancellationToken ct)
        => From(await payments.CreateAsync(request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> DeleteSupplierPayment(Guid id, HttpContext http, ISupplierPaymentService payments, CancellationToken ct)
        => From(await payments.DeleteAsync(id, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListSuppliers(string? q, ISupplierService suppliers, CancellationToken ct)
        => Results.Json(await suppliers.ListAsync(q, ct));
    private static async Task<IResult> GetSupplier(Guid id, ISupplierService suppliers, CancellationToken ct)
        => JsonOrNotFound(await suppliers.GetAsync(id, ct), "المورد غير موجود.");
    private static async Task<IResult> CreateSupplier(SaveSupplierRequest request, HttpContext http, ISupplierService suppliers, CancellationToken ct)
        => From(await suppliers.SaveAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateSupplier(Guid id, SaveSupplierRequest request, HttpContext http, ISupplierService suppliers, CancellationToken ct)
        => From(await suppliers.SaveAsync(id, request, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListReceipts(IPurchasingService purchasing, CancellationToken ct)
        => Results.Json(await purchasing.ListReceiptsAsync(ct));
    private static async Task<IResult> GetReceipt(Guid id, IPurchasingService purchasing, CancellationToken ct)
        => JsonOrNotFound(await purchasing.GetReceiptAsync(id, ct), "إذن الاستلام غير موجود.");
    private static async Task<IResult> CreateReceipt(SaveReceiptRequest request, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.SaveReceiptAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateReceipt(Guid id, SaveReceiptRequest request, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.SaveReceiptAsync(id, request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> PostReceipt(Guid id, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.PostReceiptAsync(id, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListBills(IPurchasingService purchasing, CancellationToken ct)
        => Results.Json(await purchasing.ListBillsAsync(ct));
    private static async Task<IResult> GetBill(Guid id, IPurchasingService purchasing, CancellationToken ct)
        => JsonOrNotFound(await purchasing.GetBillAsync(id, ct), "فاتورة المشتريات غير موجودة.");
    private static async Task<IResult> CreateBill(SavePurchaseInvoiceRequest request, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.SaveBillAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateBill(Guid id, SavePurchaseInvoiceRequest request, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.SaveBillAsync(id, request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> PostBill(Guid id, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.PostBillAsync(id, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> UnpostBill(Guid id, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.UnpostBillAsync(id, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> DeleteBill(Guid id, HttpContext http, IPurchasingService purchasing, CancellationToken ct)
        => From(await purchasing.DeleteBillAsync(id, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> ListReturns(ISalesReturnService returns, CancellationToken ct)
        => Results.Json(await returns.ListAsync(ct));
    private static async Task<IResult> GetReturnable(Guid invoiceId, ISalesReturnService returns, CancellationToken ct)
        => JsonOrNotFound(await returns.GetReturnableAsync(invoiceId, ct), "الفاتورة غير موجودة أو غير مرحّلة.");
    private static async Task<IResult> GetReturn(Guid id, ISalesReturnService returns, CancellationToken ct)
        => JsonOrNotFound(await returns.GetAsync(id, ct), "المرتجع غير موجود.");
    private static async Task<IResult> CreateReturn(SaveReturnRequest request, HttpContext http, ISalesReturnService returns, CancellationToken ct)
        => From(await returns.SaveAsync(null, request, CurrentUser.RequireId(http), ct), 201);
    private static async Task<IResult> UpdateReturn(Guid id, SaveReturnRequest request, HttpContext http, ISalesReturnService returns, CancellationToken ct)
        => From(await returns.SaveAsync(id, request, CurrentUser.RequireId(http), ct));
    private static async Task<IResult> PostReturn(Guid id, HttpContext http, ISalesReturnService returns, CancellationToken ct)
        => From(await returns.PostAsync(id, CurrentUser.RequireId(http), ct));

    private static async Task<IResult> Report(string type, DateTime? from, DateTime? to, Guid? customerId, Guid? supplierId, Guid? variantId, Guid? warehouseId, IReportService reports, CancellationToken ct)
    {
        var filters = new ReportFilters(from, to, customerId, supplierId, variantId, warehouseId);
        var result = type switch
        {
            "sales" => await reports.SalesAsync(filters, ct),
            "invoice-status" => await reports.InvoiceStatusAsync(filters, ct),
            "unpaid" => await reports.UnpaidInvoicesAsync(filters, ct),
            "payments" => await reports.PaymentsAsync(filters, ct),
            "inventory" => await reports.InventoryOnHandAsync(filters, ct),
            "movements" => await reports.InventoryMovementsAsync(filters, ct),
            "purchasing" => await reports.PurchasingAsync(filters, ct),
            "supplier-balances" => await reports.SupplierBalancesAsync(filters, ct),
            "customer-balances" => await reports.CustomerBalancesAsync(filters, ct),
            "product-track" => await reports.ProductTrackAsync(filters, ct),
            "profit" => await reports.ProfitAsync(filters, ct),
            _ => null
        };
        return result is null
            ? Results.Json(new { error = "غير موجود." }, statusCode: StatusCodes.Status404NotFound)
            : Results.Json(result);
    }

    private static async Task<IResult> Statement(Guid id, DateTime? from, DateTime? to, IReportService reports, CancellationToken ct)
        => JsonOrNotFound(await reports.CustomerStatementAsync(id, from, to, ct), "العميل غير موجود.");

    private static async Task<IResult> SupplierStatement(Guid id, DateTime? from, DateTime? to, IReportService reports, CancellationToken ct)
        => JsonOrNotFound(await reports.SupplierStatementAsync(id, from, to, ct), "المورد غير موجود.");

    private static async Task<IResult> TreasuryBook(DateTime? from, DateTime? to, string? category, ITreasuryService treasury, CancellationToken ct)
        => Results.Json(await treasury.GetBookAsync(from, to, category, ct));

    private static async Task<IResult> CreateTreasury(SaveTreasuryEntryRequest request, HttpContext http, ITreasuryService treasury, CancellationToken ct)
        => From(await treasury.CreateAsync(request, CurrentUser.RequireId(http), ct), 201);

    private static async Task<IResult> DeleteTreasury(Guid id, HttpContext http, ITreasuryService treasury, CancellationToken ct)
        => From(await treasury.DeleteAsync(id, CurrentUser.RequireId(http), ct));

    private static IResult JsonOrNotFound<T>(T? value, string error)
        => value is null ? Results.Json(new { error }, statusCode: StatusCodes.Status404NotFound) : Results.Json(value);

    private static IResult From<T>(OperationResult<T> result, int successStatus = StatusCodes.Status200OK)
    {
        if (!result.Succeeded || result.Value is null)
        {
            return Results.Json(new { error = result.Error }, statusCode: result.ErrorStatus);
        }

        return successStatus == StatusCodes.Status200OK
            ? Results.Json(result.Value)
            : Results.Json(result.Value, statusCode: successStatus);
    }

    private static object OfficialLogoList()
    {
        return Infrastructure.Assets.OfficialAssets.ListLogos()
            .Select(item => new OfficialImageDto(item.RelativePath, Infrastructure.Assets.OfficialAssets.ToPublicUrl(item.RelativePath)!))
            .ToList();
    }

    private sealed record BackupRequest(string DestinationPath);
    private sealed record RestoreRequest(string SourcePath, bool Confirm);
}
