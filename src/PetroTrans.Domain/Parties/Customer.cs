using PetroTrans.Domain;

namespace PetroTrans.Domain.Parties;

public sealed class Customer : IAuditedEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CustomerTypeId { get; set; }
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
    public decimal? CreditLimit { get; set; }
    public int? PaymentTermDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public CustomerType? CustomerType { get; set; }
}
