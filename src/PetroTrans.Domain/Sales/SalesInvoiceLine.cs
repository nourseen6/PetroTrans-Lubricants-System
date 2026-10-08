using PetroTrans.Domain;
using PetroTrans.Domain.Catalog;

namespace PetroTrans.Domain.Sales;

public sealed class SalesInvoiceLine : IAuditedEntity
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int LineNumber { get; set; }
    public Guid VariantId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? LineTotal { get; set; }
    public string? PriceSource { get; set; }
    public decimal? ResolvedUnitPrice { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? OverrideByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public SalesInvoice Invoice { get; set; } = null!;
    public ProductVariant Variant { get; set; } = null!;
}
