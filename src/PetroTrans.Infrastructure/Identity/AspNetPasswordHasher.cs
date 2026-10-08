using Microsoft.AspNetCore.Identity;
using PetroTrans.Application.Identity;

namespace PetroTrans.Infrastructure.Identity;

public sealed class AspNetPasswordHasher : Application.Identity.IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string password)
    {
        return _hasher.HashPassword(null!, password);
    }

    public bool Verify(string hash, string password)
    {
        var result = _hasher.VerifyHashedPassword(null!, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
