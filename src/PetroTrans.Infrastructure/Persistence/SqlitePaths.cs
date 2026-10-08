namespace PetroTrans.Infrastructure.Persistence;

public static class SqlitePaths
{
    public static string GetDefaultDatabasePath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PetroTrans");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "petrotrans.db");
    }

    public static string GetConnectionString(string? databasePath = null)
    {
        var path = databasePath ?? GetDefaultDatabasePath();
        return $"Data Source={path}";
    }
}
