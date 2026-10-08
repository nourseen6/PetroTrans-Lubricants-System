using PetroTrans.Application.Catalog;

namespace PetroTrans.Application.Assistant;

public sealed record AssistantParseRequest(string Text, string? Locale);
public sealed record AssistantDraftLine(Guid? VariantId, string ProductQuery, decimal Quantity, decimal? UnitPrice, string? PackagingHint);
public sealed record AssistantAmbiguity(string Field, string Message, IReadOnlyList<AssistantChoice> Choices);
public sealed record AssistantChoice(Guid Id, string Label);
public sealed record AssistantDraftPreview(
    Guid DraftId,
    string IntentType,
    string Status,
    string SummaryAr,
    Guid? CustomerId,
    string? CustomerName,
    DateTime? InvoiceDate,
    IReadOnlyList<AssistantDraftLine> Lines,
    decimal? DiscountAmount,
    decimal? ManualTotal,
    string? ManualTotalReason,
    decimal? EstimatedTotal,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<AssistantAmbiguity> Ambiguities,
    bool CanApprove,
    Guid? VariantId = null,
    decimal? Amount = null,
    Guid? PaymentMethodId = null,
    string? ActionLabelAr = null,
    object? ActionPayload = null);

public sealed record AssistantApproveResult(Guid DraftId, string Status, Guid? CreatedEntityId, string MessageAr);

public static class AssistantIntentTypes
{
    public const string SalesInvoice = "sales_invoice";
    public const string RecordPayment = "record_payment";
    public const string CustomerBalance = "customer_balance";
    public const string CustomerInvoices = "customer_invoices";
    public const string CustomerStatement = "customer_statement";
    public const string SalesReport = "sales_report";
    public const string UpdateBasePrice = "update_base_price";
    public const string UpdateCustomerPrice = "update_customer_price";
    public const string CreateCustomer = "create_customer";
    public const string ListOwing = "list_owing";
    public const string PrintStatement = "print_statement";
    public const string PrintInvoice = "print_invoice";
    public const string PrintPayment = "print_payment";
    public const string PrintReport = "print_report";
    public const string InventoryLow = "inventory_low";
    public const string InventoryLookup = "inventory_lookup";
    public const string GetPrices = "get_prices";
    public const string ArchiveCustomer = "archive_customer";
    public const string Unknown = "unknown";
}

public interface IAssistantService
{
    Task<OperationResult<AssistantDraftPreview>> ParseAsync(AssistantParseRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<AssistantDraftPreview?> GetDraftAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<AssistantApproveResult>> ApproveAsync(Guid id, Guid actorUserId, bool canOverridePrice, CancellationToken cancellationToken = default);
    Task<OperationResult<AssistantApproveResult>> RejectAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}
