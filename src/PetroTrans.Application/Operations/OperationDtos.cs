using PetroTrans.Application.Catalog;

namespace PetroTrans.Application.Operations;

public sealed record WarehouseDto(Guid Id, string Name, bool IsActive);
public sealed record SaveNamedLookupRequest(string Name, bool IsActive);
public sealed record PaymentMethodDto(Guid Id, string Name, bool IsActive);
public sealed record NumberSeriesDto(string DocumentType, string Prefix, int Padding, int NextValue);
public sealed record SaveNumberSeriesRequest(string Prefix, int Padding);
public sealed record CompanySettingsDto(string CompanyName, string? LogoRelativePath, string? LogoUrl, string DefaultLocale);
public sealed record SaveCompanySettingsRequest(string CompanyName, string? LogoRelativePath, string DefaultLocale);

public sealed record StockRowDto(
    Guid WarehouseId,
    string WarehouseName,
    Guid VariantId,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    decimal OnHand,
    decimal? MinStock,
    bool BelowMin,
    string? Category = null,
    decimal? CompanyCost = null,
    decimal? StockValue = null);

public sealed record MovementRowDto(
    Guid Id,
    DateTime OccurredAt,
    string WarehouseName,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    string MovementType,
    string Direction,
    decimal Quantity,
    string SourceDocumentType,
    Guid SourceDocumentId,
    string? Notes);

public sealed record SaveAdjustmentLineRequest(Guid VariantId, decimal Quantity);
public sealed record SaveAdjustmentRequest(Guid WarehouseId, string Direction, string Reason, string? Notes, DateTime OccurredAt, IReadOnlyList<SaveAdjustmentLineRequest> Lines);
public sealed record AdjustmentDto(
    Guid Id,
    Guid WarehouseId,
    string WarehouseName,
    string Direction,
    string Reason,
    DateTime OccurredAt,
    IReadOnlyList<AdjustmentLineDto> Lines,
    string? Notes = null);
public sealed record AdjustmentLineDto(Guid VariantId, string ProductName, string PackagingType, string PackagingSize, decimal Quantity);

public sealed record SaveTransferLineRequest(Guid VariantId, decimal Quantity);
public sealed record SaveTransferRequest(Guid FromWarehouseId, Guid ToWarehouseId, string? Notes, DateTime OccurredAt, IReadOnlyList<SaveTransferLineRequest> Lines);
public sealed record TransferDto(Guid Id, string FromWarehouse, string ToWarehouse, DateTime OccurredAt);

public sealed record SupplierListItemDto(Guid Id, string Code, string Name, string? Phone, bool IsActive, decimal Outstanding);
public sealed record SupplierDto(Guid Id, string Code, string Name, string? Phone, string? Address, bool IsActive, decimal Outstanding);
public sealed record SaveSupplierRequest(string Code, string Name, string? Phone, string? Address, bool IsActive);

public sealed record ReceiptListItemDto(Guid Id, string? Number, DateTime DocumentDate, string SupplierName, string WarehouseName, string Status);
public sealed record ReceiptLineDto(Guid VariantId, string ProductName, string PackagingType, string PackagingSize, decimal Quantity);
public sealed record ReceiptDto(Guid Id, string? Number, Guid SupplierId, string SupplierName, Guid WarehouseId, DateTime DocumentDate, string Status, string? Notes, IReadOnlyList<ReceiptLineDto> Lines);
public sealed record SaveReceiptLineRequest(Guid VariantId, decimal Quantity);
public sealed record SaveReceiptRequest(Guid SupplierId, Guid WarehouseId, DateTime DocumentDate, string? Notes, IReadOnlyList<SaveReceiptLineRequest> Lines);

