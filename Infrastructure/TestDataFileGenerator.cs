using System.Text;

namespace FG.HighThroughput.Engine.Infrastructure;

/// <summary>
/// Generates a pipe-delimited (<c>Id|RawValue</c>) test data file compatible with
/// <see cref="Core.RecordParser"/>, used to stress-test the ingestion pipeline with
/// volumes ranging from 1M to 20M rows.
/// </summary>
/// <remarks>
/// Uses a buffered <see cref="StreamWriter"/> and writes lines via a plain
/// <see cref="StringBuilder"/> to keep throughput high for tens of millions of rows,
/// avoiding per-line synchronous flushes.
/// </remarks>
public sealed class TestDataFileGenerator
{
    private const int BufferSize = 1024 * 1024; // 1 MB write buffer.
    private const int ProgressReportInterval = 100_000;

    /// <summary>
    /// Generates <paramref name="recordCount"/> lines into <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Destination file path (overwritten if it already exists).</param>
    /// <param name="recordCount">Total number of lines/records to generate.</param>
    /// <param name="progress">Optional progress reporter, invoked every <see cref="ProgressReportInterval"/> records.</param>
    /// <param name="cancellationToken">Token used to cancel the generation.</param>
    public async Task GenerateAsync(string filePath, long recordCount, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (recordCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordCount), "Record count must be greater than zero.");
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: BufferSize,
            useAsync: true);

        await using var writer = new StreamWriter(fileStream, Encoding.UTF8, BufferSize);

        var lineBuilder = new StringBuilder(128);

        for (long id = 1; id <= recordCount; id++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineBuilder.Clear();
            lineBuilder.Append(id)
                       .Append('|')
                       .Append("sample-payload-")
                       .Append(id);

            await writer.WriteLineAsync(lineBuilder, cancellationToken);

            if (progress is not null && id % ProgressReportInterval == 0)
            {
                progress.Report(id);
            }
        }

        await writer.FlushAsync(cancellationToken);

        progress?.Report(recordCount);
    }
}
