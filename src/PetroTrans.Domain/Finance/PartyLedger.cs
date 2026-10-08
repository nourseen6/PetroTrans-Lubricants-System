using PetroTrans.Domain;

namespace PetroTrans.Domain.Finance;

public static class PartyKinds
{
    public const string Customer = "customer";
    public const string Supplier = "supplier";
}

public static class LedgerEntryTypes
{
    public const string SalesInvoice = "sales_invoice";
    public const string Payment = "payment";
    public const string SalesReturn = "sales_return";
    public const string PurchaseInvoice = "purchase_invoice";
    public const string SupplierPayment = "supplier_payment";
}

public sealed class PartyLedgerEntry : IAuditedEntity
{
    public Guid Id { get; set; }
    public string PartyKind { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public decimal SignedAmount { get; set; }
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;
}
