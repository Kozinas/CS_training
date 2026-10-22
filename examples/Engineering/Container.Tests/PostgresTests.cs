using Npgsql;
using Testcontainers.PostgreSql;

namespace Course.ContainerTests;

public sealed class PostgresTests
{
    // Explicitly opt-in: real Docker engine and image download are required.
    [Fact(Timeout = 120000)]
    public async Task Duplicate_key_is_rejected_by_the_real_database()
    {
        const string image = "postgres:17.6-alpine@sha256:ef257d85f76e48da1c64832459b59fcaba1a4dac97bf5d7450c77753542eee94";
        await using var database = new PostgreSqlBuilder(image).Build();
        CancellationToken token = TestContext.Current.CancellationToken;
        await database.StartAsync(token);
        await using var connection = new NpgsqlConnection(database.GetConnectionString());
        await connection.OpenAsync(token);
        await using (var schema = new NpgsqlCommand("CREATE TABLE expense (id uuid PRIMARY KEY)", connection))
            await schema.ExecuteNonQueryAsync(token);
        var id = Guid.NewGuid();
        await using var insert = new NpgsqlCommand("INSERT INTO expense (id) VALUES (@id)", connection);
        insert.Parameters.AddWithValue("id", id);
        Assert.Equal(1, await insert.ExecuteNonQueryAsync(token));
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync(token));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
        await using var count = new NpgsqlCommand("SELECT count(*) FROM expense", connection);
        Assert.Equal(1L, await count.ExecuteScalarAsync(token));
    }
}
