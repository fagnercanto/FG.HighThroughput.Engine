namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Zero-allocation, forward-only tokenizer for delimited ingestion lines.
/// Being a <c>ref struct</c>, it can only live on the stack and operate directly over a
/// <see cref="ReadOnlySpan{Char}"/> slice of the source buffer, avoiding substring allocations
/// on the managed heap while walking through 20M+ lines.
/// </summary>
/// <remarks>
/// This is intentionally a skeleton: field mapping / column count / validation rules
/// will be implemented once the real file layout is defined.
/// </remarks>
public ref struct RecordParser
{
    private ReadOnlySpan<char> _remaining;
    private readonly char _delimiter;

    public RecordParser(ReadOnlySpan<char> line, char delimiter = '|')
    {
        _remaining = line;
        _delimiter = delimiter;
    }

    /// <summary>
    /// Returns <c>true</c> while there are more fields to read, advancing the internal cursor
    /// without allocating. The caller receives a span pointing into the original buffer.
    /// </summary>
    public bool TryReadNextField(out ReadOnlySpan<char> field)
    {
        if (_remaining.IsEmpty)
        {
            field = default;
            return false;
        }

        var separatorIndex = _remaining.IndexOf(_delimiter);
        if (separatorIndex < 0)
        {
            field = _remaining;
            _remaining = ReadOnlySpan<char>.Empty;
            return true;
        }

        field = _remaining[..separatorIndex];
        _remaining = _remaining[(separatorIndex + 1)..];
        return true;
    }

    /// <summary>
    /// Parses a single pipe-delimited line (e.g. "123|Some raw text") into an
    /// <see cref="IngestionRecord"/>. Tokenization happens entirely over
    /// <see cref="ReadOnlySpan{Char}"/> slices - no substrings are created while walking
    /// the line. <c>Id</c> is parsed directly from its span via <see cref="long.Parse(ReadOnlySpan{char})"/>.
    /// The only unavoidable allocation is the final <c>RawValue.ToString()</c>, since the
    /// value must become a managed <see cref="string"/> to be stored/transported downstream.
    /// </summary>
    public static IngestionRecord ParseLine(ReadOnlySpan<char> line, char delimiter = '|')
    {
        var parser = new RecordParser(line, delimiter);

        if (!parser.TryReadNextField(out var idField))
        {
            throw new FormatException("Line does not contain an Id column.");
        }

        var id = long.Parse(idField);

        parser.TryReadNextField(out var rawValueField);

        return new IngestionRecord(id, rawValueField.ToString(), DateTime.UtcNow);
    }

    /// <summary>
    /// Parses a single line of a simulated SPED Contábil (ECD) file. Unlike
    /// <see cref="ParseLine"/>, the number of columns is <b>not fixed</b>: it depends on the
    /// record code, which is always the first field (e.g. <c>0000</c>, <c>I200</c>,
    /// <c>I250</c>, <c>9999</c>). The dispatch below uses C#'s native pattern matching over
    /// <see cref="ReadOnlySpan{Char}"/> (no allocation, no <c>ToString()</c> needed just to
    /// compare the record code).
    /// </summary>
    /// <remarks>
    /// For simplicity, this POC does not include the leading/trailing pipe used by the real
    /// SPED layout (e.g. real files look like <c>|0000|...|</c>); lines are expected as
    /// <c>0000|...</c>.
    /// </remarks>
    /// <param name="id">Sequential id assigned by the caller (SPED lines have no inherent numeric id).</param>
    /// <param name="line">The raw line to parse.</param>
    public static IngestionRecord ParseSpedLine(long id, ReadOnlySpan<char> line)
    {
        var parser = new RecordParser(line, delimiter: '|');

        if (!parser.TryReadNextField(out var recordCode))
        {
            throw new FormatException("SPED line does not contain a record code.");
        }

        // Dispatch by record code only to validate that the line has the expected column
        // layout for its type (throws FormatException on malformed lines). The persisted
        // RawValue is always the full original line - not a per-record summary - so the UI
        // can display exactly what was read from the file (e.g. "I250|2|1.01.01.002|C|1500.00|HISTORICO PADRAO").
        switch (recordCode)
        {
            case "0000":
                ValidateCabecalho0000(ref parser);
                break;
            case "I200":
                ValidateLancamentoI200(ref parser);
                break;
            case "I250":
                ValidatePartidaI250(ref parser);
                break;
            case "9999":
                ValidateTotalizador9999(ref parser);
                break;
            default:
                throw new FormatException($"Unknown SPED record code '{recordCode}'.");
        }

        return new IngestionRecord(id, line.ToString(), DateTime.UtcNow);
    }

    /// <summary>Record <c>0000</c>: file header. Layout: version|dateFrom|dateTo|companyName|cnpj|uf.</summary>
    private static void ValidateCabecalho0000(ref RecordParser parser)
    {
        parser.TryReadNextField(out _); // version
        parser.TryReadNextField(out _); // dateFrom
        parser.TryReadNextField(out _); // dateTo
        parser.TryReadNextField(out _); // companyName
        parser.TryReadNextField(out _); // cnpj
        parser.TryReadNextField(out _); // uf
    }

    /// <summary>Record <c>I200</c>: journal entry header. Layout: lineNumber|date|entryNumber|entity|flag.</summary>
    private static void ValidateLancamentoI200(ref RecordParser parser)
    {
        parser.TryReadNextField(out _); // lineNumber
        parser.TryReadNextField(out _); // date
        parser.TryReadNextField(out _); // entryNumber
        parser.TryReadNextField(out _); // entity
        parser.TryReadNextField(out _); // flag
    }

    /// <summary>Record <c>I250</c>: journal entry line (débito/crédito). Layout: lineNumber|account|debitCredit|value|complementaryHistory.</summary>
    private static void ValidatePartidaI250(ref RecordParser parser)
    {
        parser.TryReadNextField(out _); // lineNumber
        parser.TryReadNextField(out _); // account
        parser.TryReadNextField(out _); // debitCredit
        parser.TryReadNextField(out _); // value
        parser.TryReadNextField(out _); // complementaryHistory
    }

    /// <summary>Record <c>9999</c>: file trailer. Layout: totalLines.</summary>
    private static void ValidateTotalizador9999(ref RecordParser parser)
    {
        parser.TryReadNextField(out _); // totalLines
    }
}
