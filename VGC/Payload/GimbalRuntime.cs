namespace VGC.Payload;

public sealed record GimbalRuntimeState(
    GimbalAttitude? Attitude,
    GimbalCommand? Target,
    PayloadCommandState SetAttitudeCommand)
{
    public static GimbalRuntimeState Empty { get; } = new(
        null,
        null,
        PayloadCommandState.Idle("Gimbal attitude"));

    public bool HasAttitude => Attitude is not null;

    public bool IsLocked => Attitude?.IsLocked == true || Target?.LockYaw == true;

    public string AttitudeText => Attitude is { } attitude
        ? $"Pitch {attitude.PitchDegrees:F1} | Yaw {attitude.YawDegrees:F1} | Roll {attitude.RollDegrees:F1}"
        : "No gimbal attitude";

    public string LockText => IsLocked ? "Gimbal locked" : "Gimbal free";

    public string TargetText => Target is { } target
        ? $"Target pitch {target.PitchDegrees:F1} | yaw {target.YawDegrees:F1}"
        : "No gimbal target";

    public string CommandText => SetAttitudeCommand.StatusText;
}

public sealed class GimbalRuntimeController
{
    private GimbalAttitude? _attitude;
    private GimbalCommand? _target;
    private PayloadCommandState _setAttitudeCommand = PayloadCommandState.Idle("Gimbal attitude");

    public GimbalRuntimeState State => Snapshot();

    public GimbalRuntimeState ApplyAttitude(GimbalAttitude attitude)
    {
        _attitude = attitude;
        return Snapshot();
    }

    public GimbalRuntimeState BeginSetAttitude(GimbalCommand command)
    {
        _target = command;
        _setAttitudeCommand = _setAttitudeCommand.Begin();
        return Snapshot();
    }

    public GimbalRuntimeState CompleteSetAttitude()
    {
        _setAttitudeCommand = _setAttitudeCommand.Succeed();
        return Snapshot();
    }

    public GimbalRuntimeState FailSetAttitude(string error, bool canRetry = true)
    {
        _setAttitudeCommand = _setAttitudeCommand.Fail(error, canRetry);
        return Snapshot();
    }

    public GimbalRuntimeState RetrySetAttitude()
    {
        if (_target is null || !_setAttitudeCommand.CanRetry)
        {
            return Snapshot();
        }

        _setAttitudeCommand = _setAttitudeCommand.Begin();
        return Snapshot();
    }

    public GimbalRuntimeState ClearTarget()
    {
        _target = null;
        _setAttitudeCommand = PayloadCommandState.Idle("Gimbal attitude");
        return Snapshot();
    }

    private GimbalRuntimeState Snapshot()
    {
        return new GimbalRuntimeState(_attitude, _target, _setAttitudeCommand);
    }
}
