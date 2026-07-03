using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Payload;

public sealed class MavlinkCameraService : ICameraService
{
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly ILinkTransport _link;
    private readonly MavlinkCommandService _commandService;

    public MavlinkCameraService(
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

    public CameraStatus CurrentStatus { get; private set; } = new(
        SystemId: 0,
        ComponentId: 0,
        IsReady: false,
        IsCapturingImage: false,
        IsRecordingVideo: false);

    public Task<CameraStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CurrentStatus);
    }

    public async Task StartImageCaptureAsync(CancellationToken cancellationToken = default)
    {
        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: MavlinkCommandIds.ImageStartCapture,
            Confirmation: 0,
            Param1: 0,
            Param2: 1);

        await _commandService.SendCommandLongAsync(_link, command, cancellationToken).ConfigureAwait(false);
        CurrentStatus = CurrentStatus with { IsCapturingImage = true };
    }

    public async Task StartVideoRecordingAsync(CancellationToken cancellationToken = default)
    {
        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: MavlinkCommandIds.VideoStartCapture,
            Confirmation: 0);

        await _commandService.SendCommandLongAsync(_link, command, cancellationToken).ConfigureAwait(false);
        CurrentStatus = CurrentStatus with { IsRecordingVideo = true };
    }

    public async Task StopVideoRecordingAsync(CancellationToken cancellationToken = default)
    {
        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: MavlinkCommandIds.VideoStopCapture,
            Confirmation: 0);

        await _commandService.SendCommandLongAsync(_link, command, cancellationToken).ConfigureAwait(false);
        CurrentStatus = CurrentStatus with { IsRecordingVideo = false };
    }

    public void ApplyCameraStatus(CameraStatus status)
    {
        CurrentStatus = status;
    }
}
