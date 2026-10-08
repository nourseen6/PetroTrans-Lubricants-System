using PetroTrans.Domain;

namespace PetroTrans.Domain.Catalog;

public sealed class Product : IAuditedEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Category { get; set; }
    public string? Specification { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    public ICollection<ProductMedia> Media { get; set; } = new List<ProductMedia>();
}
