using PetroTrans.Domain;

namespace PetroTrans.Domain.Finance;

public sealed class PaymentMethod : IAuditedEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }
}
