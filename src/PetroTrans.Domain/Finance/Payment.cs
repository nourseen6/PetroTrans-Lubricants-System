using PetroTrans.Domain;
using PetroTrans.Domain.Sales;

namespace PetroTrans.Domain.Finance;

public sealed class Payment : IAuditedEntity
{
    public Guid Id { get; set; }
    public string PartyKind { get; set; } = PartyKinds.Customer;
    public Guid PartyId { get; set; }
    public Guid? InvoiceId { get; set; }
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

    public PaymentMethod PaymentMethod { get; set; } = null!;
    public SalesInvoice? Invoice { get; set; }
    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
