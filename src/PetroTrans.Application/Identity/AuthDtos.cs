namespace PetroTrans.Application.Identity;

public sealed record AccountSetupRequest(
    string DisplayName,
    string UserName,
    string Password,
    string ConfirmPassword);

public sealed record SetupRequest(
    AccountSetupRequest Owner,
    AccountSetupRequest Operator);

public sealed record LoginRequest(string UserName, string Password);

public sealed record CurrentUserDto(
    Guid Id,
    string UserName,
    string DisplayName,
    string Locale,
    string Theme,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record AuthStatusDto(
    bool NeedsSetup,
    bool Authenticated,
    CurrentUserDto? User);

public sealed record AuthResult(bool Succeeded, string? Error, CurrentUserDto? User)
{
    public static AuthResult Ok(CurrentUserDto user) => new(true, null, user);
    public static AuthResult Fail(string error) => new(false, error, null);
}
