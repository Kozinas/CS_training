using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ClientCore;

public sealed record ExpenseInput(Guid Id, string Date, string Category, decimal Amount, string? Note = null);
public sealed record ImportBatch(Guid BatchId, ExpenseInput[] Rows);
public enum ImportOutcome { Accepted, Conflict }
public interface IImportTransport
{
    Task<ImportOutcome> SendAsync(string token, string payload, CancellationToken cancellationToken);
}

// An outbox scoped to one configured API + account. Tokens are never stored here.
// One queue object per account in a process; server receipts also protect concurrent processes.
public sealed class OfflineQueue
{
    private readonly string connectionString;
    private readonly string account;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public OfflineQueue(string path, string account)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(account);
        this.account = account;
        connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS PendingV1 (
              Account TEXT NOT NULL, BatchId TEXT NOT NULL, Payload TEXT NOT NULL,
              Blocked INTEGER NOT NULL DEFAULT 0 CHECK(Blocked IN (0,1)),
              PRIMARY KEY (Account, BatchId));
            """;
        command.ExecuteNonQuery();
    }

    public void Enqueue(ImportBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        if (batch.BatchId == Guid.Empty || batch.Rows is null || batch.Rows.Length is < 1 or > 100)
            throw new ArgumentException("A nonempty batch contains 1..100 rows.");
        var ids = new HashSet<Guid>();
        foreach (var row in batch.Rows)
        {
            if (row is null || row.Id == Guid.Empty || !ids.Add(row.Id)
                || !DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out _)
                || string.IsNullOrEmpty(row.Category) || row.Category.Length > 32
                || row.Category[0] is < 'a' or > 'z'
                || row.Category.Any(c => !(c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-'))
                || row.Amount is <= 0 or > 999999.99m || decimal.Round(row.Amount, 2) != row.Amount
                || row.Note?.Length > 200)
                throw new ArgumentException("Invalid expense row.");
        }
        // Serializing now detaches the queued snapshot from the caller's mutable array.
        var payload = JsonSerializer.Serialize(batch, Json);
        if (Encoding.UTF8.GetByteCount(payload) > 128 * 1024) throw new ArgumentException("Batch too large.");
        using var db = Open();
        using var transaction = db.BeginTransaction(deferred: false);
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.Parameters.AddWithValue("$account", account);
        command.Parameters.AddWithValue("$id", batch.BatchId.ToString("D"));
        command.CommandText = "SELECT Payload FROM PendingV1 WHERE Account=$account AND BatchId=$id";
        if (command.ExecuteScalar() is string old)
        {
            if (old != payload) throw new InvalidOperationException("BatchId is already bound to another snapshot.");
            return;
        }
        command.CommandText = "SELECT COUNT(*) FROM PendingV1 WHERE Account=$account";
        if ((long)command.ExecuteScalar()! >= 100) throw new InvalidOperationException("Queue is full.");
        command.CommandText = "INSERT INTO PendingV1(Account,BatchId,Payload) VALUES($account,$id,$payload)";
        command.Parameters.AddWithValue("$payload", payload);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public (int Pending, int Blocked) Counts()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT COUNT(*), COALESCE(SUM(Blocked),0) FROM PendingV1 WHERE Account=$account";
        command.Parameters.AddWithValue("$account", account);
        using var reader = command.ExecuteReader();
        reader.Read();
        return (reader.GetInt32(0), reader.GetInt32(1));
    }

    // afterRemoteCommit is a fault-injection seam: no local ack when the process fails here.
    public async Task<int> SyncAsync(string authenticatedAccount, string token, IImportTransport transport,
        CancellationToken cancellationToken, Action? afterRemoteCommit = null)
    {
        if (authenticatedAccount != account) throw new InvalidOperationException("Account changed; use its own queue.");
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var pending = new List<(string Id, string Payload)>();
            using (var db = Open())
            using (var command = db.CreateCommand())
            {
                command.CommandText = "SELECT BatchId,Payload FROM PendingV1 WHERE Account=$account AND Blocked=0 ORDER BY rowid";
                command.Parameters.AddWithValue("$account", account);
                using var reader = command.ExecuteReader();
                while (reader.Read()) pending.Add((reader.GetString(0), reader.GetString(1)));
            }
            var sent = 0;
            foreach (var row in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await transport.SendAsync(token, row.Payload, cancellationToken).ConfigureAwait(false);
                if (result == ImportOutcome.Accepted) afterRemoteCommit?.Invoke();
                using var db = Open();
                using var command = db.CreateCommand();
                command.CommandText = result == ImportOutcome.Accepted
                    ? "DELETE FROM PendingV1 WHERE Account=$account AND BatchId=$id"
                    : "UPDATE PendingV1 SET Blocked=1 WHERE Account=$account AND BatchId=$id";
                command.Parameters.AddWithValue("$account", account);
                command.Parameters.AddWithValue("$id", row.Id);
                command.ExecuteNonQuery();
                if (result == ImportOutcome.Accepted) sent++;
            }
            return sent;
        }
        finally { gate.Release(); }
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(connectionString);
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }
}
