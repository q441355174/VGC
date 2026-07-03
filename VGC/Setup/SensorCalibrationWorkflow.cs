namespace VGC.Setup;

public enum SensorCalibrationType
{
    Compass,
    Accelerometer,
    Gyroscope,
    Level
}

public enum SensorCalibrationState
{
    Idle,
    AwaitingConfirmation,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public sealed record SensorCalibrationCommandRequest(
    SensorCalibrationType CalibrationType,
    bool RequiresSafetyConfirmation,
    string CommandName,
    IReadOnlyDictionary<string, float> Parameters);

public sealed record SensorCalibrationSnapshot(
    SensorCalibrationType? CalibrationType,
    SensorCalibrationState State,
    double Progress,
    string StatusText,
    SensorCalibrationCommandRequest? PendingCommand);

public sealed class SensorCalibrationWorkflow
{
    private SensorCalibrationType? _calibrationType;
    private SensorCalibrationState _state = SensorCalibrationState.Idle;
    private double _progress;
    private string _statusText = "Idle";
    private SensorCalibrationCommandRequest? _pendingCommand;

    public SensorCalibrationSnapshot Snapshot => new(
        _calibrationType,
        _state,
        _progress,
        _statusText,
        _pendingCommand);

    public SensorCalibrationCommandRequest RequestStart(SensorCalibrationType calibrationType)
    {
        if (_state == SensorCalibrationState.InProgress || _state == SensorCalibrationState.AwaitingConfirmation)
        {
            throw new InvalidOperationException("A calibration workflow is already active.");
        }

        _calibrationType = calibrationType;
        _state = SensorCalibrationState.AwaitingConfirmation;
        _progress = 0;
        _pendingCommand = CreateCommand(calibrationType);
        _statusText = $"Confirm {calibrationType} calibration";
        return _pendingCommand;
    }

    public SensorCalibrationCommandRequest ConfirmStart()
    {
        if (_state != SensorCalibrationState.AwaitingConfirmation || _pendingCommand is null)
        {
            throw new InvalidOperationException("No calibration command is awaiting confirmation.");
        }

        _state = SensorCalibrationState.InProgress;
        _statusText = $"{_calibrationType} calibration in progress";
        return _pendingCommand;
    }

    public void ReportProgress(double progress, string? statusText = null)
    {
        if (_state != SensorCalibrationState.InProgress)
        {
            throw new InvalidOperationException("Calibration progress requires an active workflow.");
        }

        _progress = Math.Clamp(progress, 0, 1);
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _statusText = statusText;
        }
    }

    public void Complete(string statusText = "Calibration complete")
    {
        if (_state != SensorCalibrationState.InProgress)
        {
            throw new InvalidOperationException("Only an active calibration can complete.");
        }

        _progress = 1;
        _state = SensorCalibrationState.Completed;
        _statusText = statusText;
        _pendingCommand = null;
    }

    public void Fail(string error)
    {
        if (_state is SensorCalibrationState.Idle or SensorCalibrationState.Completed or SensorCalibrationState.Cancelled)
        {
            throw new InvalidOperationException("No active calibration can fail.");
        }

        _state = SensorCalibrationState.Failed;
        _statusText = error;
        _pendingCommand = null;
    }

    public void Cancel(string statusText = "Calibration cancelled")
    {
        if (_state is SensorCalibrationState.Idle or SensorCalibrationState.Completed)
        {
            return;
        }

        _state = SensorCalibrationState.Cancelled;
        _statusText = statusText;
        _pendingCommand = null;
    }

    public void Reset()
    {
        _calibrationType = null;
        _state = SensorCalibrationState.Idle;
        _progress = 0;
        _statusText = "Idle";
        _pendingCommand = null;
    }

    private static SensorCalibrationCommandRequest CreateCommand(SensorCalibrationType calibrationType)
    {
        var parameters = calibrationType switch
        {
            SensorCalibrationType.Compass => new Dictionary<string, float> { ["compass"] = 1 },
            SensorCalibrationType.Accelerometer => new Dictionary<string, float> { ["accelerometer"] = 1 },
            SensorCalibrationType.Gyroscope => new Dictionary<string, float> { ["gyroscope"] = 1 },
            SensorCalibrationType.Level => new Dictionary<string, float> { ["level"] = 1 },
            _ => new Dictionary<string, float>()
        };

        return new SensorCalibrationCommandRequest(
            calibrationType,
            RequiresSafetyConfirmation: true,
            CommandName: "MAV_CMD_PREFLIGHT_CALIBRATION",
            parameters);
    }
}
