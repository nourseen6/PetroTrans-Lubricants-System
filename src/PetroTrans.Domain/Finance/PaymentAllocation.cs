using PetroTrans.Domain.Sales;

namespace PetroTrans.Domain.Finance;

public sealed class PaymentAllocation : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Payment Payment { get; set; } = null!;
    public SalesInvoice Invoice { get; set; } = null!;
}
