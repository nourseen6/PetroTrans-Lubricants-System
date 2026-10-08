using System.Security.Claims;

namespace PetroTrans.Api;

public static class CurrentUser
{
    public static Guid RequireId(HttpContext http)
    {
        var raw = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(raw, out var id))
        {
            throw new InvalidOperationException("Authenticated user id is missing.");
        }

        return id;
    }

    public static string DisplayName(HttpContext http)
    {
        return http.User.FindFirstValue("display_name")
            ?? http.User.Identity?.Name
            ?? string.Empty;
    }

    public static string UserName(HttpContext http)
    {
        return http.User.Identity?.Name ?? string.Empty;
    }
}
