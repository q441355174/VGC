namespace VGC.Payload;

public enum VideoBackendKind
{
    PlatformMedia,
    FFmpeg,
    GStreamer,
    ExternalProcess
}

public sealed record VideoBackendOption(
    VideoBackendKind Kind,
    string Name,
    bool SupportsDesktop,
    bool SupportsAndroid,
    bool SupportsRtsp,
    bool SupportsUdp,
    bool SupportsUvc,
    bool SupportsHardwareDecode,
    string LicensingNotes);

public sealed record VideoBackendDecision(
    VideoBackendOption Selected,
    IReadOnlyList<VideoBackendOption> Candidates,
    string Rationale,
    IReadOnlyList<string> DeferredRisks);

public sealed class VideoBackendDecisionService
{
    public static IReadOnlyList<VideoBackendOption> DefaultCandidates { get; } =
    [
        new(VideoBackendKind.PlatformMedia, "Platform Media", true, true, true, false, true, true, "Uses OS media APIs where available."),
        new(VideoBackendKind.FFmpeg, "FFmpeg Boundary", true, true, true, true, true, true, "Requires packaging and license review before bundling binaries."),
        new(VideoBackendKind.GStreamer, "GStreamer Boundary", true, true, true, true, true, true, "Requires plugin packaging, Android ABI handling, and license review."),
        new(VideoBackendKind.ExternalProcess, "External Process Probe", true, false, true, true, false, false, "Desktop diagnostic path only; not an Android runtime plan.")
    ];

    public VideoBackendDecision Decide(bool requireUdp, bool requireAndroid, bool preferBundledBinaries)
    {
        var candidates = DefaultCandidates
            .Where(option => !requireAndroid || option.SupportsAndroid)
            .Where(option => !requireUdp || option.SupportsUdp)
            .ToArray();

        var selected = candidates.FirstOrDefault(option => preferBundledBinaries && option.Kind == VideoBackendKind.FFmpeg)
            ?? candidates.FirstOrDefault(option => option.Kind == VideoBackendKind.GStreamer)
            ?? candidates.FirstOrDefault()
            ?? DefaultCandidates[0];

        var risks = new List<string>();
        if (selected.Kind is VideoBackendKind.FFmpeg or VideoBackendKind.GStreamer)
        {
            risks.Add("Native binary packaging and license review required before release.");
        }

        if (requireAndroid)
        {
            risks.Add("Android hardware decoder and lifecycle validation require physical-device evidence.");
        }

        return new VideoBackendDecision(
            selected,
            candidates,
            $"Selected {selected.Name} for shared decode boundary planning.",
            risks);
    }
}

public enum VideoDecodePipelineState
{
    Idle,
    Opening,
    Decoding,
    Stalled,
    Failed,
    Closed
}

public sealed record VideoDecodePipelineConfig(
    VideoStreamDescriptor Stream,
    VideoBackendKind Backend,
    string Codec,
    bool HardwareAcceleration);

public sealed record VideoDecodePipelineSnapshot(
    VideoDecodePipelineConfig Config,
    VideoDecodePipelineState State,
    long DecodedFrames,
    TimeSpan LastFrameAge,
    string? Error)
{
    public bool IsHealthy => State == VideoDecodePipelineState.Decoding && LastFrameAge <= TimeSpan.FromSeconds(1);
}

public sealed class VideoDecodePipelineRuntime
{
    private VideoDecodePipelineState _state = VideoDecodePipelineState.Idle;
    private long _frames;
    private TimeSpan _lastFrameAge = TimeSpan.MaxValue;
    private string? _error;

    public VideoDecodePipelineRuntime(VideoDecodePipelineConfig config)
    {
        Config = config;
    }

    public VideoDecodePipelineConfig Config { get; }

    public VideoDecodePipelineSnapshot Snapshot => BuildSnapshot();

    public VideoDecodePipelineSnapshot Open()
    {
        _state = VideoDecodePipelineState.Opening;
        _error = null;
        return BuildSnapshot();
    }

    public VideoDecodePipelineSnapshot ReportFrame(TimeSpan lastFrameAge)
    {
        _state = VideoDecodePipelineState.Decoding;
        _frames++;
        _lastFrameAge = lastFrameAge;
        _error = null;
        return BuildSnapshot();
    }

    public VideoDecodePipelineSnapshot MarkStalled(TimeSpan lastFrameAge)
    {
        _state = VideoDecodePipelineState.Stalled;
        _lastFrameAge = lastFrameAge;
        return BuildSnapshot();
    }

