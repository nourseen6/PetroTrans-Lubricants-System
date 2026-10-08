using PetroTrans.Domain;

namespace PetroTrans.Domain.Inventory;

public sealed class StockTransfer : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Warehouse FromWarehouse { get; set; } = null!;
    public Warehouse ToWarehouse { get; set; } = null!;
    public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();
}

public sealed class StockTransferLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid TransferId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public StockTransfer Transfer { get; set; } = null!;
    public Catalog.ProductVariant Variant { get; set; } = null!;
}
