using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Identity;
using PetroTrans.Domain.Parties;
using PetroTrans.Domain.Settings;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Catalog;

public sealed class CustomerService : ICustomerService
{
    private readonly AppDbContext _db;

    public CustomerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CustomerListItemDto>> ListAsync(string? search, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = includeInactive
            ? _db.Customers.IgnoreQueryFilters().AsNoTracking().Include(x => x.CustomerType).AsQueryable()
            : _db.Customers.AsNoTracking().Include(x => x.CustomerType).AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.Code.Contains(term) || (x.Phone != null && x.Phone.Contains(term)));
        }

        var customers = await query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        if (customers.Count == 0)
        {
            return [];
        }

        var ids = customers.Select(x => x.Id).ToList();
        var ledger = await _db.PartyLedgerEntries.AsNoTracking()
            .Where(x => x.PartyKind == PartyKinds.Customer && ids.Contains(x.PartyId))
            .Select(x => new { x.PartyId, x.SignedAmount })
            .ToListAsync(cancellationToken);
        var outstandingByCustomer = ledger
            .GroupBy(x => x.PartyId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.SignedAmount));

        var invoiceStats = await _db.SalesInvoices.AsNoTracking()
            .Where(x => ids.Contains(x.CustomerId))
            .Select(x => new { x.CustomerId, x.InvoiceDate })
            .ToListAsync(cancellationToken);
        var invoiceByCustomer = invoiceStats
            .GroupBy(x => x.CustomerId)
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Count(), Last: (DateTime?)g.Max(x => x.InvoiceDate)));

        return customers.Select(x =>
        {
            invoiceByCustomer.TryGetValue(x.Id, out var stats);
            outstandingByCustomer.TryGetValue(x.Id, out var outstanding);
            return new CustomerListItemDto(
                x.Id,
                x.Code,
                x.Name,
                x.CustomerType?.Name,
                x.Phone,
                x.IsActive,
                decimal.Round(outstanding, 2, MidpointRounding.AwayFromZero),
                stats.Count,
                stats.Last);
        }).ToList();
    }

    public async Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.CustomerType)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return customer is null ? null : Map(customer);
    }

    public async Task<OperationResult<CustomerDto>> CreateAsync(SaveCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null)
        {
            return OperationResult<CustomerDto>.Fail(error);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var code = await NextCustomerCodeAsync(cancellationToken);
        var customer = new Customer
        {
            Id = UuidV7.New(),
            Code = code,
            Name = request.Name.Trim(),
            CustomerTypeId = request.CustomerTypeId,
            ContactPerson = TrimToNull(request.ContactPerson),
            Phone = TrimToNull(request.Phone),
            WhatsApp = TrimToNull(request.WhatsApp),
            Address = TrimToNull(request.Address),
            IsActive = true,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId
        };
        _db.Customers.Add(customer);
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "customers.create",
            EntityType = "customer",
            EntityId = customer.Id,
            AfterJson = JsonSerializer.Serialize(new { customer.Code, customer.Name })
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<CustomerDto>.Ok((await GetAsync(customer.Id, cancellationToken))!);
    }

    public async Task<OperationResult<CustomerDto>> UpdateAsync(Guid id, SaveCustomerRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerDto>.Fail("العميل غير موجود.");
        }

        var error = await ValidateAsync(request, cancellationToken);
        if (error is not null)
        {
            return OperationResult<CustomerDto>.Fail(error);
        }

        var before = JsonSerializer.Serialize(new { customer.Name, customer.CustomerTypeId, customer.Phone });
        customer.Name = request.Name.Trim();
        customer.CustomerTypeId = request.CustomerTypeId;
        customer.ContactPerson = TrimToNull(request.ContactPerson);
        customer.Phone = TrimToNull(request.Phone);
        customer.WhatsApp = TrimToNull(request.WhatsApp);
        customer.Address = TrimToNull(request.Address);
        customer.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "customers.update",
            EntityType = "customer",
            EntityId = customer.Id,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { customer.Name, customer.CustomerTypeId, customer.Phone })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerDto>.Ok((await GetAsync(customer.Id, cancellationToken))!);
    }

    public async Task<OperationResult<CustomerDto>> ArchiveAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerDto>.Fail("العميل غير موجود.", 404);
        }

        if (!customer.IsActive && customer.DeletedAt is not null)
        {
            return OperationResult<CustomerDto>.Ok(Map(customer));
        }

        var before = JsonSerializer.Serialize(new { customer.IsActive, customer.DeletedAt });
        customer.IsActive = false;
        customer.DeletedAt = DateTime.UtcNow;
        customer.DeletedByUserId = actorUserId;
        customer.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "customers.archive",
            EntityType = "customer",
            EntityId = customer.Id,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { customer.IsActive, customer.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerDto>.Ok(Map(customer));
    }

    public async Task<OperationResult<CustomerDto>> RestoreAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var customer = await _db.Customers.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (customer is null)
        {
            return OperationResult<CustomerDto>.Fail("العميل غير موجود.", 404);
        }

        var before = JsonSerializer.Serialize(new { customer.IsActive, customer.DeletedAt });
        customer.IsActive = true;
        customer.DeletedAt = null;
        customer.DeletedByUserId = null;
        customer.UpdatedByUserId = actorUserId;
        _db.AuditLogs.Add(new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = DateTime.UtcNow,
            UserId = actorUserId,
            Action = "customers.restore",
            EntityType = "customer",
            EntityId = customer.Id,
            BeforeJson = before,
            AfterJson = JsonSerializer.Serialize(new { customer.IsActive, customer.DeletedAt })
        });
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CustomerDto>.Ok(Map(customer));
    }

    private async Task<string?> ValidateAsync(SaveCustomerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "اسم العميل مطلوب.";
        }

        if (request.CustomerTypeId is { } typeId)
        {
            var exists = await _db.CustomerTypes.AnyAsync(x => x.Id == typeId && x.IsActive, cancellationToken);
            if (!exists)
            {
                return "نوع العميل غير صالح.";
            }
        }

        return null;
    }

    private async Task<string> NextCustomerCodeAsync(CancellationToken cancellationToken)
    {
        var series = await _db.NumberSeries
            .SingleAsync(x => x.DocumentType == NumberSeriesTypes.Customer, cancellationToken);
        var code = $"{series.Prefix}-{series.NextValue.ToString().PadLeft(series.Padding, '0')}";
        series.NextValue++;
        return code;
    }

    private static CustomerDto Map(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.CustomerTypeId,
            customer.CustomerType?.Name,
            customer.ContactPerson,
            customer.Phone,
            customer.WhatsApp,
            customer.Address,
            customer.IsActive);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
