using PetroTrans.Domain.Identity;

namespace PetroTrans.Application.Identity;

public static class PermissionCatalog
{
    public static readonly string[] All =
    [
        PermissionCodes.SalesView,
        PermissionCodes.SalesCreate,
        PermissionCodes.SalesPost,
        PermissionCodes.SalesEditPosted,
        PermissionCodes.PaymentsView,
        PermissionCodes.PaymentsCreate,
        PermissionCodes.PaymentsVoid,
        PermissionCodes.CustomersView,
        PermissionCodes.CustomersEdit,
        PermissionCodes.ProductsView,
        PermissionCodes.ProductsEdit,
        PermissionCodes.PricingView,
        PermissionCodes.PricingEditMasters,
        PermissionCodes.PricingOverride,
        PermissionCodes.InventoryView,
        PermissionCodes.InventoryReceive,
        PermissionCodes.InventoryAdjust,
        PermissionCodes.InventoryTransfer,
        PermissionCodes.PurchasingView,
        PermissionCodes.PurchasingEdit,
        PermissionCodes.ReturnsCreate,
        PermissionCodes.ExcelImport,
        PermissionCodes.ExcelExport,
        PermissionCodes.ReportsView,
        PermissionCodes.SettingsManage,
        PermissionCodes.UsersManage,
        PermissionCodes.RolesManage,
        PermissionCodes.AuditView,
        PermissionCodes.BackupCreate,
        PermissionCodes.BackupRestore,
        PermissionCodes.AssistantUse,
        PermissionCodes.TreasuryView,
        PermissionCodes.TreasuryEdit
    ];

    public static readonly string[] OperationalPack =
    [
        PermissionCodes.SalesView,
        PermissionCodes.SalesCreate,
        PermissionCodes.SalesPost,
        PermissionCodes.PaymentsView,
        PermissionCodes.PaymentsCreate,
        PermissionCodes.CustomersView,
        PermissionCodes.CustomersEdit,
        PermissionCodes.ProductsView,
        PermissionCodes.ProductsEdit,
        PermissionCodes.PricingView,
        PermissionCodes.PricingEditMasters,
        PermissionCodes.InventoryView,
        PermissionCodes.InventoryReceive,
        PermissionCodes.InventoryAdjust,
        PermissionCodes.InventoryTransfer,
        PermissionCodes.PurchasingView,
        PermissionCodes.PurchasingEdit,
        PermissionCodes.ReturnsCreate,
        PermissionCodes.ExcelImport,
        PermissionCodes.ExcelExport,
        PermissionCodes.ReportsView,
        PermissionCodes.SettingsManage,
        PermissionCodes.UsersManage,
        PermissionCodes.AuditView,
        PermissionCodes.BackupCreate,
        PermissionCodes.BackupRestore,
        PermissionCodes.TreasuryView,
        PermissionCodes.TreasuryEdit
    ];

    public static readonly (string Code, string NameAr)[] Roles =
    [
        (RoleCodes.OwnerManager, "مالك / مدير"),
        (RoleCodes.Operator, "مشغّل"),
        (RoleCodes.Admin, "مسؤول"),
        (RoleCodes.Sales, "مبيعات"),
        (RoleCodes.Warehouse, "مخزن"),
        (RoleCodes.Accountant, "محاسب")
    ];

    public static IReadOnlyList<string> PermissionsForRole(string roleCode)
    {
        return roleCode switch
        {
            RoleCodes.OwnerManager => [..OperationalPack, PermissionCodes.PricingOverride, PermissionCodes.SalesEditPosted, PermissionCodes.PaymentsVoid, PermissionCodes.AssistantUse],
            RoleCodes.Operator => [..OperationalPack, PermissionCodes.AssistantUse],
            RoleCodes.Admin => [..OperationalPack, PermissionCodes.RolesManage],
            RoleCodes.Sales =>
            [
                PermissionCodes.SalesView,
                PermissionCodes.SalesCreate,
                PermissionCodes.SalesPost,
                PermissionCodes.CustomersView,
                PermissionCodes.CustomersEdit,
                PermissionCodes.PaymentsView,
                PermissionCodes.PaymentsCreate,
                PermissionCodes.PricingView,
                PermissionCodes.ProductsView,
                PermissionCodes.ReportsView
            ],
            RoleCodes.Warehouse =>
            [
                PermissionCodes.InventoryView,
                PermissionCodes.InventoryReceive,
                PermissionCodes.InventoryAdjust,
                PermissionCodes.InventoryTransfer,
                PermissionCodes.ProductsView,
                PermissionCodes.PurchasingView
            ],
            RoleCodes.Accountant =>
            [
                PermissionCodes.PaymentsView,
                PermissionCodes.PaymentsCreate,
                PermissionCodes.ReportsView,
                PermissionCodes.AuditView,
                PermissionCodes.SalesView,
                PermissionCodes.CustomersView,
                PermissionCodes.TreasuryView,
                PermissionCodes.TreasuryEdit
            ],
            _ => []
        };
    }
}
