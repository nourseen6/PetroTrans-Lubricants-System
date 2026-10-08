namespace PetroTrans.Application.Identity;

public interface IAuthService
{
    Task<AuthStatusDto> GetStatusAsync(Guid? userId, CancellationToken cancellationToken = default);
    Task<AuthResult> SetupAsync(SetupRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(Guid userId, string permissionCode, CancellationToken cancellationToken = default);
}
