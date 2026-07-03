using VGC.Core.Logging;

namespace VGC.Analyze;

public enum MavlinkConsoleLineKind
{
    Command,
    Response,
    Error
}

public sealed record MavlinkConsoleLine(
    int Sequence,
    DateTimeOffset Timestamp,
    MavlinkConsoleLineKind Kind,
    string Text);

public sealed record MavlinkConsoleSnapshot(
    IReadOnlyList<MavlinkConsoleLine> Lines,
    bool HasPendingCommand,
    string? PendingCommand,
    string StatusText);

public sealed class MavlinkConsoleRuntime
{
    private readonly List<MavlinkConsoleLine> _lines = [];
    private string? _pendingCommand;
    private int _sequence;

    public MavlinkConsoleSnapshot Snapshot => BuildSnapshot();

    public MavlinkConsoleSnapshot SubmitCommand(string command, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            AddLine(timestamp, MavlinkConsoleLineKind.Error, "Console command cannot be empty.");
            return BuildSnapshot();
        }

        _pendingCommand = command.Trim();
        AddLine(timestamp, MavlinkConsoleLineKind.Command, _pendingCommand);
        return BuildSnapshot();
    }

    public MavlinkConsoleSnapshot ReceiveLine(string text, DateTimeOffset timestamp)
    {
        AddLine(timestamp, MavlinkConsoleLineKind.Response, text);
        _pendingCommand = null;
        return BuildSnapshot();
    }

    public MavlinkConsoleSnapshot Fail(string error, DateTimeOffset timestamp)
    {
        AddLine(timestamp, MavlinkConsoleLineKind.Error, string.IsNullOrWhiteSpace(error) ? "Console command failed." : error);
        _pendingCommand = null;
        return BuildSnapshot();
    }

    private void AddLine(DateTimeOffset timestamp, MavlinkConsoleLineKind kind, string text)
    {
        _sequence++;
        _lines.Add(new MavlinkConsoleLine(_sequence, timestamp, kind, text));
    }

    private MavlinkConsoleSnapshot BuildSnapshot()
    {
        return new MavlinkConsoleSnapshot(
            _lines.ToArray(),
            _pendingCommand is not null,
            _pendingCommand,
            _pendingCommand is null ? $"Console lines {_lines.Count}" : $"Pending console command: {_pendingCommand}");
    }
}

public sealed record FlightLogDownloadPanelState(
    FlightLogDownloadState State,
    string StatusText,
    bool CanRequestList,
    bool CanCancel,
    bool CanRetry,
    double Progress,
    string? ActiveLogName);

public sealed class FlightLogDownloadPanelProjector
{
    public FlightLogDownloadPanelState Project(FlightLogDownloadSnapshot snapshot)
    {
        var active = snapshot.Logs.FirstOrDefault(log => log.Id == snapshot.ActiveLogId);
        var status = snapshot.State switch
        {
            FlightLogDownloadState.Idle => "Flight logs idle",
            FlightLogDownloadState.Listing => "Listing flight logs",
            FlightLogDownloadState.ListReady => $"Flight logs ready: {snapshot.Logs.Count}",
            FlightLogDownloadState.Downloading => $"Downloading {active?.Name ?? snapshot.ActiveLogId?.ToString() ?? "log"}",
            FlightLogDownloadState.Completed => $"Stored {snapshot.StoredPath}",
            FlightLogDownloadState.Cancelled => "Flight log download cancelled",
            FlightLogDownloadState.Failed => snapshot.LastError ?? "Flight log download failed",
            _ => snapshot.State.ToString()
        };

        return new FlightLogDownloadPanelState(
            snapshot.State,
            status,
            snapshot.CanRequestList,
            snapshot.CanCancel,
            snapshot.CanRetry,
            snapshot.Progress,
            active?.Name);
    }
}

public sealed record Px4LogMetadata(
    uint Id,
    string FileName,
    long SizeBytes,
    DateTimeOffset Timestamp,
    TimeSpan? Duration,
    string VehicleName,
    bool IsUploaded);

