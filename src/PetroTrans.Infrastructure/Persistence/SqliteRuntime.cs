namespace PetroTrans.Infrastructure.Persistence;

public sealed class SqliteRuntime
{
    public SqliteRuntime(string databasePath)
    {
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }
}
