namespace PetroTrans.Application.Sales;

public sealed record InvoiceListItemDto(
    Guid Id,
    string? Number,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string CustomerName,
    string CustomerCode,
    decimal GoodsTotal,
    decimal PaidTotal,
    decimal RemainingTotal,
    string Status,
    string PaymentStatus);

public sealed record InvoiceLineDto(
    Guid Id,
    int LineNumber,
    Guid VariantId,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    decimal Quantity,
    decimal? UnitPrice,
    decimal? LineTotal,
    string? PriceSource,
    decimal? ResolvedUnitPrice,
    string? OverrideReason);

public sealed record InvoiceDto(
    Guid Id,
    string? Number,
    Guid CustomerId,
    string CustomerName,
    Guid? WarehouseId,
    string? WarehouseName,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string Status,
    decimal LinesSubtotal,
    decimal DiscountAmount,
    decimal ManualAdjustment,
    bool HasManualTotal,
    string? ManualTotalReason,
    decimal GoodsTotal,
    decimal PaidTotal,
    decimal RemainingTotal,
    string PaymentStatus,
    string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? CreatedByName = null,
    string? UpdatedByName = null);

public sealed record SaveInvoiceLineRequest(
    Guid VariantId,
    decimal Quantity,
    decimal? UnitPrice,
    string? OverrideReason);

public sealed record SaveInvoiceRequest(
    Guid CustomerId,
    Guid? WarehouseId,
    DateTime InvoiceDate,
    DateTime? DueDate,
    string? Notes,
    decimal DiscountAmount,
    decimal? ManualTotal,
    string? ManualTotalReason,
    IReadOnlyList<SaveInvoiceLineRequest> Lines);
