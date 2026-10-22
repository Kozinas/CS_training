using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Course.Data;

public static class LedgerDatabase
{
    public static string ConnectionString(string path, bool create = false) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(path),
        Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
        ForeignKeys = true,
        Pooling = false,
        DefaultTimeout = 5
    }.ToString();

    public static DbContextOptions<LedgerDbContext> Options(string path, bool create = false,
        params IInterceptor[] interceptors) => new DbContextOptionsBuilder<LedgerDbContext>()
        .UseSqlite(ConnectionString(path, create)).AddInterceptors(interceptors).Options;

    public static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(ConnectionString(path));
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    public static async Task MigrateAsync(string path, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        await using var db = new LedgerDbContext(Options(path, create: true));
        await db.Database.MigrateAsync(token);
    }

    public static void Backup(string source, string destination)
    {
        using var input = Open(source);
        // Reserve a NEW destination; never overwrite a user's database or backup.
        using (new FileStream(destination, FileMode.CreateNew, FileAccess.Write)) { }
        using var output = Open(destination);
        input.BackupDatabase(output);
    }
}
