using PetroTrans.Domain;
using PetroTrans.Domain.Parties;

namespace PetroTrans.Domain.Catalog;

public sealed class CustomerVariantPrice : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VariantId { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Customer Customer { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
