using PetroTrans.Domain.Identity;

namespace PetroTrans.Application.Assistant;

public interface IAssistantTool
{
    string Name { get; }
    string DescriptionAr { get; }
    bool IsWrite { get; }
    string? RequiredPermission { get; }
}

public sealed record AssistantToolDescriptor(string Name, string DescriptionAr, bool IsWrite, string? RequiredPermission) : IAssistantTool;

public static class AssistantToolCatalog
{
    public static readonly IReadOnlyList<IAssistantTool> All =
    [
        new AssistantToolDescriptor("get_customers", "قائمة العملاء", false, PermissionCodes.CustomersView),
        new AssistantToolDescriptor("get_customer", "بيانات عميل", false, PermissionCodes.CustomersView),
        new AssistantToolDescriptor("get_customer_balance", "رصيد عميل", false, PermissionCodes.CustomersView),
        new AssistantToolDescriptor("get_customer_statement", "كشف حساب", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("get_customer_invoices", "فواتير عميل", false, PermissionCodes.SalesView),
        new AssistantToolDescriptor("get_customer_payments", "تحصيلات عميل", false, PermissionCodes.PaymentsView),
        new AssistantToolDescriptor("create_customer", "إنشاء عميل", true, PermissionCodes.CustomersEdit),
        new AssistantToolDescriptor("archive_customer", "أرشفة عميل", true, PermissionCodes.CustomersEdit),
        new AssistantToolDescriptor("restore_customer", "استعادة عميل", true, PermissionCodes.CustomersEdit),
        new AssistantToolDescriptor("get_invoice", "عرض فاتورة", false, PermissionCodes.SalesView),
        new AssistantToolDescriptor("search_invoices", "بحث فواتير", false, PermissionCodes.SalesView),
        new AssistantToolDescriptor("create_invoice_draft", "مسودة فاتورة بيع", true, PermissionCodes.SalesCreate),
        new AssistantToolDescriptor("record_payment", "تسجيل تحصيل", true, PermissionCodes.PaymentsCreate),
        new AssistantToolDescriptor("get_products", "الأصناف", false, PermissionCodes.ProductsView),
        new AssistantToolDescriptor("get_variants", "العبوات", false, PermissionCodes.ProductsView),
        new AssistantToolDescriptor("get_base_price", "سعر العملاء", false, PermissionCodes.PricingView),
        new AssistantToolDescriptor("get_customer_price", "سعر خاص لعميل", false, PermissionCodes.PricingView),
        new AssistantToolDescriptor("get_price_history", "تاريخ الأسعار", false, PermissionCodes.PricingView),
        new AssistantToolDescriptor("update_base_price", "تعديل سعر العملاء", true, PermissionCodes.PricingEditMasters),
        new AssistantToolDescriptor("update_customer_price", "تعديل سعر خاص لعميل", true, PermissionCodes.PricingEditMasters),
        new AssistantToolDescriptor("get_inventory", "المخزون", false, PermissionCodes.InventoryView),
        new AssistantToolDescriptor("get_inventory_movements", "حركة المخزون", false, PermissionCodes.InventoryView),
        new AssistantToolDescriptor("get_low_stock", "تحت الحد", false, PermissionCodes.InventoryView),
        new AssistantToolDescriptor("get_sales_report", "تقرير المبيعات", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("get_customer_balances_report", "أرصدة العملاء", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("get_inventory_report", "تقرير المخزون", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("get_purchasing_report", "تقرير المشتريات", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("print_invoice", "طباعة فاتورة", false, PermissionCodes.SalesView),
        new AssistantToolDescriptor("print_customer_statement", "طباعة كشف حساب", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("print_report", "طباعة تقرير", false, PermissionCodes.ReportsView),
        new AssistantToolDescriptor("print_payment_receipt", "طباعة إيصال", false, PermissionCodes.PaymentsView),
    ];

    public static IAssistantTool? ForIntent(string intentType) => intentType switch
    {
        AssistantIntentTypes.SalesInvoice => All.First(x => x.Name == "create_invoice_draft"),
        AssistantIntentTypes.RecordPayment => All.First(x => x.Name == "record_payment"),
        AssistantIntentTypes.CustomerBalance => All.First(x => x.Name == "get_customer_balance"),
        AssistantIntentTypes.CustomerInvoices => All.First(x => x.Name == "get_customer_invoices"),
        AssistantIntentTypes.CustomerStatement => All.First(x => x.Name == "get_customer_statement"),
        AssistantIntentTypes.PrintStatement => All.First(x => x.Name == "print_customer_statement"),
        AssistantIntentTypes.SalesReport => All.First(x => x.Name == "get_sales_report"),
        AssistantIntentTypes.UpdateBasePrice => All.First(x => x.Name == "update_base_price"),
        AssistantIntentTypes.UpdateCustomerPrice => All.First(x => x.Name == "update_customer_price"),
        AssistantIntentTypes.CreateCustomer => All.First(x => x.Name == "create_customer"),
        AssistantIntentTypes.ListOwing => All.First(x => x.Name == "get_customer_balances_report"),
        AssistantIntentTypes.PrintInvoice => All.First(x => x.Name == "print_invoice"),
        AssistantIntentTypes.PrintPayment => All.First(x => x.Name == "print_payment_receipt"),
        AssistantIntentTypes.PrintReport => All.First(x => x.Name == "print_report"),
        AssistantIntentTypes.InventoryLow => All.First(x => x.Name == "get_low_stock"),
        AssistantIntentTypes.InventoryLookup => All.First(x => x.Name == "get_inventory"),
        AssistantIntentTypes.GetPrices => All.First(x => x.Name == "get_base_price"),
        AssistantIntentTypes.ArchiveCustomer => All.First(x => x.Name == "archive_customer"),
        _ => null
    };
}
