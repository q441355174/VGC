namespace VGC.Payload;

public enum PayloadProtocolComponentKind
{
    Camera,
    Gimbal
}

public enum PayloadProtocolAction
{
    Discover,
    CaptureImage,
    StartVideoRecording,
    StopVideoRecording,
    SetGimbalAttitude
}

public enum PayloadProtocolCommandStatus
{
    Pending,
    Acknowledged,
    TimedOut,
    Rejected
}

public sealed record PayloadProtocolTarget(
    byte SystemId,
    byte ComponentId,
    PayloadProtocolComponentKind Kind);

public sealed record PayloadProtocolCommand(
    string Id,
    PayloadProtocolAction Action,
    PayloadProtocolTarget Target,
    GimbalCommand? GimbalTarget = null)
{
    public static PayloadProtocolCommand Create(
        PayloadProtocolAction action,
        PayloadProtocolTarget target,
        GimbalCommand? gimbalTarget = null,
        string? id = null)
    {
        return new PayloadProtocolCommand(
            string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id,
            action,
            target,
            gimbalTarget);
    }
}

public sealed record PayloadProtocolCommandResult(
    PayloadProtocolCommand Command,
    PayloadProtocolCommandStatus Status,
    int AttemptCount,
    string? Error = null)
{
    public bool CanRetry => Status is PayloadProtocolCommandStatus.TimedOut or PayloadProtocolCommandStatus.Rejected;
}

public interface IPayloadProtocolTranslator
{
    PayloadProtocolCommand CreateCameraCommand(PayloadProtocolAction action, PayloadProtocolTarget target);

    PayloadProtocolCommand CreateGimbalCommand(PayloadProtocolTarget target, GimbalCommand command);
}

public sealed class PayloadProtocolCommandTracker
{
    private readonly Dictionary<string, PayloadProtocolCommandResult> _commands = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<PayloadProtocolCommandResult> Commands => _commands.Values.ToArray();

    public PayloadProtocolCommandResult Submit(PayloadProtocolCommand command)
    {
        var attempt = _commands.TryGetValue(command.Id, out var existing)
            ? existing.AttemptCount + 1
            : 1;
        var result = new PayloadProtocolCommandResult(command, PayloadProtocolCommandStatus.Pending, attempt);
        _commands[command.Id] = result;
        return result;
    }

    public PayloadProtocolCommandResult Acknowledge(string commandId)
    {
        var existing = RequireExisting(commandId);
        var result = existing with
        {
            Status = PayloadProtocolCommandStatus.Acknowledged,
            Error = null
        };
        _commands[commandId] = result;
        return result;
    }

    public PayloadProtocolCommandResult Timeout(string commandId)
    {
        var existing = RequireExisting(commandId);
        var result = existing with
        {
            Status = PayloadProtocolCommandStatus.TimedOut,
            Error = "Payload command timed out"
        };
        _commands[commandId] = result;
        return result;
    }

    public PayloadProtocolCommandResult Reject(string commandId, string error)
    {
        var existing = RequireExisting(commandId);
        var result = existing with
        {
            Status = PayloadProtocolCommandStatus.Rejected,
            Error = string.IsNullOrWhiteSpace(error) ? "Payload command rejected" : error
        };
        _commands[commandId] = result;
        return result;
    }

    private PayloadProtocolCommandResult RequireExisting(string commandId)
    {
        if (_commands.TryGetValue(commandId, out var existing))
        {
            return existing;
        }

        throw new InvalidOperationException($"Payload command '{commandId}' is not pending.");
    }
}

public sealed class PayloadProtocolBoundary : IPayloadProtocolTranslator
{
    public PayloadProtocolCommand CreateCameraCommand(PayloadProtocolAction action, PayloadProtocolTarget target)
    {
        if (target.Kind != PayloadProtocolComponentKind.Camera)
        {
            throw new InvalidOperationException("Camera protocol commands require a camera target.");
        }

        return action switch
        {
            PayloadProtocolAction.CaptureImage or
            PayloadProtocolAction.StartVideoRecording or
            PayloadProtocolAction.StopVideoRecording or
            PayloadProtocolAction.Discover => PayloadProtocolCommand.Create(action, target),
            _ => throw new InvalidOperationException($"Action {action} is not a camera protocol command.")
        };
    }

    public PayloadProtocolCommand CreateGimbalCommand(PayloadProtocolTarget target, GimbalCommand command)
    {
        if (target.Kind != PayloadProtocolComponentKind.Gimbal)
        {
            throw new InvalidOperationException("Gimbal protocol commands require a gimbal target.");
        }

        return PayloadProtocolCommand.Create(PayloadProtocolAction.SetGimbalAttitude, target, command);
    }
}