public sealed record Px4LogCatalogState(
    IReadOnlyList<Px4LogMetadata> Logs,
    long TotalBytes,
    DateTimeOffset? LatestTimestamp,
    string Summary);

public sealed class Px4LogManager
{
    private readonly List<Px4LogMetadata> _logs = [];

    public Px4LogCatalogState State => BuildState();

    public Px4LogCatalogState Load(IEnumerable<Px4LogMetadata> logs)
    {
        _logs.Clear();
        _logs.AddRange(logs.OrderByDescending(static log => log.Timestamp));
        return BuildState();
    }

    public Px4LogCatalogState ApplyDownloadComplete(uint id, string storedPath)
    {
        var index = _logs.FindIndex(log => log.Id == id);
        if (index >= 0)
        {
            _logs[index] = _logs[index] with { IsUploaded = true, FileName = storedPath };
        }

        return BuildState();
    }

    private Px4LogCatalogState BuildState()
    {
        return new Px4LogCatalogState(
            _logs.ToArray(),
            _logs.Sum(static log => log.SizeBytes),
            _logs.Count == 0 ? null : _logs.Max(static log => log.Timestamp),
            $"{_logs.Count} PX4 logs, {FormatBytes(_logs.Sum(static log => log.SizeBytes))}");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes < 1024 * 1024
            ? $"{bytes / 1024.0:F1} KB"
            : $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}

public sealed record FlightLogParserRuntimeState(
    FlightLogFormat Format,
    bool Recognized,
    bool Parsed,
    string StatusText,
    IReadOnlyList<FlightLogDiagnostic> Diagnostics);

public sealed class FlightLogParserRuntime
{
    private readonly FlightLogParserCatalog _catalog;

    public FlightLogParserRuntime(FlightLogParserCatalog? catalog = null)
    {
        _catalog = catalog ?? new FlightLogParserCatalog();
    }

