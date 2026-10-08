using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PetroTrans.Application.Identity;
using PetroTrans.Domain.Identity;

namespace PetroTrans.Api.Auth;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapGet("/status", (Delegate)GetStatusAsync);
        group.MapPost("/setup", (Delegate)SetupAsync);
        group.MapPost("/login", (Delegate)LoginAsync);
        group.MapPost("/logout", (Delegate)LogoutAsync).RequireAuthorization();
        group.MapGet("/me", (Delegate)GetMeAsync).RequireAuthorization();
    }

    public static void MapAuthorizationCanaries(this WebApplication app)
    {
        app.MapGet("/api/secure/ping", () => Results.Json(new { ok = true }))
            .RequireAuthorization();
        app.MapGet("/api/secure/pricing-override", () => Results.Json(new { ok = true }))
            .RequireAuthorization($"perm:{PermissionCodes.PricingOverride}");
        app.MapGet("/api/secure/roles-manage", () => Results.Json(new { ok = true }))
            .RequireAuthorization($"perm:{PermissionCodes.RolesManage}");
    }

    private static async Task<IResult> GetStatusAsync(
        HttpContext http,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var status = await auth.GetStatusAsync(GetUserId(http.User), cancellationToken);
        return Results.Json(status);
    }

    private static async Task<IResult> SetupAsync(
        SetupRequest request,
        HttpContext http,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.SetupAsync(request, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest);
        }

        await SignInAsync(http, result.User);
        return Results.Json(result.User);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        HttpContext http,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        if (!result.Succeeded || result.User is null)
        {
            return Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status401Unauthorized);
        }

        await SignInAsync(http, result.User);
        return Results.Json(result.User);
    }

    private static async Task<IResult> LogoutAsync(HttpContext http)
    {
        await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Json(new { ok = true });
    }

    private static async Task<IResult> GetMeAsync(
        HttpContext http,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(http.User);
        if (userId is null)
        {
            return Results.Json(new { error = "يجب تسجيل الدخول." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await auth.GetCurrentUserAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Results.Json(new { error = "يجب تسجيل الدخول." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        return Results.Json(user);
    }

    private static async Task SignInAsync(HttpContext http, CurrentUserDto user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName)
        };
        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await http.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true
            });
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
