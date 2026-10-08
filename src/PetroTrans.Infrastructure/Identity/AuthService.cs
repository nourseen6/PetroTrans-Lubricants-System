using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Application.Identity;
using PetroTrans.Domain;
using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private const int MinimumPasswordLength = 6;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthStatusDto> GetStatusAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var needsSetup = !await _db.Users.AnyAsync(cancellationToken);
        if (needsSetup)
        {
            return new AuthStatusDto(true, false, null);
        }

        if (userId is null)
        {
            return new AuthStatusDto(false, false, null);
        }

        var user = await GetCurrentUserAsync(userId.Value, cancellationToken);
        return new AuthStatusDto(false, user is not null, user);
    }

    public async Task<AuthResult> SetupAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        if (await _db.Users.AnyAsync(cancellationToken))
        {
            return AuthResult.Fail("تم إعداد المستخدمين مسبقاً.");
        }

        var ownerError = ValidateAccount(request.Owner, "المالك");
        if (ownerError is not null)
        {
            return AuthResult.Fail(ownerError);
        }

        var operatorError = ValidateAccount(request.Operator, "المستخدم الثاني");
        if (operatorError is not null)
        {
            return AuthResult.Fail(operatorError);
        }

        var ownerNormalized = NormalizeUserName(request.Owner.UserName);
        var operatorNormalized = NormalizeUserName(request.Operator.UserName);
        if (ownerNormalized == operatorNormalized)
        {
            return AuthResult.Fail("اسم المستخدم يجب أن يكون مختلفاً لكل حساب.");
        }

        var ownerRole = await RequireRoleAsync(RoleCodes.OwnerManager, cancellationToken);
        var operatorRole = await RequireRoleAsync(RoleCodes.Operator, cancellationToken);
        var utc = DateTime.UtcNow;
        var correlationId = UuidV7.New();

        var owner = CreateUser(request.Owner, utc);
        var operatorUser = CreateUser(request.Operator, utc);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.Users.AddRange(owner, operatorUser);
        _db.UserRoles.AddRange(
            new UserRole { UserId = owner.Id, RoleId = ownerRole.Id },
            new UserRole { UserId = operatorUser.Id, RoleId = operatorRole.Id });
        _db.AuditLogs.AddRange(
            UserCreatedLog(correlationId, owner, RoleCodes.OwnerManager, utc),
            UserCreatedLog(correlationId, operatorUser, RoleCodes.Operator, utc));
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var current = await GetCurrentUserAsync(operatorUser.Id, cancellationToken);
        return current is null
            ? AuthResult.Fail("تعذر إنشاء الحسابات.")
            : AuthResult.Ok(current);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var userName = request.UserName?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        if (userName.Length == 0 || password.Length == 0)
        {
            return AuthResult.Fail("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        var normalized = NormalizeUserName(userName);
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserNameNormalized == normalized, cancellationToken);

        if (user is null
            || user.DeletedAt is not null
            || !user.IsActive
            || !_passwordHasher.Verify(user.PasswordHash, password))
        {
            return AuthResult.Fail("اسم المستخدم أو كلمة المرور غير صحيحة.");
        }

        var current = await GetCurrentUserAsync(user.Id, cancellationToken);
        return current is null
            ? AuthResult.Fail("اسم المستخدم أو كلمة المرور غير صحيحة.")
            : AuthResult.Ok(current);
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .ThenInclude(x => x.RolePermissions)
            .ThenInclude(x => x.Permission)
            .FirstOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = user.UserRoles
            .Select(x => x.Role.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToArray();
        var permissions = user.UserRoles
            .SelectMany(x => x.Role.RolePermissions)
            .Select(x => x.Permission.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToArray();

        return new CurrentUserDto(
            user.Id,
            user.UserName,
            user.DisplayName,
            user.Locale,
            user.Theme,
            roles,
            permissions);
    }

    public async Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        return await _db.UserRoles
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .SelectMany(x => x.Role.RolePermissions)
            .AnyAsync(x => x.Permission.Code == permissionCode, cancellationToken);
    }

    private User CreateUser(AccountSetupRequest request, DateTime utc)
    {
        var userName = request.UserName.Trim();
        return new User
        {
            Id = UuidV7.New(),
            UserName = userName,
            UserNameNormalized = NormalizeUserName(userName),
            PasswordHash = _passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Locale = "ar",
            Theme = "light",
            IsActive = true,
            CreatedAt = utc,
            UpdatedAt = utc,
            RowVersion = 1
        };
    }

    private static AuditLog UserCreatedLog(Guid correlationId, User user, string roleCode, DateTime utc)
    {
        return new AuditLog
        {
            Id = UuidV7.New(),
            OccurredAt = utc,
            UserId = user.Id,
            Action = "users.create",
            EntityType = "user",
            EntityId = user.Id,
            AfterJson = JsonSerializer.Serialize(new
            {
                userName = user.UserName,
                displayName = user.DisplayName,
                role = roleCode
            }),
            CorrelationId = correlationId
        };
    }

    private async Task<Role> RequireRoleAsync(string code, CancellationToken cancellationToken)
    {
        return await _db.Roles.SingleAsync(x => x.Code == code, cancellationToken);
    }

    private static string? ValidateAccount(AccountSetupRequest account, string label)
    {
        if (string.IsNullOrWhiteSpace(account.DisplayName))
        {
            return $"اسم العرض مطلوب لحساب {label}.";
        }

        if (string.IsNullOrWhiteSpace(account.UserName))
        {
            return $"اسم المستخدم مطلوب لحساب {label}.";
        }

        if (string.IsNullOrWhiteSpace(account.Password))
        {
            return $"كلمة المرور مطلوبة لحساب {label}.";
        }

        if (account.Password.Length < MinimumPasswordLength)
        {
            return $"كلمة المرور قصيرة جداً لحساب {label}.";
        }

        if (account.Password != account.ConfirmPassword)
        {
            return $"تأكيد كلمة المرور غير مطابق لحساب {label}.";
        }

        return null;
    }

    private static string NormalizeUserName(string userName)
    {
        return userName.Trim().ToUpperInvariant();
    }
}
