namespace PetroTrans.Application.Catalog;

public sealed record CustomerTypeDto(Guid Id, string Name, bool IsActive);

public sealed record CustomerListItemDto(
    Guid Id,
    string Code,
    string Name,
    string? CustomerTypeName,
    string? Phone,
    bool IsActive,
    decimal Outstanding,
    int InvoiceCount,
    DateTime? LastInvoiceDate);

public sealed record CustomerDto(
    Guid Id,
    string Code,
    string Name,
    Guid? CustomerTypeId,
    string? CustomerTypeName,
    string? ContactPerson,
    string? Phone,
    string? WhatsApp,
    string? Address,
    bool IsActive);

public sealed record SaveCustomerRequest(
    string Name,
    Guid? CustomerTypeId,
    string? ContactPerson,
    string? Phone,
    string? WhatsApp,
    string? Address);

public sealed record SaveCustomerTypeRequest(string Name, bool IsActive);

public sealed record VariantDto(
    Guid Id,
    Guid ProductId,
    string PackagingType,
    string PackagingSize,
    string? Sku,
    string? Barcode,
    decimal? MinStock,
    decimal? StandardWholesalePrice,
    bool IsActive,
    decimal? StandardPurchasePrice = null);

public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string? Brand,
    bool IsActive,
    int VariantCount,
    string? ImageUrl,
    string? Category = null);

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Brand,
    string? Category,
    string? Specification,
    bool IsActive,
    string? ImageRelativePath,
    string? ImageUrl,
    IReadOnlyList<VariantDto> Variants);

public sealed record SaveVariantRequest(
    string PackagingType,
    string PackagingSize,
    string? Sku,
    string? Barcode,
    decimal? MinStock,
    decimal? StandardWholesalePrice,
    bool IsActive,
    decimal? StandardPurchasePrice = null);

public sealed record CreateProductRequest(
    string Name,
    string? Brand,
    string? Category,
    string? Specification,
    bool IsActive,
    string? ImageRelativePath,
    IReadOnlyList<SaveVariantRequest> Variants);

public sealed record UpdateProductRequest(
    string Name,
    string? Brand,
    string? Category,
    string? Specification,
    bool IsActive,
    string? ImageRelativePath);

public sealed record HomeSummaryDto(
    string DisplayName,
    string UserName,
    int CustomerCount,
    int ProductCount,
    int VariantCount,
    int DraftInvoiceCount,
    int PostedInvoiceCount,
    int UnpaidInvoiceCount,
    int LowStockCount,
    decimal SalesTotal,
    decimal CollectionsTotal,
    decimal CustomerReceivables,
    decimal AdnocPayable,
    decimal InventoryValue,
    decimal TodaySales,
    decimal TodayCollections);

public sealed record OfficialImageDto(string RelativePath, string Url);

public sealed record VariantPickDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    decimal? StandardWholesalePrice,
    bool IsActive,
    decimal? CustomerUnitPrice = null,
    decimal? StandardPurchasePrice = null);

public sealed record PricingMatrixRowDto(
    Guid VariantId,
    Guid ProductId,
    string ProductName,
    string? Brand,
    string PackagingType,
    string PackagingSize,
    decimal? BasePrice,
    int CustomerPriceCount,
    bool IsActive,
    decimal? PurchasePrice = null,
    string? Category = null);

public sealed record CustomerPriceDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    Guid VariantId,
    string ProductName,
    string PackagingType,
    string PackagingSize,
    decimal UnitPrice);

public sealed record PriceHistoryDto(
    Guid Id,
    Guid? CustomerVariantPriceId,
    Guid CustomerId,
    Guid VariantId,
    decimal? OldUnitPrice,
    decimal? NewUnitPrice,
    string ChangeKind,
    string? Reason,
    DateTime ChangedAt,
    Guid? ChangedByUserId);

public sealed record OperationResult<T>(bool Succeeded, string? Error, T? Value, int ErrorStatus = 400)
{
    public static OperationResult<T> Ok(T value) => new(true, null, value, 200);
    public static OperationResult<T> Fail(string error, int errorStatus = 400) => new(false, error, default, errorStatus);
}
