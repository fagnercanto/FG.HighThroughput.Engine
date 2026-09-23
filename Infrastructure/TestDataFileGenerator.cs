using System.Text;
using FG.HighThroughput.Engine.Core;

namespace FG.HighThroughput.Engine.Infrastructure;

/// <summary>
/// Generates test data files consumable by <see cref="Core.RecordParser"/>, used to
/// stress-test the ingestion pipeline with volumes ranging from 1M to 20M rows.
/// Supports two <see cref="RecordFormat"/>s: a fixed 2-column <c>Simple</c> format and a
/// <c>SpedCompleto</c> format that simulates a real SPED Contábil file (multiple record
/// types with variable column counts).
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

    /// <summary>Number of I250 (partida) detail lines generated per I200 (lançamento) header, in SpedCompleto mode.</summary>
    private const int PartidasPerLancamento = 5;

    /// <summary>
    /// Generates <paramref name="recordCount"/> lines into <paramref name="filePath"/>.
    /// </summary>
    /// <param name="filePath">Destination file path (overwritten if it already exists).</param>
    /// <param name="recordCount">Total number of lines/records to generate.</param>
    /// <param name="format">Line format to generate: <see cref="RecordFormat.Simple"/> or <see cref="RecordFormat.SpedCompleto"/>.</param>
    /// <param name="progress">Optional progress reporter, invoked every <see cref="ProgressReportInterval"/> records.</param>
    /// <param name="cancellationToken">Token used to cancel the generation.</param>
    public async Task GenerateAsync(string filePath, long recordCount, RecordFormat format = RecordFormat.Simple, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (recordCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recordCount), "Record count must be greater than zero.");
        }

        if (format == RecordFormat.SpedCompleto && recordCount < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(recordCount), "SpedCompleto format requires at least 2 records (header + trailer).");
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

        if (format == RecordFormat.Simple)
        {
            await WriteSimpleAsync(writer, recordCount, progress, cancellationToken);
        }
        else
        {
            await WriteSpedCompletoAsync(writer, recordCount, progress, cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);

        progress?.Report(recordCount);
    }

    private static async Task WriteSimpleAsync(StreamWriter writer, long recordCount, IProgress<long>? progress, CancellationToken cancellationToken)
    {
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
    }

    /// <summary>
    /// Writes a simulated SPED Contábil file: one <c>0000</c> header, a mix of
    /// <c>I200</c> (lançamento header) and <c>I250</c> (partida) detail lines, and a final
    /// <c>9999</c> trailer with the detail line count - each with its own column layout.
    /// </summary>
    private static async Task WriteSpedCompletoAsync(StreamWriter writer, long recordCount, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var lineBuilder = new StringBuilder(160);
        var detailLineCount = recordCount - 2;

        // 0000 - file header: version|dateFrom|dateTo|companyName|cnpj|uf
        await writer.WriteLineAsync("0000|1|01012024|31122024|EMPRESA TESTE LTDA|12345678000199|SP".AsMemory(), cancellationToken);

        long entryNumber = 0;
        for (long detailLine = 1; detailLine <= detailLineCount; detailLine++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lineBuilder.Clear();

            var isLancamentoHeader = (detailLine - 1) % PartidasPerLancamento == 0;
            if (isLancamentoHeader)
            {
                entryNumber++;

                // I200 - lançamento header: lineNumber|date|entryNumber|entity|flag
                lineBuilder.Append("I200|").Append(detailLine).Append("|20240115|LCTO").Append(entryNumber).Append("|1|N");
            }
            else
            {
                // I250 - partida (débito/crédito): lineNumber|account|debitCredit|value|complementaryHistory
                var debitCredit = detailLine % 2 == 0 ? 'C' : 'D';
                lineBuilder.Append("I250|").Append(detailLine).Append("|1.01.01.00").Append(detailLine % 10)
                           .Append('|').Append(debitCredit).Append("|1500.00|HISTORICO PADRAO");
            }

            await writer.WriteLineAsync(lineBuilder, cancellationToken);

            if (progress is not null && detailLine % ProgressReportInterval == 0)
            {
                progress.Report(detailLine + 1); // +1 to account for the 0000 header already written.
            }
        }

        // 9999 - file trailer: totalLines
        lineBuilder.Clear();
        lineBuilder.Append("9999|").Append(detailLineCount);
        await writer.WriteLineAsync(lineBuilder, cancellationToken);
    }
}
