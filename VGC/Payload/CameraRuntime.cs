namespace VGC.Payload;

public sealed record CameraStorageState(
    bool IsAvailable,
    long? FreeBytes = null,
    string? Error = null)
{
    public static CameraStorageState Unknown { get; } = new(false, null, "Storage unknown");

    public string StatusText
    {
        get
        {
            if (Error is { Length: > 0 })
            {
                return $"Storage error: {Error}";
            }

            if (!IsAvailable)
            {
                return "Storage unavailable";
            }

            return FreeBytes is { } bytes
                ? $"Storage available | {FormatBytes(bytes)} free"
                : "Storage available";
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024L * 1024L * 1024L => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            >= 1024L * 1024L => $"{bytes / (1024.0 * 1024.0):F1} MB",
            >= 1024L => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
}

public sealed record CameraRuntimeState(
    CameraStatus? Status,
    CameraStorageState Storage,
    PayloadCommandState ImageCapture,
    PayloadCommandState VideoRecording)
{
    public static CameraRuntimeState Empty { get; } = new(
        null,
        CameraStorageState.Unknown,
        PayloadCommandState.Idle("Image capture"),
        PayloadCommandState.Idle("Video recording"));

    public bool IsReady => Status?.IsReady == true;

    public string ReadyText => Status is null
        ? "No camera"
        : Status.IsReady
            ? "Camera ready"
            : "Camera not ready";

    public string ModeText => Status?.Mode is { Length: > 0 } mode ? mode : "No camera mode";

    public string CaptureText => Status?.IsCapturingImage == true
        ? "Capturing image"
        : ImageCapture.StatusText;

    public string RecordingText => Status?.IsRecordingVideo == true
        ? "Recording video"
        : VideoRecording.StatusText;
}

public sealed class CameraRuntimeController
{
    private CameraStatus? _status;
    private CameraStorageState _storage = CameraStorageState.Unknown;
    private PayloadCommandState _imageCapture = PayloadCommandState.Idle("Image capture");
    private PayloadCommandState _videoRecording = PayloadCommandState.Idle("Video recording");

    public CameraRuntimeState State => Snapshot();

    public CameraRuntimeState ApplyStatus(CameraStatus status, CameraStorageState? storage = null)
    {
        _status = status;
        if (storage is not null)
        {
            _storage = storage;
        }

        return Snapshot();
    }

    public CameraRuntimeState BeginImageCapture()
    {
        if (_status?.IsReady != true)
        {
            _imageCapture = _imageCapture.Fail("Camera is not ready", canRetry: true);
            return Snapshot();
        }

        _imageCapture = _imageCapture.Begin();
        return Snapshot();
    }

    public CameraRuntimeState CompleteImageCapture()
    {
        _imageCapture = _imageCapture.Succeed();
        return Snapshot();
    }

    public CameraRuntimeState FailImageCapture(string error, bool canRetry = true)
    {
        _imageCapture = _imageCapture.Fail(error, canRetry);
        return Snapshot();
    }

    public CameraRuntimeState RetryImageCapture()
    {
        if (!_imageCapture.CanRetry)
        {
            return Snapshot();
        }

        return BeginImageCapture();
    }

    public CameraRuntimeState BeginVideoRecordingCommand()
    {
        if (_status?.IsReady != true)
        {
            _videoRecording = _videoRecording.Fail("Camera is not ready", canRetry: true);
            return Snapshot();
        }

        _videoRecording = _videoRecording.Begin();
        return Snapshot();
    }

    public CameraRuntimeState CompleteVideoRecordingCommand()
    {
        _videoRecording = _videoRecording.Succeed();
        return Snapshot();
    }

    public CameraRuntimeState FailVideoRecordingCommand(string error, bool canRetry = true)
    {
        _videoRecording = _videoRecording.Fail(error, canRetry);
        return Snapshot();
    }

    public CameraRuntimeState RetryVideoRecordingCommand()
    {
        if (!_videoRecording.CanRetry)
        {
            return Snapshot();
        }

        return BeginVideoRecordingCommand();
    }

    private CameraRuntimeState Snapshot()
    {
        return new CameraRuntimeState(_status, _storage, _imageCapture, _videoRecording);
    }
}
