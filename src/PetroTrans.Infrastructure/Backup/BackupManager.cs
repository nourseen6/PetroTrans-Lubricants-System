using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace PetroTrans.Infrastructure.Backup;

public sealed record BackupFile(string FileName, string FullPath, DateTime CreatedAt, long SizeBytes, string Reason);

public sealed record BackupStatus(
    DateTime? LastSuccessAt,
    string? LastError,
    string? SecondaryFolder,
    DateTime? LastSecondaryAt,
    string? LastSecondaryError,
    bool LiveDatabaseOk,
    string BackupFolder,
    string? SuggestedSecondaryFolder,
    IReadOnlyList<BackupFile> Backups);

public sealed record BackupResult(bool Ok, string? Path, string? Error);

/// <summary>
/// Copies the live SQLite file with the SQLite online backup API, checks every copy,
/// keeps a rolling history and mirrors each copy to a second folder (USB or Google Drive).
/// </summary>
public sealed class BackupManager
{
    public const string SecondarySubfolder = "PetroTrans-Backups";
    private const string FilePrefix = "petrotrans-";
    private const string StampFormat = "yyyyMMdd-HHmmss";

    private readonly object _gate = new();
    private readonly string _databasePath;
    private readonly string _dataFolder;

    public BackupManager(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
        _dataFolder = Path.GetDirectoryName(_databasePath) ?? Directory.GetCurrentDirectory();
        BackupFolder = Path.Combine(_dataFolder, "Backups");
    }

    public string BackupFolder { get; }

    private string SettingsPath => Path.Combine(_dataFolder, "backup-settings.json");
    private string StatePath => Path.Combine(_dataFolder, "backup-status.json");

    public BackupResult CreateBackup(string reason)
    {
        lock (_gate)
        {
            var state = ReadState();
            if (!File.Exists(_databasePath))
            {
                return new BackupResult(false, null, "لا توجد قاعدة بيانات بعد.");
            }

            // A damaged live file must never push good copies out of the rotation.
            if (!IsHealthy(_databasePath))
            {
                state.LiveDatabaseOk = false;
                state.LastError = "ملف البيانات به مشكلة. لم يتم أخذ نسخة حتى لا تُمسح النسخ السليمة. استرجع آخر نسخة سليمة.";
                WriteState(state);
                return new BackupResult(false, null, state.LastError);
            }

            state.LiveDatabaseOk = true;
            string dest;
            try
            {
                Directory.CreateDirectory(BackupFolder);
                var baseName = $"{FilePrefix}{DateTime.Now.ToString(StampFormat, CultureInfo.InvariantCulture)}-{Sanitize(reason)}";
                dest = Path.Combine(BackupFolder, $"{baseName}.db");
                for (var n = 2; File.Exists(dest); n++)
                {
                    dest = Path.Combine(BackupFolder, $"{baseName}-{n}.db");
                }

                CopyDatabase(_databasePath, dest);
                if (!IsHealthy(dest))
                {
                    TryDelete(dest);
                    throw new InvalidOperationException("النسخة الاحتياطية الجديدة لم تنجح في الفحص.");
                }
            }
            catch (Exception ex)
            {
                state.LastError = $"فشل أخذ النسخة الاحتياطية: {ex.Message}";
                WriteState(state);
                return new BackupResult(false, null, state.LastError);
            }

            state.LastSuccessAt = DateTime.Now;
            state.LastError = null;
            Rotate(BackupFolder);
            MirrorToSecondary(dest, state);
            WriteState(state);
            return new BackupResult(true, dest, null);
        }
    }

