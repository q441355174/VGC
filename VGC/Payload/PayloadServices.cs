namespace VGC.Payload;

public enum VideoStreamProtocol
{
    Unknown,
    Rtsp,
    Rtp,
    Udp,
    WebRtc
}

public sealed record VideoStreamDescriptor(
    string Id,
    string Name,
    Uri Uri,
    VideoStreamProtocol Protocol,
    string? Encoding = null);

public sealed record VideoStreamState(
    VideoStreamDescriptor? ActiveStream,
    bool IsStreaming,
    string? Error = null);

public interface IVideoService
{
    Task<IReadOnlyList<VideoStreamDescriptor>> DiscoverStreamsAsync(CancellationToken cancellationToken = default);

    Task<VideoStreamState> GetStateAsync(CancellationToken cancellationToken = default);
}

public sealed record CameraStatus(
    byte SystemId,
    byte ComponentId,
    bool IsReady,
    bool IsCapturingImage,
    bool IsRecordingVideo,
    string? Mode = null);

public interface ICameraService
{
    Task<CameraStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task StartImageCaptureAsync(CancellationToken cancellationToken = default);

    Task StartVideoRecordingAsync(CancellationToken cancellationToken = default);

    Task StopVideoRecordingAsync(CancellationToken cancellationToken = default);
}

public sealed record GimbalAttitude(
    double PitchDegrees,
    double RollDegrees,
    double YawDegrees,
    bool IsLocked);

public sealed record GimbalCommand(
    double PitchDegrees,
    double YawDegrees,
    bool LockYaw = false);

public interface IGimbalService
{
    Task<GimbalAttitude> GetAttitudeAsync(CancellationToken cancellationToken = default);

    Task SetAttitudeAsync(GimbalCommand command, CancellationToken cancellationToken = default);
}

public sealed record PayloadServiceSet(
    IVideoService Video,
    ICameraService Camera,
    IGimbalService Gimbal);
