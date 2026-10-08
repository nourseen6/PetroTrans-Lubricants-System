using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PetroTrans.Application.Identity;

namespace PetroTrans.Api.Auth;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string code)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAuthService _auth;

    public PermissionAuthorizationHandler(IAuthService auth)
    {
        _auth = auth;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var raw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var userId))
        {
            return;
        }

        if (await _auth.HasPermissionAsync(userId, requirement.Code))
        {
            context.Succeed(requirement);
        }
    }
}
