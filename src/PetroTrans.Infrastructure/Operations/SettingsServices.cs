using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Catalog;
using PetroTrans.Application.Operations;
using PetroTrans.Domain;
using PetroTrans.Domain.Finance;
using PetroTrans.Domain.Identity;
using PetroTrans.Domain.Inventory;
using PetroTrans.Domain.Settings;
using PetroTrans.Infrastructure.Assets;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Operations;

public sealed class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _db;

    public WarehouseService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WarehouseDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.Warehouses.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderBy(x => x.Name)
            .Select(x => new WarehouseDto(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<WarehouseDto>> SaveAsync(Guid? id, SaveNamedLookupRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<WarehouseDto>.Fail("اسم المخزن مطلوب.");
        }

        var name = request.Name.Trim();
        Warehouse warehouse;
        if (id is { } existingId)
        {
            warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? null!;
            if (warehouse is null)
            {
                return OperationResult<WarehouseDto>.Fail("المخزن غير موجود.");
            }

            warehouse.Name = name;
            warehouse.IsActive = request.IsActive;
            warehouse.UpdatedByUserId = actorUserId;
        }
        else
        {
            warehouse = new Warehouse
            {
                Id = UuidV7.New(),
                Name = name,
                IsActive = request.IsActive,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.Warehouses.Add(warehouse);
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "settings.warehouse", "warehouse", warehouse.Id, null, new { warehouse.Name, warehouse.IsActive }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<WarehouseDto>.Ok(new WarehouseDto(warehouse.Id, warehouse.Name, warehouse.IsActive));
    }
}

public sealed class PaymentMethodService : IPaymentMethodService
{
    private readonly AppDbContext _db;

    public PaymentMethodService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentMethodDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _db.PaymentMethods.AsNoTracking().AsQueryable();
        if (activeOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        return await query.OrderBy(x => x.Name)
            .Select(x => new PaymentMethodDto(x.Id, x.Name, x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<PaymentMethodDto>> SaveAsync(Guid? id, SaveNamedLookupRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return OperationResult<PaymentMethodDto>.Fail("اسم طريقة الدفع مطلوب.");
        }

        var name = request.Name.Trim();
        PaymentMethod method;
        if (id is { } existingId)
        {
            method = await _db.PaymentMethods.FirstOrDefaultAsync(x => x.Id == existingId, cancellationToken)
                ?? null!;
            if (method is null)
            {
                return OperationResult<PaymentMethodDto>.Fail("طريقة الدفع غير موجودة.");
            }

            method.Name = name;
            method.IsActive = request.IsActive;
            method.UpdatedByUserId = actorUserId;
        }
        else
        {
            method = new PaymentMethod
            {
                Id = UuidV7.New(),
                Name = name,
                IsActive = request.IsActive,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.PaymentMethods.Add(method);
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "settings.payment_method", "payment_method", method.Id, null, new { method.Name }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<PaymentMethodDto>.Ok(new PaymentMethodDto(method.Id, method.Name, method.IsActive));
    }
}

public sealed class SettingsService : ISettingsService
{
    private readonly AppDbContext _db;
    private readonly SqliteRuntime _sqlite;

    public SettingsService(AppDbContext db, SqliteRuntime sqlite)
    {
        _db = db;
        _sqlite = sqlite;
    }

    public async Task<CompanySettingsDto> GetCompanyAsync(CancellationToken cancellationToken = default)
    {
        var row = await EnsureCompanyAsync(cancellationToken);
        return Map(row);
    }

    public async Task<OperationResult<CompanySettingsDto>> SaveCompanyAsync(SaveCompanySettingsRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return OperationResult<CompanySettingsDto>.Fail("اسم الشركة مطلوب.");
        }

        if (!string.IsNullOrWhiteSpace(request.LogoRelativePath) && !OfficialAssets.IsAllowedLogo(request.LogoRelativePath))
        {
            return OperationResult<CompanySettingsDto>.Fail("الشعار يجب أن يكون من ملفات الأصول الرسمية.");
        }

        var row = await EnsureCompanyAsync(cancellationToken);
        row.CompanyName = request.CompanyName.Trim();
        row.LogoRelativePath = string.IsNullOrWhiteSpace(request.LogoRelativePath) ? null : OfficialAssets.NormalizeRelative(request.LogoRelativePath);
        row.DefaultLocale = request.DefaultLocale is "en" ? "en" : "ar";
        row.UpdatedByUserId = actorUserId;
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<CompanySettingsDto>.Ok(Map(row));
    }

    public async Task<NumberSeriesDto?> GetInvoiceSeriesAsync(CancellationToken cancellationToken = default)
    {
        var row = await _db.NumberSeries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DocumentType == NumberSeriesTypes.SalesInvoice, cancellationToken);
        return row is null ? null : new NumberSeriesDto(row.DocumentType, row.Prefix, row.Padding, row.NextValue);
    }

    public async Task<OperationResult<NumberSeriesDto>> SaveInvoiceSeriesAsync(SaveNumberSeriesRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Prefix) || request.Padding is < 1 or > 8)
        {
            return OperationResult<NumberSeriesDto>.Fail("أدخل بادئة الترقيم وعدد الخانات.");
        }

        var row = await _db.NumberSeries.FirstOrDefaultAsync(x => x.DocumentType == NumberSeriesTypes.SalesInvoice, cancellationToken);
        if (row is null)
        {
            row = new NumberSeries
            {
                Id = UuidV7.New(),
                DocumentType = NumberSeriesTypes.SalesInvoice,
                Prefix = request.Prefix.Trim(),
                Padding = request.Padding,
                NextValue = 1,
                CreatedByUserId = actorUserId,
                UpdatedByUserId = actorUserId
            };
            _db.NumberSeries.Add(row);
        }
        else
        {
            row.Prefix = request.Prefix.Trim();
            row.Padding = request.Padding;
            row.UpdatedByUserId = actorUserId;
        }

        _db.AuditLogs.Add(Audits.Create(actorUserId, "settings.number_series", "number_series", row.Id, null, new { row.Prefix, row.Padding }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<NumberSeriesDto>.Ok(new NumberSeriesDto(row.DocumentType, row.Prefix, row.Padding, row.NextValue));
    }

    public async Task<IReadOnlyList<AuditRowDto>> ListAuditAsync(string? action, CancellationToken cancellationToken = default)
    {
        var query =
            from log in _db.AuditLogs.AsNoTracking()
            join user in _db.Users.IgnoreQueryFilters().AsNoTracking() on log.UserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            select new { log, user };
        if (!string.IsNullOrWhiteSpace(action))
        {
            var term = action.Trim();
            query = query.Where(x => x.log.Action.Contains(term) || (x.log.EntityType != null && x.log.EntityType.Contains(term)));
        }

        return await query
            .OrderByDescending(x => x.log.OccurredAt)
            .Take(300)
            .Select(x => new AuditRowDto(
                x.log.Id,
                x.log.OccurredAt,
                x.user != null ? x.user.DisplayName : null,
                x.log.Action,
                x.log.EntityType,
                x.log.EntityId,
                x.log.BeforeJson,
                x.log.AfterJson))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<string>> BackupAsync(string destinationPath, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return OperationResult<string>.Fail("اختر مسار النسخة الاحتياطية.");
        }

        var dest = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? dest);
        await _db.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(FULL);", cancellationToken);
        File.Copy(_sqlite.DatabasePath, dest, overwrite: true);
        _db.AuditLogs.Add(Audits.Create(actorUserId, "backup.create", "database", null, null, new { dest }));
        await _db.SaveChangesAsync(cancellationToken);
        return OperationResult<string>.Ok(dest);
    }

    public async Task<OperationResult<string>> RestoreAsync(string sourcePath, bool confirm, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (!confirm)
        {
            return OperationResult<string>.Fail("سيتم استبدال البيانات الحالية");
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return OperationResult<string>.Fail("ملف النسخة غير صالح.");
        }

        try
        {
            await using var probe = new SqliteConnection($"Data Source={sourcePath}");
            await probe.OpenAsync(cancellationToken);
            await using var cmd = probe.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='users'";
            if (await cmd.ExecuteScalarAsync(cancellationToken) is null)
            {
                return OperationResult<string>.Fail("ملف النسخة غير صالح.");
            }
        }
        catch
        {
            return OperationResult<string>.Fail("ملف النسخة غير صالح.");
        }

        var live = _sqlite.DatabasePath;
        var next = live + ".new";
        var bak = live + ".bak";
        File.Copy(sourcePath, next, overwrite: true);
        SqliteConnection.ClearAllPools();
        await _db.Database.CloseConnectionAsync();
        DeleteSqliteSidecars(live);
        if (File.Exists(bak))
        {
            File.Delete(bak);
        }

        File.Replace(next, live, bak);
        DeleteSqliteSidecars(live);
        if (File.Exists(bak))
        {
            File.Delete(bak);
        }
        await using var restored = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(SqlitePaths.GetConnectionString(live)).Options);
        restored.AuditLogs.Add(Audits.Create(actorUserId, "backup.restore", "database", null, null, new { sourcePath }));
        await restored.SaveChangesAsync(cancellationToken);
        return OperationResult<string>.Ok(live);
    }

    private async Task<CompanySettings> EnsureCompanyAsync(CancellationToken cancellationToken)
    {
        var row = await _db.CompanySettings.FirstOrDefaultAsync(cancellationToken);
        if (row is not null)
        {
            return row;
        }

        row = new CompanySettings
        {
            Id = UuidV7.New(),
            CompanyName = "Petro Trans",
            DefaultLocale = "ar"
        };
        _db.CompanySettings.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row;
    }

    private static void DeleteSqliteSidecars(string databasePath)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = databasePath + suffix;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }

    private static CompanySettingsDto Map(CompanySettings row)
    {
        return new CompanySettingsDto(
            row.CompanyName,
            row.LogoRelativePath,
            OfficialAssets.ToPublicUrl(row.LogoRelativePath),
            row.DefaultLocale);
    }
}
