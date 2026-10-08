using PetroTrans.Domain;

namespace PetroTrans.Domain.Inventory;

public sealed class InventoryAdjustment : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public string Direction { get; set; } = MovementDirections.In;
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Warehouse Warehouse { get; set; } = null!;
    public ICollection<InventoryAdjustmentLine> Lines { get; set; } = new List<InventoryAdjustmentLine>();
}

public sealed class InventoryAdjustmentLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid AdjustmentId { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public InventoryAdjustment Adjustment { get; set; } = null!;
    public Catalog.ProductVariant Variant { get; set; } = null!;
}
