namespace FG.HighThroughput.Engine.Core;

/// <summary>
/// Lightweight record representing a single parsed line of ingestion data.
/// Kept as a struct/record with primitive fields only to minimize per-row allocations
/// once materialized off the zero-allocation parsing stage.
/// </summary>
/// <param name="Id">Sequential identifier assigned during parsing.</param>
/// <param name="RawValue">Placeholder payload column - to be replaced by real schema fields.</param>
/// <param name="IngestedAtUtc">UTC timestamp captured at parse time.</param>
public readonly record struct IngestionRecord(long Id, string RawValue, DateTime IngestedAtUtc);
