using PetroTrans.Domain;
using PetroTrans.Domain.Parties;

namespace PetroTrans.Domain.Finance;

public sealed class SupplierPayment : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid SupplierId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidOn { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "posted";
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Supplier Supplier { get; set; } = null!;
    public PaymentMethod PaymentMethod { get; set; } = null!;
}