    public VideoDecodePipelineSnapshot Fail(string error)
    {
        _state = VideoDecodePipelineState.Failed;
        _error = string.IsNullOrWhiteSpace(error) ? "Video decode failed" : error;
        return BuildSnapshot();
    }

    public VideoDecodePipelineSnapshot Close()
    {
        _state = VideoDecodePipelineState.Closed;
        return BuildSnapshot();
    }

    private VideoDecodePipelineSnapshot BuildSnapshot()
    {
        return new VideoDecodePipelineSnapshot(Config, _state, _frames, _lastFrameAge, _error);
    }
}

public enum UvcDeviceState
{
    Disconnected,
    Discovered,
    PermissionRequired,
    Ready,
    Streaming,
    Failed
}

public sealed record UvcVideoFormat(int Width, int Height, int FrameRate, string PixelFormat);

public sealed record UvcDeviceDescriptor(
    string DeviceId,
    string DisplayName,
    IReadOnlyList<UvcVideoFormat> Formats,
    bool RequiresPermission);

public sealed record UvcDeviceRuntimeState(
    UvcDeviceDescriptor Device,
    UvcDeviceState State,
    UvcVideoFormat? SelectedFormat,
    string? Error);

public sealed class UvcDeviceRuntime
{
    public UvcDeviceRuntime(UvcDeviceDescriptor device)
    {
        State = new UvcDeviceRuntimeState(device, UvcDeviceState.Discovered, null, null);
    }

    public UvcDeviceRuntimeState State { get; private set; }

    public UvcDeviceRuntimeState RequestPermission()
    {
        State = State with { State = State.Device.RequiresPermission ? UvcDeviceState.PermissionRequired : UvcDeviceState.Ready };
        return State;
    }

    public UvcDeviceRuntimeState GrantPermission()
    {
        State = State with { State = UvcDeviceState.Ready, Error = null };
        return State;
    }

    public UvcDeviceRuntimeState Start(UvcVideoFormat format)
    {
        if (!State.Device.Formats.Contains(format))
        {
            State = State with { State = UvcDeviceState.Failed, Error = "UVC format is not supported by the device." };
            return State;
        }

        State = State with { State = UvcDeviceState.Streaming, SelectedFormat = format, Error = null };
        return State;
    }

    public UvcDeviceRuntimeState Disconnect()
    {
        State = State with { State = UvcDeviceState.Disconnected, SelectedFormat = null };
        return State;
    }
}

public enum VideoDisplayMode
{
    Hidden,
    FullFrame,
    PictureInPicture
}

public sealed record VideoDisplayLayoutState(
    VideoDisplayMode Mode,
    string ActiveStreamName,
    double WidthRatio,
    double HeightRatio,
    string Placement)
{
    public bool IsPictureInPicture => Mode == VideoDisplayMode.PictureInPicture;
}

public sealed class VideoDisplayLayoutProjector
{
    public VideoDisplayLayoutState Project(VideoStreamRuntimeState video, bool preferPip)
    {
        if (video.SelectedStream is null || video.Status == VideoStreamRuntimeStatus.Unavailable)
        {
            return new VideoDisplayLayoutState(VideoDisplayMode.Hidden, "No stream", 0, 0, "Hidden");
        }

        return preferPip
            ? new VideoDisplayLayoutState(VideoDisplayMode.PictureInPicture, video.SelectedStream.Name, 0.32, 0.24, "BottomRight")
            : new VideoDisplayLayoutState(VideoDisplayMode.FullFrame, video.SelectedStream.Name, 1, 1, "FullFrame");
    }
}

public sealed record PayloadMediaOutputPlan(
    PayloadStoragePlan StoragePlan,
    string MediaId,
    DateTimeOffset CreatedAt,
    bool IsAndroidScopedStorageSafe);

public sealed class PayloadMediaOutputPlanner
{
    private readonly PayloadStoragePlanner _storagePlanner = new();

    public PayloadMediaOutputPlan Plan(
        PayloadStorageRequest request,
        PayloadStoragePolicy policy,
        DateTimeOffset createdAt)
    {
        var storage = _storagePlanner.Plan(request, policy);
        var mediaId = $"{request.Kind}:{createdAt:yyyyMMddHHmmss}:{storage.RelativeFileName}";
        return new PayloadMediaOutputPlan(
            storage,
            mediaId,
            createdAt,
            policy.Platform != PayloadStoragePlatform.Android || policy.RequiresScopedStorage);
    }
}

public enum ThermalPalette
{
    WhiteHot,
    BlackHot,
    Ironbow,
    Rainbow
}

