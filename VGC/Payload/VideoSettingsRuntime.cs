namespace VGC.Payload;

public sealed record VideoResolution(int Width, int Height)
{
    public string DisplayText => $"{Width}x{Height}";
}

public sealed record VideoSettingsSnapshot(
    VideoResolution Resolution,
    int BitrateKbps,
    int Framerate,
    bool RecordingEnabled,
    string? RecordingPath,
    string? StreamUri)
{
    public string ResolutionText => Resolution.DisplayText;

    public string BitrateText => BitrateKbps >= 1000
        ? $"{BitrateKbps / 1000.0:F1} Mbps"
        : $"{BitrateKbps} Kbps";

    public string FramerateText => $"{Framerate} fps";

    public string RecordingText => RecordingEnabled
        ? RecordingPath is { Length: > 0 } ? $"Recording to {RecordingPath}" : "Recording enabled"
        : "Recording disabled";
}

public sealed class VideoSettingsRuntime
{
    public static IReadOnlyList<VideoResolution> AvailableResolutions { get; } =
    [
        new(640, 480),
        new(1280, 720),
        new(1920, 1080),
        new(2560, 1440),
        new(3840, 2160)
    ];

    private VideoResolution _resolution = new(1280, 720);
    private int _bitrateKbps = 4000;
    private int _framerate = 30;
    private bool _recordingEnabled;
    private string? _recordingPath;
    private string? _streamUri;

    public VideoSettingsSnapshot Snapshot => BuildSnapshot();

    public VideoSettingsSnapshot UpdateResolution(VideoResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        _resolution = resolution;
        return BuildSnapshot();
    }

    public VideoSettingsSnapshot UpdateBitrate(int bitrateKbps)
    {
        if (bitrateKbps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitrateKbps), "Bitrate must be positive.");
        }

        _bitrateKbps = bitrateKbps;
        return BuildSnapshot();
    }

    public VideoSettingsSnapshot UpdateFramerate(int framerate)
    {
        if (framerate is <= 0 or > 120)
        {
            throw new ArgumentOutOfRangeException(nameof(framerate), "Framerate must be between 1 and 120.");
        }

        _framerate = framerate;
        return BuildSnapshot();
    }

    public VideoSettingsSnapshot ToggleRecording()
    {
        _recordingEnabled = !_recordingEnabled;
        return BuildSnapshot();
    }

    public VideoSettingsSnapshot SetRecordingPath(string? path)
    {
        _recordingPath = path;
        return BuildSnapshot();
    }

    public VideoSettingsSnapshot SetStreamUri(string? uri)
    {
        _streamUri = uri;
        return BuildSnapshot();
    }

    private VideoSettingsSnapshot BuildSnapshot()
    {
        return new VideoSettingsSnapshot(
            _resolution,
            _bitrateKbps,
            _framerate,
            _recordingEnabled,
            _recordingPath,
            _streamUri);
    }
}
