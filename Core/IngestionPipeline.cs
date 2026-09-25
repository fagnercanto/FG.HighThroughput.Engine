using System.Data;
using System.Threading.Channels;
using FG.HighThroughput.Engine.Infrastructure;
using Microsoft.Extensions.Logging;

namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Producer/Consumer pipeline built on top of a bounded <see cref="Channel{T}"/>.
/// The bounded capacity (10,000 items) provides natural backpressure: if the consumer
/// (bulk writer) falls behind, the producer (file reader/parser) is suspended instead of
/// buffering unbounded amounts of data in memory.
/// </summary>
public sealed class IngestionPipeline
{
    private const int ProgressReportInterval = 50_000;

    private readonly Channel<IngestionRecord> _channel;
    private readonly IBulkDataWriter _bulkDataWriter;
    private readonly ILogger<IngestionPipeline> _logger;

    /// <summary>
    /// Number of rows accumulated in the in-memory <see cref="DataTable"/> before it is
    /// flushed to <see cref="IBulkDataWriter"/> via <see cref="SqlBulkCopyWriter"/>.
    /// </summary>
    public int FlushThreshold { get; init; } = 50_000;

    public IngestionPipeline(IBulkDataWriter bulkDataWriter, ILogger<IngestionPipeline> logger)
    {
        _bulkDataWriter = bulkDataWriter;
        _logger = logger;
        _channel = Channel.CreateBounded<IngestionRecord>(new BoundedChannelOptions(10_000)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Producer: reads a delimited text file line by line via <see cref="StreamReader"/>,
    /// parses each line with <see cref="RecordParser"/> (zero-allocation tokenization) and
    /// publishes the resulting record into the bounded channel. Awaits (suspends) when the
    /// channel is full, applying backpressure upstream to the file reader itself.
    /// </summary>
    public async Task ProduceFromFileAsync(string filePath, RecordFormat format = RecordFormat.Simple, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        var lineNumber = 0;
        var skippedLines = 0;
        Exception? failure = null;

        try
        {
            using var streamReader = new StreamReader(filePath);

            string? line;
            while ((line = await streamReader.ReadLineAsync(cancellationToken)) is not null)
            {
                lineNumber++;

                if (line.Length == 0)
                {
                    continue;
                }

                IngestionRecord record;
                try
                {
                    record = format == RecordFormat.Simple
                        ? RecordParser.ParseLine(line)
                        : RecordParser.ParseSpedLine(lineNumber, line);
                }
                catch (FormatException ex)
                {
                    // A single malformed line must not abort the ingestion of 20M+ rows.
                    // Log with enough context to locate the offending line and move on.
                    skippedLines++;
                    _logger.LogWarning(ex, "Skipping malformed line {LineNumber} in {FilePath}", lineNumber, filePath);
                    continue;
                }

                await _channel.Writer.WriteAsync(record, cancellationToken);

                if (progress is not null && lineNumber % ProgressReportInterval == 0)
                {
                    progress.Report(lineNumber);
                }
            }
        }
        catch (IOException ex)
        {
            // File-level failures (missing file, locked file, disk error) are fatal for this run.
            _logger.LogError(ex, "Failed to read ingestion file {FilePath} after {LineNumber} lines", filePath, lineNumber);
            failure = ex;
            throw;
        }
        finally
        {
            // Completing with the failure (when there is one) lets the consumer's
            // ReadAllAsync surface the real cause instead of just ending as if the file
            // had been fully read.
            _channel.Writer.TryComplete(failure);
        }

        progress?.Report(lineNumber);

        if (skippedLines > 0)
        {
            _logger.LogWarning("Finished reading {FilePath}: {SkippedLines} malformed line(s) skipped out of {LineNumber}", filePath, skippedLines, lineNumber);
        }
    }

    /// <summary>
    /// Consumer: drains the channel, accumulating rows into a local in-memory
    /// <see cref="DataTable"/> (schema matching <c>dbo.IngestionData</c>). Once the table
    /// reaches <see cref="FlushThreshold"/> rows, it is handed off to the
    /// <see cref="IBulkDataWriter"/> (<see cref="SqlBulkCopyWriter"/>) for a bulk insert,
    /// then cleared for reuse. TODO: add retry/backoff and metrics.
    /// </summary>
    public async Task ConsumeAsync(IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        var table = SqlBulkCopyWriter.CreateEmptyIngestionTable();
        var totalInserted = 0L;

        try
        {
            await foreach (var record in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                table.Rows.Add(record.Id, record.RawValue, record.IngestedAtUtc);

                if (table.Rows.Count >= FlushThreshold)
                {
                    totalInserted += table.Rows.Count;
                    await FlushAsync(table, cancellationToken);
                    progress?.Report(totalInserted);
                }
            }

            if (table.Rows.Count > 0)
            {
                totalInserted += table.Rows.Count;
                await FlushAsync(table, cancellationToken);
                progress?.Report(totalInserted);
            }
        }
        catch (Exception ex)
        {
            // If the consumer stops (e.g. a bulk-copy failure such as a duplicate-key
            // violation), nothing else was telling the channel writer to stop accepting
            // items. That left the producer's WriteAsync suspended forever waiting for
            // buffer space that would never be freed again - a silent deadlock that looked
            // like the ingestion was just "stuck". Completing the writer with the exception
            // here makes any pending/future WriteAsync throw immediately instead of hanging,
            // so the producer task also faults and Task.WhenAll returns.
            _channel.Writer.TryComplete(ex);
            throw;
        }
    }

    private async Task FlushAsync(DataTable table, CancellationToken cancellationToken)
    {
        var rowCount = table.Rows.Count;
        _logger.LogInformation("Flushing batch of {Count} records", rowCount);

        try
        {
            await _bulkDataWriter.WriteBatchAsync(table, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed bulk flush means up to FlushThreshold rows were not persisted.
            // Log with enough context for the operator to decide whether to retry the file.
            _logger.LogError(ex, "Failed to flush batch of {Count} records to the destination store", rowCount);
            throw;
        }

        table.Clear();
    }
}