public sealed record PurchaseInvoiceListItemDto(Guid Id, string? Number, DateTime InvoiceDate, string SupplierName, decimal GoodsTotal, string Status);
public sealed record PurchaseInvoiceLineDto(Guid VariantId, string ProductName, string PackagingType, string PackagingSize, decimal Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record PurchaseInvoiceDto(
    Guid Id,
    string? Number,
    Guid SupplierId,
    string SupplierName,
    DateTime InvoiceDate,
    string Status,
    decimal GoodsTotal,
    string? Notes,
    IReadOnlyList<PurchaseInvoiceLineDto> Lines,
    decimal LinesSubtotal = 0,
    decimal PaidTotal = 0,
    decimal RemainingTotal = 0,
    string PaymentStatus = "unpaid",
    DateTime CreatedAt = default,
    DateTime UpdatedAt = default,
    string? CreatedByName = null,
    string? UpdatedByName = null);
public sealed record SavePurchaseLineRequest(Guid VariantId, decimal Quantity, decimal UnitPrice);
public sealed record SavePurchaseInvoiceRequest(Guid SupplierId, DateTime InvoiceDate, string? Notes, IReadOnlyList<SavePurchaseLineRequest> Lines);

public sealed record PaymentListItemDto(Guid Id, DateTime PaidOn, string CustomerName, string? InvoiceNumber, decimal Amount, string MethodName, string? Reference);
public sealed record PaymentAllocationDto(Guid InvoiceId, string? InvoiceNumber, decimal Amount);
public sealed record PaymentDto(Guid Id, Guid CustomerId, Guid? InvoiceId, Guid PaymentMethodId, decimal Amount, DateTime PaidOn, string? Reference, string? Notes, string? CustomerName = null, string? InvoiceNumber = null, string? MethodName = null, IReadOnlyList<PaymentAllocationDto>? Allocations = null);
public sealed record SavePaymentAllocationRequest(Guid InvoiceId, decimal Amount);
public sealed record SavePaymentRequest(Guid? InvoiceId, Guid? CustomerId, Guid PaymentMethodId, decimal Amount, DateTime PaidOn, string? Reference, string? Notes, Guid? ExistingTreasuryEntryId = null, IReadOnlyList<SavePaymentAllocationRequest>? Allocations = null);

public sealed record SupplierPaymentListItemDto(Guid Id, DateTime PaidOn, string SupplierName, decimal Amount, string MethodName, string? Reference);
public sealed record SupplierPaymentDto(Guid Id, Guid SupplierId, Guid PaymentMethodId, decimal Amount, DateTime PaidOn, string? Reference, string? Notes);
public sealed record SaveSupplierPaymentRequest(Guid SupplierId, Guid PaymentMethodId, decimal Amount, DateTime PaidOn, string? Reference, string? Notes);

public sealed record ReturnListItemDto(Guid Id, DateTime ReturnDate, string CustomerName, string? OriginalNumber, string Status, string BalancePostingStatus);
public sealed record ReturnLineDto(Guid VariantId, string ProductName, string PackagingType, string PackagingSize, decimal Quantity);
public sealed record ReturnDto(Guid Id, string? Number, Guid CustomerId, string CustomerName, Guid? OriginalSalesInvoiceId, Guid WarehouseId, DateTime ReturnDate, string Status, string BalancePostingStatus, string? Notes, IReadOnlyList<ReturnLineDto> Lines);
public sealed record SaveReturnLineRequest(Guid VariantId, decimal Quantity);
public sealed record SaveReturnRequest(Guid CustomerId, Guid? OriginalSalesInvoiceId, Guid WarehouseId, DateTime ReturnDate, string? Notes, IReadOnlyList<SaveReturnLineRequest> Lines);
public sealed record ReturnableLineDto(
    Guid VariantId,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    decimal SoldQty,
    decimal AlreadyReturnedQty,
    decimal ReturnableQty,
    decimal UnitPrice);
public sealed record ReturnableInvoiceDto(
    Guid InvoiceId,
    string? Number,
    Guid CustomerId,
    Guid WarehouseId,
    string WarehouseName,
    IReadOnlyList<ReturnableLineDto> Lines);

public sealed record AuditRowDto(Guid Id, DateTime OccurredAt, string? UserName, string Action, string? EntityType, Guid? EntityId, string? BeforeJson, string? AfterJson);

public sealed record ReportFilters(DateTime? From, DateTime? To, Guid? CustomerId, Guid? SupplierId, Guid? VariantId, Guid? WarehouseId);
public sealed record ReportKpiDto(string Label, string Value, string? Hint = null);
public sealed record ReportDto(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows,
    IReadOnlyList<ReportKpiDto>? Kpis = null,
    string? Note = null,
    IReadOnlyList<ReportDto>? Sections = null);

public sealed record CustomerStatementLineDto(
    DateTime OccurredAt,
    string Document,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    Guid SourceDocumentId,
    string EntryType);

public sealed record CustomerStatementDto(
    Guid CustomerId,
    string CustomerName,
    string? CustomerCode,
    string? Phone,
    DateTime? From,
    DateTime? To,
    decimal OpeningBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal ClosingBalance,
    IReadOnlyList<CustomerStatementLineDto> Lines)
{
    public decimal Outstanding => ClosingBalance;
}

public sealed record SupplierStatementDto(
    Guid SupplierId,
    string SupplierName,
    string? SupplierCode,
    string? Phone,
    DateTime? From,
    DateTime? To,
    decimal OpeningBalance,
    decimal TotalDebits,
    decimal TotalCredits,
    decimal ClosingBalance,
    IReadOnlyList<CustomerStatementLineDto> Lines)
{
    public decimal Outstanding => ClosingBalance;
}

public interface IWarehouseService
{
    Task<IReadOnlyList<WarehouseDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<OperationResult<WarehouseDto>> SaveAsync(Guid? id, SaveNamedLookupRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IPaymentMethodService
{
    Task<IReadOnlyList<PaymentMethodDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<OperationResult<PaymentMethodDto>> SaveAsync(Guid? id, SaveNamedLookupRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<CompanySettingsDto> GetCompanyAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<CompanySettingsDto>> SaveCompanyAsync(SaveCompanySettingsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<NumberSeriesDto?> GetInvoiceSeriesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<NumberSeriesDto>> SaveInvoiceSeriesAsync(SaveNumberSeriesRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditRowDto>> ListAuditAsync(string? action, CancellationToken cancellationToken = default);
    Task<OperationResult<string>> BackupAsync(string destinationPath, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<string>> RestoreAsync(string sourcePath, bool confirm, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IInventoryService
{
    Task<decimal> GetOnHandAsync(Guid warehouseId, Guid variantId, CancellationToken cancellationToken = default);
    Task<string?> ApplyAsync(Guid warehouseId, Guid variantId, string movementType, string direction, decimal quantity, string sourceType, Guid sourceId, DateTime occurredAt, Guid actorUserId, string? notes, CancellationToken cancellationToken = default);
    Task<string?> ReverseSourceAsync(string sourceType, Guid sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockRowDto>> ListOnHandAsync(Guid? warehouseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MovementRowDto>> ListMovementsAsync(Guid? warehouseId, Guid? variantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdjustmentDto>> ListAdjustmentsAsync(CancellationToken cancellationToken = default);
    Task<AdjustmentDto?> GetAdjustmentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<AdjustmentDto>> AdjustAsync(SaveAdjustmentRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<AdjustmentDto>> UpdateAdjustmentAsync(Guid id, SaveAdjustmentRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> DeleteAdjustmentAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<TransferDto>> TransferAsync(SaveTransferRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IPartyLedgerService
{
    Task AddAsync(string partyKind, Guid partyId, string entryType, decimal signedAmount, string sourceType, Guid sourceId, DateTime occurredAt, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<decimal> GetOutstandingAsync(string partyKind, Guid partyId, CancellationToken cancellationToken = default);
}

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentListItemDto>> ListAsync(Guid? customerId, CancellationToken cancellationToken = default);
    Task<PaymentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<PaymentDto>> CreateAsync(SavePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PaymentDto>> UpdateAsync(Guid id, SavePaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PaymentDto>> VoidAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ISupplierPaymentService
{
    Task<IReadOnlyList<SupplierPaymentListItemDto>> ListAsync(Guid? supplierId, CancellationToken cancellationToken = default);
    Task<OperationResult<SupplierPaymentDto>> CreateAsync(SaveSupplierPaymentRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<SupplierPaymentDto>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ISupplierService
{
    Task<IReadOnlyList<SupplierListItemDto>> ListAsync(string? search, CancellationToken cancellationToken = default);
    Task<SupplierDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<SupplierDto>> SaveAsync(Guid? id, SaveSupplierRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IPurchasingService
{
    Task<IReadOnlyList<ReceiptListItemDto>> ListReceiptsAsync(CancellationToken cancellationToken = default);
    Task<ReceiptDto?> GetReceiptAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<ReceiptDto>> SaveReceiptAsync(Guid? id, SaveReceiptRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<ReceiptDto>> PostReceiptAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceListItemDto>> ListBillsAsync(CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceDto?> GetBillAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<PurchaseInvoiceDto>> SaveBillAsync(Guid? id, SavePurchaseInvoiceRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PurchaseInvoiceDto>> PostBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PurchaseInvoiceDto>> SetPostedVerifiedHeaderAsync(Guid id, string number, decimal goodsTotal, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PurchaseInvoiceDto>> UnpostBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<PurchaseInvoiceDto>> DeleteBillAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ISalesReturnService
{
    Task<IReadOnlyList<ReturnListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<ReturnDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReturnableInvoiceDto?> GetReturnableAsync(Guid invoiceId, CancellationToken cancellationToken = default);
    Task<OperationResult<ReturnDto>> SaveAsync(Guid? id, SaveReturnRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<ReturnDto>> PostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<ReportDto> SalesAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> InvoiceStatusAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> UnpaidInvoicesAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> PaymentsAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> InventoryOnHandAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> InventoryMovementsAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> PurchasingAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> SupplierBalancesAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> CustomerBalancesAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> ProductTrackAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<ReportDto> ProfitAsync(ReportFilters filters, CancellationToken cancellationToken = default);
    Task<CustomerStatementDto?> CustomerStatementAsync(Guid customerId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<SupplierStatementDto?> SupplierStatementAsync(Guid supplierId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
}

public sealed record TreasuryEntryDto(
    Guid Id,
    DateTime OccurredOn,
    string Direction,
    string? Category,
    string Description,
    decimal Amount,
    string? Notes,
    decimal RunningBalance,
    bool Linked = false);

public sealed record TreasuryBookDto(
    DateTime? From,
    DateTime? To,
    decimal OpeningBalance,
    decimal TotalIn,
    decimal TotalOut,
    decimal Balance,
    IReadOnlyList<TreasuryEntryDto> Entries,
    string? FilterCategory = null,
    decimal CategoryTotal = 0);

public sealed record SaveTreasuryEntryRequest(
    DateTime OccurredOn,
    string Direction,
    string? Category,
    string Description,
    decimal Amount,
    string? Notes,
    string? SourceDocumentType = null,
    Guid? SourceDocumentId = null);

public interface ITreasuryService
{
    Task<TreasuryBookDto> GetBookAsync(DateTime? from = null, DateTime? to = null, string? category = null, CancellationToken cancellationToken = default);
    Task<OperationResult<TreasuryEntryDto>> CreateAsync(SaveTreasuryEntryRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}
