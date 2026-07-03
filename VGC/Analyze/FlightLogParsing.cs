namespace VGC.Analyze;

public enum FlightLogFormat
{
    Unknown,
    Px4ULog,
    ArduPilotDataFlash
}

public enum FlightLogDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record FlightLogDiagnostic(
    FlightLogDiagnosticSeverity Severity,
    string Code,
    string Message);

public sealed record FlightLogMessageDefinition(
    string Name,
    IReadOnlyList<string> Fields,
    double? RateHz = null);

public sealed record FlightLogSeriesSample(
    string SeriesName,
    TimeSpan Timestamp,
    IReadOnlyDictionary<string, double> Values);

public sealed record FlightLogEvent(
    TimeSpan Timestamp,
    string Name,
    string Message);

public sealed record ParsedFlightLog(
    FlightLogFormat Format,
    TimeSpan Duration,
    IReadOnlyList<FlightLogMessageDefinition> Messages,
    IReadOnlyList<FlightLogSeriesSample> Samples,
    IReadOnlyList<KeyValuePair<string, string>> Parameters,
    IReadOnlyList<FlightLogEvent> Events,
    IReadOnlyList<FlightLogDiagnostic> Diagnostics)
{
    public int MessageCount => Messages.Count;

    public int SampleCount => Samples.Count;

    public int ParameterCount => Parameters.Count;

    public bool HasErrors => Diagnostics.Any(static diagnostic => diagnostic.Severity == FlightLogDiagnosticSeverity.Error);
}

public sealed record FlightLogParserResult(
    bool Success,
    ParsedFlightLog? Log,
    IReadOnlyList<FlightLogDiagnostic> Diagnostics)
{
    public static FlightLogParserResult Unsupported(FlightLogFormat format, string message)
    {
        return new FlightLogParserResult(
            false,
            null,
            [new FlightLogDiagnostic(FlightLogDiagnosticSeverity.Error, $"{format}.Unsupported", message)]);
    }
}

public interface IFlightLogParser
{
    FlightLogFormat Format { get; }

    bool CanParse(ReadOnlySpan<byte> bytes);

    FlightLogParserResult Parse(ReadOnlySpan<byte> bytes);
}

public sealed class ULogFlightLogParser : IFlightLogParser
{
    private static readonly byte[] Magic = [0x55, 0x4c, 0x6f, 0x67, 0x01, 0x12, 0x35];

    public FlightLogFormat Format => FlightLogFormat.Px4ULog;

    public bool CanParse(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= Magic.Length && bytes[..Magic.Length].SequenceEqual(Magic);
    }

    public FlightLogParserResult Parse(ReadOnlySpan<byte> bytes)
    {
        if (!CanParse(bytes))
        {
            return FlightLogParserResult.Unsupported(Format, "Input does not start with a PX4 ULog header.");
        }

        return FlightLogParserResult.Unsupported(Format, "PX4 ULog binary parsing is planned but not implemented in this boundary phase.");
    }
}

public sealed class DataFlashFlightLogParser : IFlightLogParser
{
    public FlightLogFormat Format => FlightLogFormat.ArduPilotDataFlash;

    public bool CanParse(ReadOnlySpan<byte> bytes)
    {
        return bytes.Length >= 3 && bytes[0] == 0xa3 && bytes[1] == 0x95;
    }

    public FlightLogParserResult Parse(ReadOnlySpan<byte> bytes)
    {
        if (!CanParse(bytes))
        {
            return FlightLogParserResult.Unsupported(Format, "Input does not start with an ArduPilot DataFlash packet header.");
        }

        return FlightLogParserResult.Unsupported(Format, "ArduPilot DataFlash binary parsing is planned but not implemented in this boundary phase.");
    }
}

public sealed class FlightLogParserCatalog
{
    private readonly IReadOnlyList<IFlightLogParser> _parsers;

    public FlightLogParserCatalog(IEnumerable<IFlightLogParser>? parsers = null)
    {
        _parsers = (parsers ?? [new ULogFlightLogParser(), new DataFlashFlightLogParser()]).ToList();
    }

    public IReadOnlyList<IFlightLogParser> Parsers => _parsers;

    public IFlightLogParser? SelectParser(ReadOnlySpan<byte> bytes)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(bytes))
            {
                return parser;
            }
        }

        return null;
    }

    public FlightLogParserResult Parse(ReadOnlySpan<byte> bytes)
    {
        var parser = SelectParser(bytes);
        return parser is null
            ? FlightLogParserResult.Unsupported(FlightLogFormat.Unknown, "No registered flight-log parser recognized the input.")
            : parser.Parse(bytes);
    }
}

public sealed record FlightLogSummaryProjection(
    FlightLogFormat Format,
    string Title,
    string StatusText,
    int MessageCount,
    int SampleCount,
    int ParameterCount,
    TimeSpan Duration,
    IReadOnlyList<FlightLogDiagnostic> Diagnostics)
{
    public static FlightLogSummaryProjection FromResult(FlightLogParserResult result)
    {
        if (result.Success && result.Log is not null)
        {
            return new FlightLogSummaryProjection(
                result.Log.Format,
                result.Log.Format.ToString(),
                $"Parsed {result.Log.MessageCount} messages, {result.Log.SampleCount} samples",
                result.Log.MessageCount,
                result.Log.SampleCount,
                result.Log.ParameterCount,
                result.Log.Duration,
                result.Log.Diagnostics);
        }

        var first = result.Diagnostics.FirstOrDefault();
        return new FlightLogSummaryProjection(
            FlightLogFormat.Unknown,
            "Unsupported flight log",
            first?.Message ?? "Flight log could not be parsed.",
            0,
            0,
            0,
            TimeSpan.Zero,
            result.Diagnostics);
    }
}
