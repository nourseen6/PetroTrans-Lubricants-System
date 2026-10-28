using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetroTrans.Infrastructure.Backup;
using PetroTrans.Infrastructure.Persistence;
using Xunit;

namespace PetroTrans.Infrastructure.Tests;

public sealed class BackupManagerTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"petrotrans-backup-{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public BackupManagerTests()
    {
        Directory.CreateDirectory(_folder);
        _dbPath = Path.Combine(_folder, "petrotrans.db");
    }

    private async Task CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(SqlitePaths.GetConnectionString(_dbPath)).Options;
        await using var db = new AppDbContext(options);
        await DatabaseInitializer.InitializeAsync(db);
    }

    [Fact]
    public void CreateBackup_WithoutDatabase_DoesNothing()
    {
        var result = new BackupManager(_dbPath).CreateBackup("startup");
        Assert.False(result.Ok);
        Assert.False(Directory.Exists(Path.Combine(_folder, "Backups")));
    }

    [Fact]
    public async Task CreateBackup_WritesHealthyCopy_AndMirrorsToSecondFolder()
    {
        await CreateDatabaseAsync();
        var second = Path.Combine(_folder, "drive");
        var backups = new BackupManager(_dbPath);
        Assert.Null(backups.SetSecondaryFolder(second));

        var result = backups.CreateBackup("daily");

        Assert.True(result.Ok, result.Error);
        Assert.True(BackupManager.IsHealthy(result.Path!));
        Assert.False(File.Exists(result.Path + "-wal"));
        Assert.False(File.Exists(result.Path + "-shm"));
        var mirrored = Path.Combine(second, BackupManager.SecondarySubfolder, Path.GetFileName(result.Path!));
        Assert.True(File.Exists(mirrored));
        Assert.True(BackupManager.IsHealthy(mirrored));
        var status = backups.GetStatus();
        Assert.NotNull(status.LastSuccessAt);
        Assert.NotNull(status.LastSecondaryAt);
        Assert.Null(status.LastError);
        Assert.Equal("daily", Assert.Single(status.Backups).Reason);
    }

    [Fact]
    public async Task Rotation_KeepsTenNewest_PlusOnePerDayAndMonth()
    {
        await CreateDatabaseAsync();
        var backups = new BackupManager(_dbPath);
        Directory.CreateDirectory(backups.BackupFolder);
        var seed = backups.CreateBackup("manual").Path!;
        // 40 old copies spread over 400 days, two per day.
        for (var day = 1; day <= 200; day += 5)
        {
            foreach (var hour in new[] { 9, 18 })
            {
                var at = DateTime.Today.AddDays(-day).AddHours(hour);
                File.Copy(seed, Path.Combine(backups.BackupFolder, $"petrotrans-{at:yyyyMMdd-HHmmss}-close.db"));
            }
        }

        backups.CreateBackup("manual");

        var kept = backups.ListBackups();
        Assert.True(kept.Count < 82);
        var beyondNewestTen = kept.Skip(10).ToList();
        var recent = beyondNewestTen.Where(x => x.CreatedAt.Date > DateTime.Today.AddDays(-30)).ToList();
        var old = beyondNewestTen.Where(x => x.CreatedAt.Date <= DateTime.Today.AddDays(-30)).ToList();
        Assert.NotEmpty(recent);
        Assert.NotEmpty(old);
        Assert.All(recent.GroupBy(x => x.CreatedAt.Date), day => Assert.Single(day));
        Assert.Equal(old.Count, old.Select(x => (x.CreatedAt.Year, x.CreatedAt.Month)).Distinct().Count());
    }

    [Fact]
    public async Task DamagedLiveDatabase_IsReported_AndOldCopiesAreKept()
    {
        await CreateDatabaseAsync();
        var backups = new BackupManager(_dbPath);
        Assert.True(backups.CreateBackup("startup").Ok);
        SqliteConnection.ClearAllPools();
        File.WriteAllBytes(_dbPath, new byte[4096]);

        var result = backups.CreateBackup("close");

        Assert.False(result.Ok);
        Assert.False(backups.GetStatus().LiveDatabaseOk);
        Assert.Single(backups.ListBackups());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch
        {
            // Temp files are cleaned up by the OS later.
        }
    }
}