public sealed record ThermalStreamMetadata(
    string StreamId,
    ThermalPalette Palette,
    double MinTemperatureC,
    double MaxTemperatureC,
    bool Radiometric,
    string? Unit = "C")
{
    public bool IsValid => MaxTemperatureC > MinTemperatureC;
}

public sealed record ThermalStreamUiState(
    string StreamId,
    string PaletteText,
    string TemperatureRangeText,
    bool HasRadiometricData);

public sealed class ThermalStreamProjector
{
    public ThermalStreamUiState Project(ThermalStreamMetadata metadata)
    {
        return new ThermalStreamUiState(
            metadata.StreamId,
            metadata.Palette.ToString(),
            $"{metadata.MinTemperatureC:F1}-{metadata.MaxTemperatureC:F1} {metadata.Unit}",
            metadata.Radiometric);
    }
}

public sealed record CameraSettingsValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    string Summary);

public sealed class CameraSettingsValidator
{
    public CameraSettingsValidationResult Validate(CameraDefinition definition, CameraSettings settings)
    {
        var errors = new List<string>();
        if (!string.Equals(definition.Id, settings.CameraId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Settings camera id does not match the selected camera definition.");
        }

        if (settings.IntervalSeconds is <= 0)
        {
            errors.Add("Interval must be positive when provided.");
        }

        if (settings.ExposureSeconds is <= 0)
        {
            errors.Add("Exposure must be positive when provided.");
        }

        if (settings.Iso is <= 0)
        {
            errors.Add("ISO must be positive when provided.");
        }

        return new CameraSettingsValidationResult(
            errors.Count == 0,
            errors,
            errors.Count == 0 ? $"{definition.Name} settings valid" : string.Join(" | ", errors));
    }
}

public sealed record GimbalRoiTarget(
    double Latitude,
    double Longitude,
    double? AltitudeMeters,
    string Label);

public sealed record GimbalRoiLinkState(
    GimbalRoiTarget? Target,
    GimbalCommand? Command,
    bool IsLinkedToMode,
    string Summary);

public sealed class GimbalRoiLinkController
{
    public GimbalRoiLinkState State { get; private set; } = new(null, null, false, "No ROI target");

    public GimbalRoiLinkState SetRoi(GimbalRoiTarget target, double pitchDegrees, double yawDegrees, bool linkToMode)
    {
        var command = new GimbalCommand(pitchDegrees, yawDegrees, LockYaw: true);
        State = new GimbalRoiLinkState(
            target,
            command,
            linkToMode,
            $"ROI {target.Label} pitch {pitchDegrees:F1} yaw {yawDegrees:F1}");
        return State;
    }

    public GimbalRoiLinkState Clear()
    {
        State = new GimbalRoiLinkState(null, null, false, "No ROI target");
        return State;
    }
}

public sealed record PayloadRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class PayloadRuntimeEvidenceCatalog
{
    public IReadOnlyList<PayloadRuntimeEvidenceItem> Build()
    {
        return
        [
            new("PAYLOADFULL-243", "L0", "Video backend decision matrix records platform, FFmpeg, GStreamer, and external-process tradeoffs.", true),
            new("PAYLOADFULL-244", "L1/L6", "Decode pipeline boundary models RTSP/UDP stream state; real decode remains device/media evidence.", true),
            new("PAYLOADFULL-245", "L1/L6", "UVC discovery, permission, format selection, streaming, and disconnect states are modeled.", true),
            new("PAYLOADFULL-246", "L2/L3/L6", "Video display/PiP projection is shared; FlyView AXAML and runtime screenshots remain deferred.", true),
            new("PAYLOADFULL-247", "L1/L6", "Snapshot/recording output planning covers naming and Android scoped-storage risk.", true),
            new("PAYLOADFULL-248", "L1/L6", "Thermal stream palette and temperature metadata are modeled.", true),
            new("PAYLOADFULL-249", "L1/L5", "Camera definition settings validation is covered by shared tests.", true),
            new("PAYLOADFULL-250", "L1/L5", "Gimbal ROI target and command linkage are modeled.", true),
            new("PAYLOADFULL-251", "L0/L6", "Real stream, camera, and gimbal hardware evidence remains deferred.", false)
        ];
    }
}

public sealed record PayloadRuntimeAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class PayloadRuntimeParityAudit
{
    public PayloadRuntimeAuditResult Audit(IReadOnlyList<PayloadRuntimeEvidenceItem> evidence)
    {
        var complete = evidence.Count(static item => item.Complete);
        var deferred = evidence.Where(static item => !item.Complete).Select(static item => item.Description).ToArray();
        return new PayloadRuntimeAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} payload runtime evidence items complete; {deferred.Length} real media/device gaps remain.");
    }
}
