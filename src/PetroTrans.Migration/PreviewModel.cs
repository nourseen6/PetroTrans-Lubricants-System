namespace PetroTrans.Migration;

public sealed class PreviewRoot
{
    public List<PreviewCustomer> Customers { get; set; } = [];
    public List<PreviewProduct> Products { get; set; } = [];
    public List<PreviewInvoice> SalesInvoices { get; set; } = [];
    public List<PreviewPayment> Payments { get; set; } = [];
    public List<PreviewReceipt> AdnocReceipts { get; set; } = [];
    public List<PreviewBill> AdnocBills { get; set; } = [];
    public PreviewInventory Inventory { get; set; } = new();
    public PreviewAdnocBalance AdnocBalance { get; set; } = new();
    public List<PreviewUnresolvedRef> UnresolvedCustomerReferences { get; set; } = [];
    public List<PreviewWarehouseNote> WarehouseValidationNotes { get; set; } = [];
    public List<PreviewUnresolved> UnresolvedItems { get; set; } = [];
}

public sealed class PreviewCustomer
{
    public string LegacyKey { get; set; } = "";
    public string Nickname { get; set; } = "";
    public string FullName { get; set; } = "";
    public decimal GoodsTotal { get; set; }
    public decimal PaymentTotal { get; set; }
    public decimal CalculatedBalance { get; set; }
    public decimal? ExcelSnap15 { get; set; }
    public decimal? DifferenceVsSnap15 { get; set; }
}

public sealed class PreviewProduct
{
    public string VariantKey { get; set; } = "";
    public string? Grade { get; set; }
    public string? Brand { get; set; }
    public string ProposedProductName { get; set; } = "";
    public string? ProposedBrand { get; set; }
    public string? ProposedSpecification { get; set; }
    public string? ProposedCategory { get; set; }
    public string ProposedPackagingType { get; set; } = "كرتونة";
    public string ProposedPackagingSize { get; set; } = "";
    public decimal? ProposedBaseSell { get; set; }
}

public sealed class PreviewInvoice
{
    public string LegacyKey { get; set; } = "";
    public string CustomerNickname { get; set; } = "";
    public string CustomerFullName { get; set; } = "";
    public string Date { get; set; } = "";
    public decimal GoodsTotal { get; set; }
    public List<PreviewInvoiceLine> Lines { get; set; } = [];
}

public sealed class PreviewInvoiceLine
{
    public string VariantKey { get; set; } = "";
    public decimal QtyCartons { get; set; }
    public decimal? HistoricalUnitPrice { get; set; }
    public string? OverrideReasonIfImported { get; set; }
}

public sealed class PreviewPayment
{
    public string LegacyKey { get; set; } = "";
    public string CustomerNickname { get; set; } = "";
    public string Date { get; set; } = "";
    public decimal Amount { get; set; }
    public bool ImportableUnderCurrentRules { get; set; }
    public PreviewAllocation? ProposedAllocation { get; set; }
    public string? BlockReason { get; set; }
}

public sealed class PreviewAllocation
{
    public string? Mode { get; set; }
    public string? InvoiceLegacyKey { get; set; }
    public decimal AllocatedAmount { get; set; }
}

public sealed class PreviewReceipt
{
    public string LegacyKey { get; set; } = "";
    public string Date { get; set; } = "";
    public List<PreviewInboundLine> Lines { get; set; } = [];
}

public sealed class PreviewBill
{
    public string LegacyKey { get; set; } = "";
    public string Date { get; set; } = "";
    public decimal GoodsTotal { get; set; }
    public List<PreviewInboundLine> Lines { get; set; } = [];
}

public sealed class PreviewInboundLine
{
    public string VariantKey { get; set; } = "";
    public decimal QtyCartons { get; set; }
    public decimal? CostPrice { get; set; }
}

public sealed class PreviewInventory
{
    public decimal OpeningStock { get; set; }
    public decimal InboundFromAdnoc { get; set; }
    public decimal OutboundFromSales { get; set; }
    public decimal CalculatedClosing { get; set; }
    public decimal ExcelClosingFromJard { get; set; }
    public decimal Difference { get; set; }
    public List<PreviewInventoryRow> ByVariant { get; set; } = [];
}

public sealed class PreviewInventoryRow
{
    public string VariantKey { get; set; } = "";
    public decimal CalculatedClosing { get; set; }
    public decimal ExcelClosingFromJard { get; set; }
    public decimal Difference { get; set; }
    public decimal InboundFromAdnoc { get; set; }
    public decimal OutboundFromSales { get; set; }
}

public sealed class PreviewAdnocBalance
{
    public decimal CalculatedPayableAtCutoff { get; set; }
    public decimal AdnocPaymentsInWindow { get; set; }
}

public sealed class PreviewUnresolvedRef
{
    public string Name { get; set; } = "";
    public string Evidence { get; set; } = "";
    public string Action { get; set; } = "";
}

public sealed class PreviewWarehouseNote
{
    public string? Date { get; set; }
    public string? Note { get; set; }
    public string? Status { get; set; }
    public decimal NetCartons { get; set; }
}

public sealed class PreviewUnresolved
{
    public string Kind { get; set; } = "";
    public string? Customer { get; set; }
    public string? Nickname { get; set; }
    public string? Date { get; set; }
    public decimal? ExcelValue { get; set; }
    public string? PossibleSource { get; set; }
}

public sealed class ImportCounters
{
    public int CustomersCreated { get; set; }
    public int CustomersSkipped { get; set; }
    public int ProductsCreated { get; set; }
    public int VariantsCreated { get; set; }
    public int ReceiptsCreated { get; set; }
    public int ReceiptsPosted { get; set; }
    public int BillsCreated { get; set; }
    public int BillsPosted { get; set; }
    public int InvoicesCreated { get; set; }
    public int InvoicesPosted { get; set; }
    public int InvoiceLinesCreated { get; set; }
    public int PaymentsCreated { get; set; }
    public int PaymentsSkipped { get; set; }
    public int ConfigCreated { get; set; }
}

public sealed class ImportEvent
{
    public string Status { get; set; } = "";
    public string Entity { get; set; } = "";
    public string Key { get; set; } = "";
    public string? Detail { get; set; }
}
