using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;

namespace PetroTrans.Domain.Inventory;

public sealed class InventoryMovement : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid VariantId { get; set; }
    public Guid WarehouseId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Direction { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }

    public ProductVariant Variant { get; set; } = null!;
    public Warehouse Warehouse { get; set; } = null!;
}
