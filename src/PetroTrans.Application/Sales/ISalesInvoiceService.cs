using PetroTrans.Application.Catalog;

namespace PetroTrans.Application.Sales;

public interface ISalesInvoiceService
{
    Task<IReadOnlyList<InvoiceListItemDto>> ListAsync(string? search, Guid? customerId = null, CancellationToken cancellationToken = default);
    Task<InvoiceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OperationResult<InvoiceDto>> CreateAsync(SaveInvoiceRequest request, Guid actorUserId, bool canOverridePrice, CancellationToken cancellationToken = default);
    Task<OperationResult<InvoiceDto>> UpdateAsync(Guid id, SaveInvoiceRequest request, Guid actorUserId, bool canOverridePrice, CancellationToken cancellationToken = default);
    Task<OperationResult<InvoiceDto>> PostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<InvoiceDto>> UnpostAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<OperationResult<InvoiceDto>> DeleteAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);
}
