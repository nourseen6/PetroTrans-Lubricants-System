using PetroTrans.Domain;

namespace PetroTrans.Domain.Catalog;

public sealed class ProductVariant : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string PackagingType { get; set; } = string.Empty;
    public string PackagingSize { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string? Barcode { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? StandardWholesalePrice { get; set; }
    public decimal? StandardPurchasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public Product Product { get; set; } = null!;
    public ICollection<ProductMedia> Media { get; set; } = new List<ProductMedia>();
}
