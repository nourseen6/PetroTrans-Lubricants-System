using PetroTrans.Domain;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Sales;

namespace PetroTrans.Domain.Returns;

public static class ReturnStatuses
{
    public const string Draft = "draft";
    public const string Posted = "posted";
}

public static class BalancePostingStatuses
{
    public const string NotPosted = "not_posted";
    public const string Posted = "posted";
}

public sealed class SalesReturn : IAuditedEntity
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? OriginalSalesInvoiceId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime ReturnDate { get; set; }
    public string Status { get; set; } = ReturnStatuses.Draft;
    public string BalancePostingStatus { get; set; } = BalancePostingStatuses.NotPosted;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Parties.Customer Customer { get; set; } = null!;
    public SalesInvoice? OriginalSalesInvoice { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<SalesReturnLine> Lines { get; set; } = new List<SalesReturnLine>();
}

public sealed class SalesReturnLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid ReturnId { get; set; }
    public int LineNumber { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public SalesReturn Return { get; set; } = null!;
    public Catalog.ProductVariant Variant { get; set; } = null!;
}
