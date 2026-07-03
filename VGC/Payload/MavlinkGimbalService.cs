using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Payload;

public sealed class MavlinkGimbalService : IGimbalService
{
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly ILinkTransport _link;
    private readonly MavlinkCommandService _commandService;

    public const ushort MavCmdDoGimbalManagerPitchyaw = 1000;
    public const ushort MavCmdDoGimbalManagerConfigure = 1001;

    public MavlinkGimbalService(
        byte systemId,
        byte componentId,
        ILinkTransport link,
        MavlinkCommandService? commandService = null)
    {
        _systemId = systemId;
        _componentId = componentId;
        _link = link;
        _commandService = commandService ?? new MavlinkCommandService(systemId, componentId);
    }

    public GimbalAttitude CurrentAttitude { get; private set; } = new(0, 0, 0, false);

    public Task<GimbalAttitude> GetAttitudeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CurrentAttitude);
    }

    public async Task SetAttitudeAsync(GimbalCommand command, CancellationToken cancellationToken = default)
    {
        var mavCommand = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: MavCmdDoGimbalManagerPitchyaw,
            Confirmation: 0,
            Param1: (float)command.PitchDegrees,
            Param2: (float)command.YawDegrees,
            Param3: command.LockYaw ? 1 : 0);

        await _commandService.SendCommandLongAsync(_link, mavCommand, cancellationToken).ConfigureAwait(false);
        CurrentAttitude = new GimbalAttitude(command.PitchDegrees, CurrentAttitude.RollDegrees, command.YawDegrees, command.LockYaw);
    }

    public void ApplyAttitude(GimbalAttitude attitude)
    {
        CurrentAttitude = attitude;
    }
}