    public FlightLogParserRuntimeState Inspect(ReadOnlySpan<byte> bytes)
    {
        var parser = _catalog.SelectParser(bytes);
        if (parser is null)
        {
            return new FlightLogParserRuntimeState(
                FlightLogFormat.Unknown,
                Recognized: false,
                Parsed: false,
                "No parser recognized the flight log.",
                [new FlightLogDiagnostic(FlightLogDiagnosticSeverity.Error, "Unknown.Unsupported", "No parser recognized the input.")]);
        }

        var result = parser.Parse(bytes);
        return new FlightLogParserRuntimeState(
            parser.Format,
            Recognized: true,
            Parsed: result.Success,
            result.Success ? $"{parser.Format} parsed" : result.Diagnostics.FirstOrDefault()?.Message ?? $"{parser.Format} parse failed",
            result.Diagnostics);
    }
}

public sealed record GeoTagWorkflowRow(
    string ImageName,
    GeoTagMatchState State,
    DateTimeOffset AdjustedTimestamp,
    string CoordinateText,
    string StatusText);

public sealed record GeoTagRuntimeUiState(
    int MatchedCount,
    int UnmatchedCount,
    IReadOnlyList<GeoTagWorkflowRow> Rows,
    string Summary);

public sealed class GeoTagRuntimeProjector
{
    public GeoTagRuntimeUiState Project(GeoTagWorkflowSummary summary)
    {
        var rows = summary.Results
            .Select(static result => new GeoTagWorkflowRow(
                Path.GetFileName(result.Image.Path),
                result.State,
                result.AdjustedCaptureTime,
                result.TrackPoint is null
                    ? "No coordinate"
                    : $"{result.TrackPoint.Coordinate.Latitude:F6},{result.TrackPoint.Coordinate.Longitude:F6}",
                result.StatusText))
            .ToArray();

        return new GeoTagRuntimeUiState(
            summary.MatchedCount,
            summary.UnmatchedCount,
            rows,
            $"{summary.MatchedCount} matched, {summary.UnmatchedCount} unmatched");
    }
}

public sealed record FileLogViewerState(
    IReadOnlyList<LogViewerRow> Rows,
    string? LevelFilter,
    string? CategoryFilter,
    string Summary);

public sealed class FileLogViewerProjector
{
    public FileLogViewerState Project(
        IEnumerable<LogViewerRow> rows,
        string? levelFilter = null,
        string? categoryFilter = null)
    {
        var filtered = rows
            .Where(row => string.IsNullOrWhiteSpace(levelFilter) || string.Equals(row.Level, levelFilter, StringComparison.OrdinalIgnoreCase))
            .Where(row => string.IsNullOrWhiteSpace(categoryFilter) || string.Equals(row.Category, categoryFilter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static row => row.Timestamp)
            .ToArray();

        return new FileLogViewerState(
            filtered,
            levelFilter,
            categoryFilter,
            $"{filtered.Length} app log rows");
    }

    public string ExportText(FileLogViewerState state)
    {
        return string.Join(Environment.NewLine, state.Rows.Select(static row => $"{row.Timestamp:O}\t{row.Level}\t{row.Category}\t{row.Message}"));
    }
}

public sealed record ReplayWorkflowDetailState(
    bool CanPlay,
    bool CanPause,
    bool CanSeek,
    bool CanStep,
    int PacketCount,
    int GapCount,
    string SelectedDetail,
    string Summary);

public sealed class ReplayWorkflowProjector
{
    public ReplayWorkflowDetailState Project(ReplayPlaybackSnapshot snapshot, ReplayTimelineProjection timeline, ReplayPacketIndexRow? selected)
    {
        var detail = selected is null
            ? "No packet selected"
            : $"{selected.Sequence}: {selected.MessageName} {selected.FieldSummary}";

        return new ReplayWorkflowDetailState(
            snapshot.CanPlay,
            snapshot.CanPause,
            snapshot.CanSeek,
            snapshot.State == ReplayPlaybackState.Playing,
            timeline.PacketCount,
            timeline.Gaps.Count,
            detail,
            $"{timeline.PacketCount} packets, {timeline.Gaps.Count} gaps, {snapshot.StatusText}");
    }
}

public sealed record AnalyzeRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class AnalyzeRuntimeEvidenceCatalog
{
    public IReadOnlyList<AnalyzeRuntimeEvidenceItem> Build()
    {
        return
        [
            new("ANALYZEFULL-253", "L1/L5", "MAVLink console command/write/read boundary is modeled in shared code.", true),
            new("ANALYZEFULL-254", "L1/L5", "Flight log download panel projects list/download/cancel/retry workflow state.", true),
            new("ANALYZEFULL-255", "L1", "PX4 log manager models log discovery metadata and completion state.", true),
            new("ANALYZEFULL-256", "L1", "ULog parser runtime recognizes PX4 ULog input and reports parser boundary status.", true),
            new("ANALYZEFULL-257", "L1", "DataFlash parser runtime recognizes ArduPilot DataFlash input and reports parser boundary status.", true),
            new("ANALYZEFULL-258", "L2/L5", "GeoTag runtime projects matched/unmatched image-log rows.", true),
            new("ANALYZEFULL-259", "L2", "File logging UI projection supports filters and export text.", true),
            new("ANALYZEFULL-260", "L2", "Replay workflow projection exposes controls, packet detail, and gap summary.", true),
            new("ANALYZEFULL-261", "L0/L5", "Real or SITL log fixture evidence remains deferred.", false)
        ];
    }
}

public sealed record AnalyzeRuntimeAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class AnalyzeRuntimeParityAudit
{
    public AnalyzeRuntimeAuditResult Audit(IReadOnlyList<AnalyzeRuntimeEvidenceItem> evidence)
    {
        var complete = evidence.Count(static item => item.Complete);
        var deferred = evidence.Where(static item => !item.Complete).Select(static item => item.Description).ToArray();
        return new AnalyzeRuntimeAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} analyze/log evidence items complete; {deferred.Length} real/SITL log evidence gaps remain.");
    }
}
