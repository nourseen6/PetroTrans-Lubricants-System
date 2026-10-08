namespace PetroTrans.Application.Catalog;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerListItemDto>> ListAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerDto>> CreateAsync(SaveCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerDto>> UpdateAsync(Guid id, SaveCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerDto>> ArchiveAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerDto>> RestoreAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ICustomerTypeService
{
    Task<IReadOnlyList<CustomerTypeDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerTypeDto>> CreateAsync(SaveCustomerTypeRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerTypeDto>> UpdateAsync(Guid id, SaveCustomerTypeRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
}

public interface ICatalogService
{
    Task<IReadOnlyList<ProductListItemDto>> ListProductsAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<ProductDto>> CreateProductAsync(CreateProductRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<ProductDto>> UpdateProductAsync(Guid id, UpdateProductRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<ProductDto>> ArchiveProductAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<ProductDto>> RestoreProductAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> AddVariantAsync(Guid productId, SaveVariantRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> UpdateVariantAsync(Guid variantId, SaveVariantRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> ArchiveVariantAsync(Guid variantId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> RestoreVariantAsync(Guid variantId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OfficialImageDto>> ListOfficialImagesAsync();
    Task<IReadOnlyList<VariantPickDto>> SearchVariantsAsync(string? search, Guid? customerId = null, CancellationToken cancellationToken = default);
}

public interface IHomeService
{
    Task<HomeSummaryDto> GetSummaryAsync(string displayName, string userName, CancellationToken cancellationToken = default);
}

public interface IPricingService
{
    Task<IReadOnlyList<PricingMatrixRowDto>> ListMatrixAsync(string? search = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CustomerPriceDto>> ListCustomerPricesAsync(Guid? customerId = null, CancellationToken cancellationToken = default);
    Task<OperationResult<CustomerPriceDto>> SetCustomerPriceAsync(Guid customerId, Guid variantId, decimal unitPrice, string? reason, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<bool>> RemoveCustomerPriceAsync(Guid customerId, Guid variantId, string? reason, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> UpdateBasePriceAsync(Guid variantId, decimal unitPrice, string? reason, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<VariantDto>> UpdatePurchasePriceAsync(Guid variantId, decimal unitPrice, string? reason, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PriceHistoryDto>> GetHistoryAsync(Guid? variantId = null, Guid? customerId = null, CancellationToken cancellationToken = default);
}
