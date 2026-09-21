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
}
