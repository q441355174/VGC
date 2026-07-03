using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed class VehicleCommandService
{
    private readonly Vehicle _vehicle;
    private readonly ILinkTransport _link;
    private readonly MavlinkCommandService _commandService;
    private readonly VehicleCommandQueue _commandQueue;

    public VehicleCommandService(Vehicle vehicle, ILinkTransport link, MavlinkCommandService? commandService = null, VehicleCommandQueue? commandQueue = null)
    {
        _vehicle = vehicle;
        _link = link;
        _commandService = commandService ?? new MavlinkCommandService();
        _commandQueue = commandQueue ?? new VehicleCommandQueue();
    }

    public VehicleCommandQueue CommandQueue => _commandQueue;

    public async ValueTask<VehicleCommandSendResult> SendCommandAsync(
        ushort command,
        byte confirmation = 0,
        float param1 = 0,
        float param2 = 0,
        float param3 = 0,
        float param4 = 0,
        float param5 = 0,
        float param6 = 0,
        float param7 = 0,
        CancellationToken cancellationToken = default)
    {
        var beginResult = _commandQueue.TryEnqueue(_vehicle.ComponentId, command);
        if (!beginResult.Sent)
        {
            return beginResult;
        }

        var commandLong = new MavlinkCommandLong(
            _vehicle.Id,
            _vehicle.ComponentId,
            command,
            confirmation,
            param1,
            param2,
            param3,
            param4,
            param5,
            param6,
            param7);

        await _commandService.SendCommandLongAsync(_link, commandLong, cancellationToken).ConfigureAwait(false);
        return beginResult;
    }

    public ValueTask<VehicleCommandSendResult> ArmAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(MavlinkCommandIds.ComponentArmDisarm, param1: 1.0f, cancellationToken: cancellationToken);
    }

    public ValueTask<VehicleCommandSendResult> DisarmAsync(CancellationToken cancellationToken = default)
    {
        return SendCommandAsync(MavlinkCommandIds.ComponentArmDisarm, param1: 0.0f, cancellationToken: cancellationToken);
    }

    public bool HandleCommandAck(MavlinkCommandAck ack)
    {
        return _commandQueue.TryAcknowledge(ack.ComponentId, ack.Command);
    }
}
