namespace VGC.Analyze;

public enum FlightLogDownloadState
{
    Idle,
    Listing,
    ListReady,
    Downloading,
    Completed,
    Cancelled,
    Failed
}

public enum FlightLogDownloadActionType
{
    None,
    RequestList,
    RequestDownload,
    RetryDownload,
    CancelDownload,
    StoreBytes
}

public sealed record FlightLogDescriptor(
    uint Id,
    string Name,
    DateTimeOffset? CreatedAt,
    long SizeBytes,
    FlightLogFormat Format);

public sealed record FlightLogStorageRequest(
    uint LogId,
    string SuggestedFileName,
    long ExpectedSizeBytes,
    bool OverwriteExisting);

public sealed record FlightLogStorageResult(
    bool Success,
    string? Path,
    string? ErrorMessage)
{
    public static FlightLogStorageResult Stored(string path)
    {
        return new FlightLogStorageResult(true, path, null);
    }

    public static FlightLogStorageResult Failed(string message)
    {
        return new FlightLogStorageResult(false, null, message);
    }
}

public sealed record FlightLogDownloadAction(
    FlightLogDownloadActionType Type,
    uint? LogId = null,
    FlightLogStorageRequest? StorageRequest = null,
    string Description = "")
{
    public static FlightLogDownloadAction None { get; } = new(FlightLogDownloadActionType.None);
}

public sealed record FlightLogDownloadSnapshot(
    FlightLogDownloadState State,
    IReadOnlyList<FlightLogDescriptor> Logs,
    uint? ActiveLogId,
    long BytesReceived,
    long BytesExpected,
    int RetryCount,
    int MaxRetryCount,
    string StatusText,
    string? StoredPath,
    string? LastError)
{
    public double Progress =>
        BytesExpected <= 0 ? 0 : Math.Clamp((double)BytesReceived / BytesExpected, 0, 1);

    public bool CanRequestList => State is FlightLogDownloadState.Idle or FlightLogDownloadState.ListReady or FlightLogDownloadState.Failed or FlightLogDownloadState.Cancelled;

    public bool CanCancel => State == FlightLogDownloadState.Downloading;

    public bool CanRetry => State == FlightLogDownloadState.Failed && ActiveLogId is not null && RetryCount < MaxRetryCount;
}

public sealed class FlightLogDownloadWorkflow
{
    private readonly List<FlightLogDescriptor> _logs = [];

    public FlightLogDownloadWorkflow(int maxRetryCount = 3)
    {
        Snapshot = new FlightLogDownloadSnapshot(
            FlightLogDownloadState.Idle,
            _logs,
            null,
            0,
            0,
            0,
            maxRetryCount,
            "Flight logs not requested",
            null,
            null);
    }

    public FlightLogDownloadSnapshot Snapshot { get; private set; }

    public FlightLogDownloadAction RequestList()
    {
        if (!Snapshot.CanRequestList)
        {
            return FlightLogDownloadAction.None;
        }

        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.Listing,
            StatusText = "Requesting flight log list",
            LastError = null
        };
        return new FlightLogDownloadAction(FlightLogDownloadActionType.RequestList, Description: "Request vehicle flight log list through transport service.");
    }

    public void ApplyList(IReadOnlyList<FlightLogDescriptor> logs)
    {
        _logs.Clear();
        _logs.AddRange(logs.OrderByDescending(static log => log.CreatedAt ?? DateTimeOffset.MinValue));
        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.ListReady,
            Logs = _logs.ToList(),
            StatusText = $"Flight logs ready: {_logs.Count}",
            LastError = null
        };
    }

    public FlightLogDownloadAction RequestDownload(uint logId, bool overwriteExisting = false)
    {
        var log = _logs.FirstOrDefault(item => item.Id == logId);
        if (log is null)
        {
            Snapshot = Snapshot with
            {
                State = FlightLogDownloadState.Failed,
                ActiveLogId = logId,
                LastError = $"Flight log {logId} is not in the current list.",
                StatusText = $"Flight log {logId} unavailable"
            };
            return FlightLogDownloadAction.None;
        }

        var storage = new FlightLogStorageRequest(log.Id, BuildFileName(log), log.SizeBytes, overwriteExisting);
        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.Downloading,
            ActiveLogId = log.Id,
            BytesReceived = 0,
            BytesExpected = log.SizeBytes,
            StatusText = $"Downloading {log.Name}",
            StoredPath = null,
            LastError = null
        };

        return new FlightLogDownloadAction(
            FlightLogDownloadActionType.RequestDownload,
            log.Id,
            storage,
            "Request vehicle log bytes through transport service; UI must not construct protocol payloads.");
    }

    public void ApplyProgress(long bytesReceived)
    {
        if (Snapshot.State != FlightLogDownloadState.Downloading)
        {
            return;
        }

        var clamped = Math.Clamp(bytesReceived, 0, Snapshot.BytesExpected);
        Snapshot = Snapshot with
        {
            BytesReceived = clamped,
            StatusText = $"Downloading {clamped}/{Snapshot.BytesExpected} bytes"
        };
    }

    public void Complete(FlightLogStorageResult storage)
    {
        if (Snapshot.State != FlightLogDownloadState.Downloading)
        {
            return;
        }

        Snapshot = storage.Success
            ? Snapshot with
            {
                State = FlightLogDownloadState.Completed,
                BytesReceived = Snapshot.BytesExpected,
                StoredPath = storage.Path,
                StatusText = $"Flight log stored: {storage.Path}",
                LastError = null
            }
            : Snapshot with
            {
                State = FlightLogDownloadState.Failed,
                LastError = storage.ErrorMessage,
                StatusText = storage.ErrorMessage ?? "Flight log storage failed"
            };
    }

    public FlightLogDownloadAction Cancel()
    {
        if (Snapshot.State != FlightLogDownloadState.Downloading)
        {
            return FlightLogDownloadAction.None;
        }

        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.Cancelled,
            StatusText = "Flight log download cancelled"
        };
        return new FlightLogDownloadAction(FlightLogDownloadActionType.CancelDownload, Snapshot.ActiveLogId, Description: "Cancel active flight log download through transport service.");
    }

    public FlightLogDownloadAction Fail(string message)
    {
        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.Failed,
            LastError = message,
            StatusText = message
        };
        return FlightLogDownloadAction.None;
    }

    public FlightLogDownloadAction Retry()
    {
        if (!Snapshot.CanRetry || Snapshot.ActiveLogId is null)
        {
            return FlightLogDownloadAction.None;
        }

        var retryCount = Snapshot.RetryCount + 1;
        Snapshot = Snapshot with
        {
            State = FlightLogDownloadState.Downloading,
            RetryCount = retryCount,
            BytesReceived = 0,
            StatusText = $"Retrying flight log download {retryCount}/{Snapshot.MaxRetryCount}",
            LastError = null
        };
        return new FlightLogDownloadAction(FlightLogDownloadActionType.RetryDownload, Snapshot.ActiveLogId, Description: "Retry active flight log download through transport service.");
    }

    private static string BuildFileName(FlightLogDescriptor log)
    {
        var extension = log.Format switch
        {
            FlightLogFormat.Px4ULog => ".ulg",
            FlightLogFormat.ArduPilotDataFlash => ".bin",
            _ => ".log"
        };

        return string.IsNullOrWhiteSpace(log.Name)
            ? $"flight-log-{log.Id}{extension}"
            : $"{Path.GetFileNameWithoutExtension(log.Name)}{extension}";
    }
}
