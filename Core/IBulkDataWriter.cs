namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Abstraction for high-throughput bulk persistence of ingested records.
/// Implementations must avoid row-by-row inserts and rely on set-based bulk operations
/// (e.g. ADO.NET <c>SqlBulkCopy</c>) to sustain millions of rows/minute.
/// </summary>
public interface IBulkDataWriter
{
    /// <summary>
    /// Writes an in-memory <see cref="System.Data.DataTable"/> batch to the destination
    /// store in a single bulk operation.
    /// </summary>
    /// <param name="batch">The in-memory batch of records to persist.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    Task WriteBatchAsync(System.Data.DataTable batch, CancellationToken cancellationToken);
}
