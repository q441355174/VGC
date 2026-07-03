namespace VGC.Analyze;

public enum FlightLogType
{
    MavlinkTelemetryLog,
    Px4Ulog,
    ArduPilotDataflash
}

public sealed record FlightLogEntry(
    string Id,
    FlightLogType Type,
    string FileName,
    long SizeBytes,
    DateTimeOffset RecordedAt);

public sealed record FlightLogTransferState(
    FlightLogEntry? ActiveDownload,
    double Progress,
    bool IsDownloading,
    string? Error);

public interface IFlightLogService
{
    Task<IReadOnlyList<FlightLogEntry>> ListLogsAsync(CancellationToken cancellationToken = default);

    Task<FlightLogTransferState> DownloadLogAsync(FlightLogEntry log, string destinationPath, CancellationToken cancellationToken = default);
}

public sealed class FlightLogServiceStub : IFlightLogService
{
    public Task<IReadOnlyList<FlightLogEntry>> ListLogsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<FlightLogEntry>>([]);
    }

    public Task<FlightLogTransferState> DownloadLogAsync(FlightLogEntry log, string destinationPath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FlightLogTransferState(log, 1.0, false, null));
    }
}
