using PetroTrans.Domain.Catalog;

namespace PetroTrans.Domain.Inventory;

public sealed class InventoryBalance
{
    public Guid Id { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid VariantId { get; set; }
    public decimal OnHand { get; set; }

    public Warehouse Warehouse { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
