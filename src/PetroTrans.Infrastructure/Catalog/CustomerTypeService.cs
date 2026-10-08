using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Domain;
using PetroTrans.Domain.Parties;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public sealed class CustomerTypeService : ICustomerTypeService
{
    private readonly AppDbContext _db;

    public CustomerTypeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerTypeDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.CustomerTypes.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query
            .OrderBy(x => x.Name)
            .Select(x => new CustomerTypeDto(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<CustomerTypeDto>> CreateAsync(SaveCustomerTypeRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return OperationResult<CustomerTypeDto>.Fail("اسم النوع مطلوب.");
        }

        var duplicate = await _db.CustomerTypes.AnyAsync(x => x.Name == name, cancellationToken);
        if (duplicate)
        {
            return OperationResult<CustomerTypeDto>.Fail("هذا النوع موجود مسبقاً.");
        }

        var type = new CustomerType
        {
            Id = UuidV7.New(),
            Name = name,
            IsActive = request.IsActive,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.CustomerTypes.Add(type);
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerTypeDto>.Ok(new CustomerTypeDto(type.Id, type.Name, type.IsActive));
    }

    public async Task<OperationResult<CustomerTypeDto>> UpdateAsync(Guid id, SaveCustomerTypeRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var type = await _db.CustomerTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (type is null)
        {
            return OperationResult<CustomerTypeDto>.Fail("نوع العميل غير موجود.");
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return OperationResult<CustomerTypeDto>.Fail("اسم النوع مطلوب.");
        }

        var duplicate = await _db.CustomerTypes.AnyAsync(x => x.Name == name && x.Id != id, cancellationToken);
        if (duplicate)
        {
            return OperationResult<CustomerTypeDto>.Fail("هذا النوع موجود مسبقاً.");
        }

        type.Name = name;
        type.IsActive = request.IsActive;
        type.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerTypeDto>.Ok(new CustomerTypeDto(type.Id, type.Name, type.IsActive));
    }
}
