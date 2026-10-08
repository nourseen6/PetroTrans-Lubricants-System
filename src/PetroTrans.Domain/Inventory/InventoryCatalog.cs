namespace PetroTrans.Domain.Inventory;

public static class MovementTypes
{
    public const string OpeningStock = "opening_stock";
    public const string PurchaseReceipt = "purchase_receipt";
    public const string Sale = "sale";
    public const string SalesReturn = "sales_return";
    public const string AdjustmentIn = "adjustment_in";
    public const string AdjustmentOut = "adjustment_out";
    public const string TransferOut = "transfer_out";
    public const string TransferIn = "transfer_in";
}

public static class MovementDirections
{
    public const string In = "in";
    public const string Out = "out";
}

public static class SourceDocumentTypes
{
    public const string SalesInvoice = "sales_invoice";
    public const string GoodsReceipt = "goods_receipt";
    public const string SalesReturn = "sales_return";
    public const string InventoryAdjustment = "inventory_adjustment";
    public const string StockTransfer = "stock_transfer";
    public const string PurchaseInvoice = "purchase_invoice";
    public const string Payment = "payment";
    public const string PaymentLink = "payment_link";
    public const string SupplierPayment = "supplier_payment";
}
