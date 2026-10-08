using PetroTrans.Domain;

namespace PetroTrans.Domain.Sales;

public sealed class SalesInvoice : IAuditedEntity
{
    public Guid Id { get; set; }
    public string? Number { get; set; }
    public Guid CustomerId { get; set; }
    public Guid? WarehouseId { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = SalesStatuses.Draft;
    public decimal GoodsTotal { get; set; }
    public decimal LinesSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ManualAdjustment { get; set; }
    public bool HasManualTotal { get; set; }
    public string? ManualTotalReason { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal RemainingTotal { get; set; }
    public string PaymentStatus { get; set; } = PaymentStatuses.Unpaid;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
    public Guid? OriginInstallationId { get; set; }

    public Parties.Customer Customer { get; set; } = null!;
    public Inventory.Warehouse? Warehouse { get; set; }
    public ICollection<SalesInvoiceLine> Lines { get; set; } = new List<SalesInvoiceLine>();
}
