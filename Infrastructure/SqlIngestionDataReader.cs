using FG.HighThroughput.Engine.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace FG.HighThroughput.Engine.Infrastructure;

/// <summary>
/// ADO.NET-only reader for <c>dbo.IngestionData</c>, used exclusively to feed the UI's
/// paginated results table so the user can see - straight from the database, not from
/// in-memory counters - that rows were actually persisted by <see cref="SqlBulkCopyWriter"/>.
/// </summary>
public sealed class SqlIngestionDataReader : IIngestionDataReader
{
    private const string TargetTable = "dbo.IngestionData";

    private readonly string _connectionString;

    public SqlIngestionDataReader(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("HighThroughputDb")
            ?? throw new InvalidOperationException("Connection string 'HighThroughputDb' not found.");
    }

    public async Task<long> GetTotalCountAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT_BIG(*) FROM {TargetTable}";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is long count ? count : Convert.ToInt64(result);
    }

    public async Task<IReadOnlyList<IngestionRecord>> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be >= 1.");
        }

        if (pageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be >= 1.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        // OFFSET/FETCH keeps the query set-based and index-friendly for paging over
        // potentially tens of millions of rows, avoiding client-side buffering of the whole table.
        command.CommandText = $"""
            SELECT Id, RawValue, IngestedAtUtc
            FROM {TargetTable}
            ORDER BY Id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """;

        command.Parameters.AddWithValue("@Offset", (pageNumber - 1) * pageSize);
        command.Parameters.AddWithValue("@PageSize", pageSize);

        var records = new List<IngestionRecord>(pageSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new IngestionRecord(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDateTime(2)));
        }

        return records;
    }

    public async Task TruncateAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"TRUNCATE TABLE {TargetTable};";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
