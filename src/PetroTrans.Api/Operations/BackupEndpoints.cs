using PetroTrans.Domain.Identity;
using PetroTrans.Infrastructure.Backup;

namespace PetroTrans.Api.Operations;

public static class BackupEndpoints
{
    public static void MapBackupEndpoints(this WebApplication app)
    {
        // Every signed-in user sees the status, so the home screen can warn when backups stop.
        app.MapGet("/api/backup/status", (BackupManager backups) => Results.Json(backups.GetStatus())).RequireAuthorization();
        app.MapPost("/api/backup/now", (Delegate)BackupNow).RequireAuthorization($"perm:{PermissionCodes.BackupCreate}");
        app.MapPut("/api/backup/settings", (Delegate)SaveSettings).RequireAuthorization($"perm:{PermissionCodes.BackupCreate}");
    }

    private static IResult BackupNow(BackupManager backups)
    {
        var result = backups.CreateBackup("manual");
        return result.Ok
            ? Results.Json(backups.GetStatus())
            : Results.Json(new { error = result.Error }, statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult SaveSettings(BackupSettingsRequest request, BackupManager backups)
    {
        var error = backups.SetSecondaryFolder(request.SecondaryFolder);
        if (error is not null)
        {
            return Results.Json(new { error }, statusCode: StatusCodes.Status400BadRequest);
        }

        if (!string.IsNullOrWhiteSpace(request.SecondaryFolder))
        {
            backups.CreateBackup("manual");
        }

        return Results.Json(backups.GetStatus());
    }

    private sealed record BackupSettingsRequest(string? SecondaryFolder);
}

/// <summary>Takes a backup once a day while the app is open, and one more when it closes.</summary>
public sealed class BackupScheduler : BackgroundService
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DailyAge = TimeSpan.FromHours(24);
    private readonly BackupManager _backups;

    public BackupScheduler(BackupManager backups)
    {
        _backups = backups;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckEvery);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            if (!_backups.LastBackupIsRecent(DailyAge))
            {
                _backups.CreateBackup("daily");
            }
        }
    }
}
