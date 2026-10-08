using PetroTrans.Domain;
using PetroTrans.Domain.Inventory;

namespace PetroTrans.Domain.Purchasing;

public static class PurchasingStatuses
{
    public const string Draft = "draft";
    public const string Posted = "posted";
    public const string Cancelled = "cancelled";
}

public sealed class GoodsReceipt : IAuditedEntity
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime DocumentDate { get; set; }
    public string Status { get; set; } = PurchasingStatuses.Draft;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Parties.Supplier Supplier { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

public sealed class GoodsReceiptLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid ReceiptId { get; set; }
    public int LineNumber { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public GoodsReceipt Receipt { get; set; } = null!;
    public Catalog.ProductVariant Variant { get; set; } = null!;
}

public sealed class PurchaseInvoice : IAuditedEntity
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public Guid SupplierId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string Status { get; set; } = PurchasingStatuses.Draft;
    public decimal GoodsTotal { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Parties.Supplier Supplier { get; set; } = null!;
    public ICollection<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();
}

public sealed class PurchaseInvoiceLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int LineNumber { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public PurchaseInvoice Invoice { get; set; } = null!;
    public Catalog.ProductVariant Variant { get; set; } = null!;
}