    /// <summary>Copies the live database to a path the user chose.</summary>
    public BackupResult CopyTo(string destinationPath)
    {
        lock (_gate)
        {
            var dest = Path.GetFullPath(destinationPath);
            Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? dest);
            CopyDatabase(_databasePath, dest);
            return IsHealthy(dest)
                ? new BackupResult(true, dest, null)
                : new BackupResult(false, null, "النسخة لم تنجح في الفحص.");
        }
    }

    public bool LastBackupIsRecent(TimeSpan maxAge)
    {
        var last = ReadState().LastSuccessAt;
        return last is { } at && DateTime.Now - at < maxAge;
    }

    public BackupStatus GetStatus()
    {
        var state = ReadState();
        var settings = ReadSettings();
        return new BackupStatus(
            state.LastSuccessAt,
            state.LastError,
            settings.SecondaryFolder,
            state.LastSecondaryAt,
            state.LastSecondaryError,
            state.LiveDatabaseOk,
            BackupFolder,
            string.IsNullOrWhiteSpace(settings.SecondaryFolder) ? DetectGoogleDrive() : null,
            ListBackups());
    }

    public string? SetSecondaryFolder(string? folder)
    {
        var value = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();
        if (value is not null)
        {
            try
            {
                var full = Path.GetFullPath(value);
                Directory.CreateDirectory(Path.Combine(full, SecondarySubfolder));
                value = full;
            }
            catch (Exception ex)
            {
                return $"تعذر استخدام هذا المكان: {ex.Message}";
            }
        }

        lock (_gate)
        {
            WriteJson(SettingsPath, new BackupSettings { SecondaryFolder = value });
        }

        return null;
    }

    public bool IsBackupFile(string path)
    {
        var full = Path.GetFullPath(path);
        return File.Exists(full) && IsHealthy(full);
    }

    public IReadOnlyList<BackupFile> ListBackups()
    {
        if (!Directory.Exists(BackupFolder))
        {
            return [];
        }

        return Directory.EnumerateFiles(BackupFolder, $"{FilePrefix}*.db")
            .Select(ToBackupFile)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
    }

    public static bool IsHealthy(string path)
    {
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            connection.Open();
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA quick_check;";
            if (!string.Equals(check.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            using var users = connection.CreateCommand();
            users.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='users'";
            return Convert.ToInt64(users.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDatabase(string source, string destination)
    {
        using var from = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Pooling = false }.ToString());
        using var to = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = destination, Pooling = false }.ToString());
        from.Open();
        to.Open();
        from.BackupDatabase(to);

        // The live file uses WAL; a backup must be one self-contained file with no -wal/-shm beside it.
        using var journal = to.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode=DELETE;";
        journal.ExecuteNonQuery();
    }

    private void MirrorToSecondary(string backupPath, BackupState state)
    {
        var secondary = ReadSettings().SecondaryFolder;
        if (string.IsNullOrWhiteSpace(secondary))
        {
            state.LastSecondaryError = null;
            return;
        }

        try
        {
            var folder = Path.Combine(secondary, SecondarySubfolder);
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, Path.GetFileName(backupPath));
            var temp = target + ".tmp";
            File.Copy(backupPath, temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
            Rotate(folder);
            state.LastSecondaryAt = DateTime.Now;
            state.LastSecondaryError = null;
        }
        catch (Exception ex)
        {
            state.LastSecondaryError = $"لم يتم نسخ النسخة إلى المكان الثاني ({secondary}): {ex.Message}";
        }
    }

    /// <summary>
    /// Keeps the 10 newest copies, the newest copy of each of the last 30 days,
    /// and the newest copy of each of the last 12 months.
    /// </summary>
    private static void Rotate(string folder)
    {
        var files = Directory.EnumerateFiles(folder, $"{FilePrefix}*.db")
            .Select(ToBackupFile)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();

        var keep = new HashSet<string>(files.Take(10).Select(x => x.FullPath), StringComparer.OrdinalIgnoreCase);
        var today = DateTime.Today;
        foreach (var day in files.Where(x => x.CreatedAt.Date > today.AddDays(-30)).GroupBy(x => x.CreatedAt.Date))
        {
            keep.Add(day.First().FullPath);
        }

        var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        foreach (var month in files.Where(x => x.CreatedAt >= monthStart).GroupBy(x => (x.CreatedAt.Year, x.CreatedAt.Month)))
        {
            keep.Add(month.First().FullPath);
        }

        foreach (var file in files.Where(x => !keep.Contains(x.FullPath)))
        {
            TryDelete(file.FullPath);
        }
    }

    private static BackupFile? ToBackupFile(string path)
    {
        // Windows matches "*.db" against ".db-shm" and ".db-wal" too, so check the extension exactly.
        if (!string.Equals(Path.GetExtension(path), ".db", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(path);
        if (!name.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase) || name.Length < FilePrefix.Length + StampFormat.Length)
        {
            return null;
        }

        var stamp = name.Substring(FilePrefix.Length, StampFormat.Length);
        if (!DateTime.TryParseExact(stamp, StampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var created))
        {
            return null;
        }

        var reason = name.Length > FilePrefix.Length + StampFormat.Length + 1
            ? name[(FilePrefix.Length + StampFormat.Length + 1)..]
            : string.Empty;
        return new BackupFile(Path.GetFileName(path), path, created, new FileInfo(path).Length, reason);
    }

    private static string? DetectGoogleDrive()
    {
        var candidates = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady)
                {
                    candidates.Add(Path.Combine(drive.RootDirectory.FullName, "My Drive"));
                    candidates.Add(Path.Combine(drive.RootDirectory.FullName, "ملفاتي"));
                }
            }
            catch
            {
                // A drive that cannot be read is simply skipped.
            }
        }

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(profile, "Google Drive"));
        candidates.Add(Path.Combine(profile, "My Drive"));
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string Sanitize(string reason)
    {
        var clean = new string((reason ?? "manual").Where(c => char.IsAsciiLetterOrDigit(c) || c == '-').ToArray());
        return clean.Length == 0 ? "manual" : clean;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // A locked old copy is removed on a later rotation.
        }
    }

    private BackupState ReadState() => ReadJson<BackupState>(StatePath) ?? new BackupState();

    private void WriteState(BackupState state) => WriteJson(StatePath, state);

    private BackupSettings ReadSettings() => ReadJson<BackupSettings>(SettingsPath) ?? new BackupSettings();

    private static T? ReadJson<T>(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default;
        }
        catch
        {
            return default;
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        try
        {
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // Status is informational; a failed write must not break the app.
        }
    }

    private sealed class BackupSettings
    {
        public string? SecondaryFolder { get; set; }
    }

    private sealed class BackupState
    {
        public DateTime? LastSuccessAt { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastSecondaryAt { get; set; }
        public string? LastSecondaryError { get; set; }
        public bool LiveDatabaseOk { get; set; } = true;
    }
}
