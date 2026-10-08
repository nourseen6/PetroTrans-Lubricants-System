using PetroTrans.Domain;

namespace PetroTrans.Domain.Catalog;

public sealed class ProductMedia : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Product? Product { get; set; }
    public ProductVariant? Variant { get; set; }
}
