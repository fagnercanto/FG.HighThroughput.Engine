namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Abstraction for reading back persisted ingestion records, used to let the user
/// visually confirm (via a paginated UI table) that <see cref="IBulkDataWriter"/> actually
/// persisted the data, instead of trusting only in-memory progress counters.
/// </summary>
public interface IIngestionDataReader
{
    /// <summary>Total number of rows currently stored in the destination table.</summary>
    Task<long> GetTotalCountAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns a single page of records ordered by <see cref="IngestionRecord.Id"/>.
    /// </summary>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="pageSize">Number of rows per page.</param>
    Task<IReadOnlyList<IngestionRecord>> GetPageAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Empties the destination table via <c>TRUNCATE TABLE</c> (metadata/page deallocation,
    /// not a row-by-row delete), so the user can reset the demo and start from a clean table
    /// without restarting the application.
    /// </summary>
    Task TruncateAsync(CancellationToken cancellationToken);
}
