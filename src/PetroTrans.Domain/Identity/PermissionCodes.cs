namespace PetroTrans.Domain.Identity;

public static class PermissionCodes
{
    public const string SalesView = "sales.view";
    public const string SalesCreate = "sales.create";
    public const string SalesPost = "sales.post";
    public const string SalesEditPosted = "sales.edit_posted";

    public const string PaymentsView = "payments.view";
    public const string PaymentsCreate = "payments.create";
    public const string PaymentsVoid = "payments.void";

    public const string CustomersView = "customers.view";
    public const string CustomersEdit = "customers.edit";

    public const string ProductsView = "products.view";
    public const string ProductsEdit = "products.edit";

    public const string PricingView = "pricing.view";
    public const string PricingEditMasters = "pricing.edit_masters";
    public const string PricingOverride = "pricing.override";

    public const string InventoryView = "inventory.view";
    public const string InventoryReceive = "inventory.receive";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InventoryTransfer = "inventory.transfer";

    public const string PurchasingView = "purchasing.view";
    public const string PurchasingEdit = "purchasing.edit";

    public const string ReturnsCreate = "returns.create";

    public const string ExcelImport = "excel.import";
    public const string ExcelExport = "excel.export";

    public const string ReportsView = "reports.view";
    public const string SettingsManage = "settings.manage";
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string AuditView = "audit.view";
    public const string BackupCreate = "backup.create";
    public const string BackupRestore = "backup.restore";
    public const string AssistantUse = "assistant.use";
    public const string TreasuryView = "treasury.view";
    public const string TreasuryEdit = "treasury.edit";
}
