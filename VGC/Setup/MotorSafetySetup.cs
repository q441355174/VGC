using VGC.Firmware;
using VGC.Vehicles;

namespace VGC.Setup;

public enum MotorSafetyActionType
{
    MotorTest,
    ActuatorTest,
    SafetyConfirm
}

public enum MotorSafetyActionState
{
    Idle,
    AwaitingSafetyConfirmation,
    Armed,
    Blocked,
    Completed,
    Cancelled
}

public sealed record MotorSafetyActionRequest(
    MotorSafetyActionType ActionType,
    string Title,
    string SafetyNotice,
    bool RequiresExplicitConfirmation);

public sealed record MotorSafetyActionSnapshot(
    MotorSafetyActionType? ActionType,
    MotorSafetyActionState State,
    string StatusText,
    string DeviceOnlyRisk,
    MotorSafetyActionRequest? PendingAction);

public sealed record MotorSafetySetupStatus(
    string Title,
    bool IsAvailable,
    bool RequiresExplicitConfirmation,
    MotorSafetyActionState State,
    string StatusText,
    string DeviceOnlyRisk);

public sealed class MotorSafetyWorkflow
{
    private MotorSafetyActionType? _actionType;
    private MotorSafetyActionState _state = MotorSafetyActionState.Idle;
    private string _statusText = "Idle";
    private MotorSafetyActionRequest? _pendingAction;

    public MotorSafetyActionSnapshot Snapshot => new(
        _actionType,
        _state,
        _statusText,
        "Motor test and safety actions must be validated on real hardware.",
        _pendingAction);

    public MotorSafetyActionRequest RequestAction(MotorSafetyActionType actionType)
    {
        if (_state is MotorSafetyActionState.AwaitingSafetyConfirmation or MotorSafetyActionState.Armed)
        {
            throw new InvalidOperationException("A motor/safety action is already active.");
        }

        _actionType = actionType;
        _state = MotorSafetyActionState.AwaitingSafetyConfirmation;
        _statusText = $"Confirm {actionType} action";
        _pendingAction = new MotorSafetyActionRequest(
            actionType,
            actionType.ToString(),
            "This action affects a real vehicle. Confirm on device before proceeding.",
            RequiresExplicitConfirmation: true);
        return _pendingAction;
    }

    public MotorSafetyActionRequest ConfirmAction()
    {
        if (_state != MotorSafetyActionState.AwaitingSafetyConfirmation || _pendingAction is null)
        {
            throw new InvalidOperationException("No motor/safety action is awaiting confirmation.");
        }

        _state = MotorSafetyActionState.Armed;
        _statusText = $"{_actionType} armed";
        return _pendingAction;
    }

    public void Complete(string statusText = "Completed")
    {
        if (_state != MotorSafetyActionState.Armed)
        {
            throw new InvalidOperationException("Only an armed action can complete.");
        }

        _state = MotorSafetyActionState.Completed;
        _statusText = statusText;
        _pendingAction = null;
    }

    public void Block(string reason)
    {
        _state = MotorSafetyActionState.Blocked;
        _statusText = reason;
        _pendingAction = null;
    }

    public void Cancel(string statusText = "Cancelled")
    {
        if (_state == MotorSafetyActionState.Idle)
        {
            return;
        }

        _state = MotorSafetyActionState.Cancelled;
        _statusText = statusText;
        _pendingAction = null;
    }

    public void Reset()
    {
        _actionType = null;
        _state = MotorSafetyActionState.Idle;
        _statusText = "Idle";
        _pendingAction = null;
    }
}

public sealed class MotorSafetySetupService
{
    public MotorSafetySetupStatus Project(
        VehicleSetupComponent component,
        Vehicle vehicle,
        FirmwarePluginManager firmwarePluginManager)
    {
        var firmware = firmwarePluginManager.GetPlugin(vehicle.Autopilot);
        var requiresConfirmation = component.Id is "motors" or "safety";
        var blocked = component.Id == "motors" && vehicle.VehicleType != MavType.Quadrotor;
        var state = blocked
            ? MotorSafetyActionState.Blocked
            : requiresConfirmation
                ? MotorSafetyActionState.AwaitingSafetyConfirmation
                : MotorSafetyActionState.Idle;

        var statusText = blocked
            ? "Motor test is only mapped for supported multirotor types."
            : requiresConfirmation
                ? $"Confirm {component.Title} action before sending commands."
                : $"{component.Title} is available.";

        var risk = component.Id switch
        {
            "motors" => "Motor tests require real hardware and a physical-device check before execution.",
            "safety" => $"Safety settings depend on {firmware.Name} behavior and must be validated on device.",
            _ => "No additional motor/safety risk."
        };

        return new MotorSafetySetupStatus(
            component.Title,
            component.IsAvailable && !blocked,
            requiresConfirmation,
            state,
            statusText,
            risk);
    }
}
