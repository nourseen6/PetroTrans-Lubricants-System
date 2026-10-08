using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using PetroTrans.Api.Assistant;
using PetroTrans.Api.Auth;
using PetroTrans.Api.Catalog;
using PetroTrans.Api.Operations;
using PetroTrans.Api.Sales;
using PetroTrans.Application;
using PetroTrans.Application.Identity;
using PetroTrans.Infrastructure;
using PetroTrans.Infrastructure.Assets;
using PetroTrans.Infrastructure.Persistence;

namespace PetroTrans.Api;

public static class PetroTransApiHost
{
    public const int DefaultPort = 5055;
    public const string CookieName = "PetroTrans.Auth";

    public static WebApplication Create(string[] args, int? port = null, string? sqlitePath = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "HH:mm:ss ";
        });

        var listenPort = port
            ?? builder.Configuration.GetValue("PetroTrans:Port", DefaultPort);

        builder.WebHost.UseWebRoot(ResolveWebRoot(builder.Environment.ContentRootPath));
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, listenPort);
        });

        var databasePath = sqlitePath ?? SqlitePaths.GetDefaultDatabasePath();
        var connectionString = SqlitePaths.GetConnectionString(databasePath);
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(connectionString, databasePath);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new FlexibleGuidConverter());
            options.SerializerOptions.Converters.Add(new FlexibleNullableGuidConverter());
        });

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.Path = "/";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context => WriteApiStatus(context.Response, StatusCodes.Status401Unauthorized, "يجب تسجيل الدخول."),
                    OnRedirectToAccessDenied = context => WriteApiStatus(context.Response, StatusCodes.Status403Forbidden, "غير مسموح.")
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            foreach (var code in PermissionCatalog.All)
            {
                options.AddPolicy($"perm:{code}", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.Requirements.Add(new PermissionRequirement(code));
                });
            }
        });

        var app = builder.Build();

        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var error = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
                WriteErrorLog(error);
                var (status, message) = MapUnhandled(error);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync($"{{\"error\":\"{EscapeJson(message)}\"}}");
            });
        });

        app.UseStaticFiles();
        var officialAssets = OfficialAssets.FindRoot();
        if (officialAssets is not null)
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(officialAssets),
                RequestPath = OfficialAssets.RequestPath
            });
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/api/health", () => Results.Json(new
        {
            status = "ok",
            utc = DateTime.UtcNow.ToString("O"),
            loopbackOnly = true
        }));

        app.MapAuthEndpoints();
        app.MapAuthorizationCanaries();
        app.MapCatalogEndpoints();
        app.MapSalesEndpoints();
        app.MapOperationsEndpoints();
        app.MapAssistantEndpoints();
        app.Map("/api/{**rest}", () => Results.Json(new { error = "غير موجود." }, statusCode: StatusCodes.Status404NotFound));
        app.MapFallbackToFile("index.html");

        return app;
    }

    private static Task WriteApiStatus(HttpResponse response, int statusCode, string message)
    {
        response.StatusCode = statusCode;
        response.ContentType = "application/json; charset=utf-8";
        return response.WriteAsync($"{{\"error\":\"{message}\"}}");
    }

    private static (int Status, string Message) MapUnhandled(Exception? error)
    {
        var current = error;
        while (current is not null)
        {
            if (current is BadHttpRequestException or JsonException)
            {
                return (StatusCodes.Status400BadRequest, "من فضلك أدخل البيانات المطلوبة.");
            }

            if (current is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
            {
                return (StatusCodes.Status409Conflict, "الفاتورة اتعدلت من شاشة تانية. اقفلها وافتحها من جديد.");
            }

            if (current.GetType().Name.Contains("SqliteException", StringComparison.Ordinal) &&
                current.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return (StatusCodes.Status400BadRequest, "لا يمكن حفظ الفاتورة بهذا الرقم لأنه مستخدم.");
            }

            current = current.InnerException;
        }

        return (StatusCodes.Status500InternalServerError, "حدث خطأ غير متوقع.");
    }

    private static void WriteErrorLog(Exception? error)
    {
        if (error is null)
        {
            return;
        }

        try
        {
            var folder = Path.GetDirectoryName(SqlitePaths.GetDefaultDatabasePath());
            if (string.IsNullOrWhiteSpace(folder))
            {
                return;
            }

            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, "error.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not hide the original failure.
        }
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string ResolveWebRoot(string contentRoot)
    {
        var fromBase = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(fromBase))
        {
            return fromBase;
        }

        var fromContent = Path.Combine(contentRoot, "wwwroot");
        if (Directory.Exists(fromContent))
        {
            return fromContent;
        }

        return fromBase;
    }

    public static async Task InitializeDatabaseAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.InitializeAsync(db, cancellationToken);
    }
}
