namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Selects which line format the ingestion pipeline should generate/parse.
/// </summary>
public enum RecordFormat
{
    /// <summary>
    /// Fixed 2-column format (<c>Id|RawValue</c>). Best-case scenario for the parser:
    /// no record-type dispatch, no variable column count.
    /// </summary>
    Simple,

    /// <summary>
    /// Simulates a real SPED Contábil (ECD) file: multiple record types
    /// (<c>0000</c>, <c>I200</c>, <c>I250</c>, <c>9999</c>), each with its own column
    /// layout, requiring per-line record-code dispatch before parsing.
    /// </summary>
    SpedCompleto
}
