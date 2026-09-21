using System.Data;
using FG.HighThroughput.Engine.Core;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FG.HighThroughput.Engine.Infrastructure;

/// <summary>
/// ADO.NET-only bulk writer. Persists batches of ingestion rows (already accumulated in a
/// <see cref="DataTable"/> by the consumer stage) using <see cref="SqlBulkCopy"/> with
/// <see cref="SqlBulkCopyOptions.TableLock"/> enabled to maximize insert throughput by
/// taking a bulk update table-level lock instead of row/page locks. Entity Framework /
/// Dapper are intentionally not used here.
/// </summary>
public sealed class SqlBulkCopyWriter : IBulkDataWriter
{
    private const string TargetTable = "dbo.IngestionData";

    private readonly string _connectionString;
    private readonly ILogger<SqlBulkCopyWriter> _logger;

    public SqlBulkCopyWriter(IConfiguration configuration, ILogger<SqlBulkCopyWriter> logger)
    {
        _connectionString = configuration.GetConnectionString("HighThroughputDb")
            ?? throw new InvalidOperationException("Connection string 'HighThroughputDb' not found.");
        _logger = logger;
    }

    public async Task WriteBatchAsync(DataTable batch, CancellationToken cancellationToken)
    {
        if (batch.Rows.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, externalTransaction: null)
        {
            DestinationTableName = TargetTable,
            BatchSize = batch.Rows.Count,
            BulkCopyTimeout = 0
        };

        bulkCopy.ColumnMappings.Add(nameof(IngestionRecord.Id), "Id");
        bulkCopy.ColumnMappings.Add(nameof(IngestionRecord.RawValue), "RawValue");
        bulkCopy.ColumnMappings.Add(nameof(IngestionRecord.IngestedAtUtc), "IngestedAtUtc");

        try
        {
            // TODO: measure/tune BatchSize, NotifyAfter and add resiliency (retry) once real workload is validated.
            await bulkCopy.WriteToServerAsync(batch, cancellationToken);
        }
        catch (SqlException ex)
        {
            // Typical causes: TableLock contention/timeout, connection dropped, schema mismatch.
            _logger.LogError(ex, "SqlBulkCopy failed writing {Count} rows to {Table} (SQL error {ErrorNumber})", batch.Rows.Count, TargetTable, ex.Number);
            throw;
        }

        _logger.LogInformation("Bulk inserted {Count} records into {Table}", batch.Rows.Count, TargetTable);
    }

    /// <summary>
    /// Builds an empty <see cref="DataTable"/> matching the <c>dbo.IngestionData</c> schema,
    /// ready to be filled row-by-row by the consumer stage of <see cref="Core.IngestionPipeline"/>.
    /// </summary>
    public static DataTable CreateEmptyIngestionTable()
    {
        var table = new DataTable();
        table.Columns.Add(nameof(IngestionRecord.Id), typeof(long));
        table.Columns.Add(nameof(IngestionRecord.RawValue), typeof(string));
        table.Columns.Add(nameof(IngestionRecord.IngestedAtUtc), typeof(DateTime));
        return table;
    }
}
