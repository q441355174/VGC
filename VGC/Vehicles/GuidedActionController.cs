using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Vehicles;

public enum GuidedActionKind
{
    Arm,
    Disarm,
    Takeoff,
    Land,
    ReturnToLaunch,
    Pause
}

public enum GuidedActionState
{
    Unavailable,
    Ready,
    ConfirmationRequired,
    Pending,
    Accepted,
    Rejected,
    Timeout,
    Failed
}

public sealed record GuidedActionStatus(
    GuidedActionKind Kind,
    string Label,
    ushort Command,
    GuidedActionState State,
    bool IsEnabled,
    bool RequiresConfirmation,
    string StatusText,
    MavlinkCommandResult? AckResult = null,
    DateTimeOffset? UpdatedAt = null);

internal sealed record GuidedActionDefinition(
    GuidedActionKind Kind,
    string Label,
    ushort Command,
    float Param1 = 0,
    float Param2 = 0,
    float Param3 = 0,
    float Param4 = 0,
    float Param5 = 0,
    float Param6 = 0,
    float Param7 = 0);

public sealed class GuidedActionController
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly GuidedActionDefinition[] Definitions =
    [
        new(GuidedActionKind.Arm, "Arm", MavlinkCommandIds.ComponentArmDisarm, Param1: 1),
        new(GuidedActionKind.Disarm, "Disarm", MavlinkCommandIds.ComponentArmDisarm, Param1: 0),
        new(GuidedActionKind.Takeoff, "Takeoff", MavlinkCommandIds.NavTakeoff),
        new(GuidedActionKind.Land, "Land", MavlinkCommandIds.NavLand),
        new(GuidedActionKind.ReturnToLaunch, "RTL", MavlinkCommandIds.NavReturnToLaunch),
        new(GuidedActionKind.Pause, "Pause", MavlinkCommandIds.DoPauseContinue, Param1: 0)
    ];

    private readonly VehicleCommandQueue _commandQueue = new();
    private readonly Dictionary<GuidedActionKind, GuidedActionStatus> _statuses = new();
    private readonly Dictionary<(byte ComponentId, ushort Command), GuidedActionKind> _pendingActions = new();
    private GuidedActionKind? _pendingConfirmation;

    public GuidedActionController()
    {
        foreach (var definition in Definitions)
        {
            _statuses[definition.Kind] = BuildStatus(definition, GuidedActionState.Unavailable, isEnabled: false, "No active vehicle");
        }
    }

    public GuidedActionKind? PendingConfirmation => _pendingConfirmation;

    public IReadOnlyList<GuidedActionStatus> Capture(Vehicle? vehicle, ILinkTransport? link)
    {
        var hasVehicle = vehicle is not null;
        var canSend = link is { IsConnected: true, CanSend: true };

        return Definitions.Select(definition =>
        {
            if (_statuses.TryGetValue(definition.Kind, out var current)
                && current.State is GuidedActionState.ConfirmationRequired or GuidedActionState.Pending or GuidedActionState.Accepted or GuidedActionState.Rejected or GuidedActionState.Timeout or GuidedActionState.Failed)
            {
                return current with { IsEnabled = hasVehicle && canSend && current.State != GuidedActionState.Pending };
            }

            var text = hasVehicle
                ? canSend ? "Ready" : "No send-capable link"
                : "No active vehicle";
            return BuildStatus(definition, canSend && hasVehicle ? GuidedActionState.Ready : GuidedActionState.Unavailable, hasVehicle && canSend, text);
        }).ToArray();
    }

    public GuidedActionStatus RequestConfirmation(GuidedActionKind kind, Vehicle? vehicle, ILinkTransport? link)
    {
        if (!TryResolve(kind, vehicle, link, out var definition, out var failure))
        {
            return SetStatus(kind, failure);
        }

        _pendingConfirmation = kind;
        return SetStatus(definition, GuidedActionState.ConfirmationRequired, true, $"Confirm {definition.Label}");
    }

    public async ValueTask<GuidedActionStatus> ConfirmAsync(Vehicle? vehicle, ILinkTransport? link, CancellationToken cancellationToken = default)
    {
        if (_pendingConfirmation is not { } kind)
        {
            return SetStatus(GuidedActionKind.Arm, GuidedActionState.Failed, false, "No guided action awaiting confirmation");
        }

        _pendingConfirmation = null;
        return await SendAsync(kind, vehicle, link, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<GuidedActionStatus> SendAsync(GuidedActionKind kind, Vehicle? vehicle, ILinkTransport? link, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(kind, vehicle, link, out var definition, out var failure))
        {
            return SetStatus(kind, failure);
        }

        var service = vehicle!.CreateCommandService(link!, commandQueue: _commandQueue);
        var result = await service.SendCommandAsync(
            definition.Command,
            param1: definition.Param1,
            param2: definition.Param2,
            param3: definition.Param3,
            param4: definition.Param4,
            param5: definition.Param5,
            param6: definition.Param6,
            param7: definition.Param7,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Sent)
        {
            return SetStatus(definition, GuidedActionState.Failed, true, result.FailureReason ?? "Guided action was not sent");
        }

        _pendingActions[(vehicle.ComponentId, definition.Command)] = definition.Kind;
        return SetStatus(definition, GuidedActionState.Pending, false, $"{definition.Label} pending");
    }

    public bool HandleCommandAck(MavlinkCommandAck ack)
    {
        if (!_pendingActions.TryGetValue((ack.ComponentId, ack.Command), out var kind))
        {
            return false;
        }

        var definition = Definitions.First(staticDefinition => staticDefinition.Kind == kind);
        var state = ack.Result switch
        {
            MavlinkCommandResult.InProgress => GuidedActionState.Pending,
            MavlinkCommandResult.Accepted => GuidedActionState.Accepted,
            MavlinkCommandResult.Failed => GuidedActionState.Failed,
            _ => GuidedActionState.Rejected
        };

        if (ack.Result != MavlinkCommandResult.InProgress)
        {
            _pendingActions.Remove((ack.ComponentId, ack.Command));
        }

        SetStatus(definition, state, isEnabled: state != GuidedActionState.Pending, $"{definition.Label} {ack.Result}", ack.Result);
        _commandQueue.TryAcknowledge(ack.ComponentId, ack.Command);
        return true;
    }

    public IReadOnlyList<GuidedActionStatus> MarkTimeouts(TimeSpan? timeout = null, DateTimeOffset? now = null)
    {
        var cutoff = (now ?? DateTimeOffset.Now) - (timeout ?? DefaultTimeout);
        var timedOut = _commandQueue.PendingCommands
            .Where(command => command.SentAt <= cutoff)
            .ToArray();

        foreach (var command in timedOut)
        {
            if (_pendingActions.TryGetValue((command.TargetComponentId, command.Command), out var kind))
            {
                var definition = Definitions.First(staticDefinition => staticDefinition.Kind == kind);
                SetStatus(definition, GuidedActionState.Timeout, true, $"{definition.Label} timeout");
                _pendingActions.Remove((command.TargetComponentId, command.Command));
            }

            _commandQueue.TryAcknowledge(command.TargetComponentId, command.Command);
        }

        return _statuses.Values.ToArray();
    }

    private static bool TryResolve(GuidedActionKind kind, Vehicle? vehicle, ILinkTransport? link, out GuidedActionDefinition definition, out GuidedActionStatus failure)
    {
        definition = Definitions.First(item => item.Kind == kind);

        if (vehicle is null)
        {
            failure = BuildStatus(definition, GuidedActionState.Unavailable, false, "No active vehicle");
            return false;
        }

        if (link is not { IsConnected: true, CanSend: true })
        {
            failure = BuildStatus(definition, GuidedActionState.Unavailable, false, "No send-capable link");
            return false;
        }

        failure = default!;
        return true;
    }

    private GuidedActionStatus SetStatus(GuidedActionKind kind, GuidedActionStatus status)
    {
        _statuses[kind] = status;
        return status;
    }

    private GuidedActionStatus SetStatus(GuidedActionKind kind, GuidedActionState state, bool isEnabled, string text)
    {
        var definition = Definitions.First(item => item.Kind == kind);
        return SetStatus(definition, state, isEnabled, text);
    }

    private GuidedActionStatus SetStatus(
        GuidedActionDefinition definition,
        GuidedActionState state,
        bool isEnabled,
        string text,
        MavlinkCommandResult? result = null)
    {
        var status = BuildStatus(definition, state, isEnabled, text, result);
        _statuses[definition.Kind] = status;
        return status;
    }

    private static GuidedActionStatus BuildStatus(
        GuidedActionDefinition definition,
        GuidedActionState state,
        bool isEnabled,
        string text,
        MavlinkCommandResult? result = null)
    {
        return new GuidedActionStatus(
            definition.Kind,
            definition.Label,
            definition.Command,
            state,
            isEnabled,
            RequiresConfirmation: true,
            text,
            result,
            DateTimeOffset.Now);
    }
}
