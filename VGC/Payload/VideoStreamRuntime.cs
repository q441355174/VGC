namespace VGC.Payload;

public enum VideoStreamRuntimeStatus
{
    Unavailable,
    Stopped,
    Connecting,
    Streaming,
    Error
}

public sealed record VideoStreamRuntimeState(
    IReadOnlyList<VideoStreamDescriptor> Streams,
    VideoStreamDescriptor? SelectedStream,
    VideoStreamRuntimeStatus Status,
    string? Error = null)
{
    public static VideoStreamRuntimeState Empty { get; } = new([], null, VideoStreamRuntimeStatus.Unavailable);

    public bool HasStreams => Streams.Count > 0;

    public string StatusText => Status switch
    {
        VideoStreamRuntimeStatus.Unavailable => "Video unavailable",
        VideoStreamRuntimeStatus.Stopped => "Video stopped",
        VideoStreamRuntimeStatus.Connecting => "Video connecting",
        VideoStreamRuntimeStatus.Streaming => "Video streaming",
        VideoStreamRuntimeStatus.Error => Error is { Length: > 0 } ? $"Video error: {Error}" : "Video error",
        _ => "Video unknown"
    };
}

public sealed class VideoStreamRuntimeController
{
    private readonly List<VideoStreamDescriptor> _streams = [];
    private VideoStreamDescriptor? _selectedStream;
    private VideoStreamRuntimeStatus _status = VideoStreamRuntimeStatus.Unavailable;
    private string? _error;

    public VideoStreamRuntimeState State => Snapshot();

    public VideoStreamRuntimeState LoadStreams(IEnumerable<VideoStreamDescriptor> streams, string? preferredStreamId = null)
    {
        var loaded = streams
            .Where(static stream => !string.IsNullOrWhiteSpace(stream.Id))
            .GroupBy(static stream => stream.Id, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        _streams.Clear();
        _streams.AddRange(loaded);

        if (_streams.Count == 0)
        {
            _selectedStream = null;
            _status = VideoStreamRuntimeStatus.Unavailable;
            _error = null;
            return Snapshot();
        }

        _selectedStream = ResolveSelection(preferredStreamId)
            ?? ResolveSelection(_selectedStream?.Id)
            ?? _streams[0];
        _status = VideoStreamRuntimeStatus.Stopped;
        _error = null;
        return Snapshot();
    }

    public VideoStreamRuntimeState SelectStream(string streamId)
    {
        var stream = ResolveSelection(streamId);
        if (stream is null)
        {
            _status = _streams.Count == 0
                ? VideoStreamRuntimeStatus.Unavailable
                : VideoStreamRuntimeStatus.Error;
            _error = _streams.Count == 0
                ? "No video streams available"
                : $"Video stream '{streamId}' was not found";
            return Snapshot();
        }

        _selectedStream = stream;
        _status = VideoStreamRuntimeStatus.Stopped;
        _error = null;
        return Snapshot();
    }

    public VideoStreamRuntimeState StartConnecting()
    {
        if (_selectedStream is null)
        {
            _status = VideoStreamRuntimeStatus.Unavailable;
            _error = "No video stream selected";
            return Snapshot();
        }

        _status = VideoStreamRuntimeStatus.Connecting;
        _error = null;
        return Snapshot();
    }

    public VideoStreamRuntimeState MarkStreaming()
    {
        if (_selectedStream is null)
        {
            _status = VideoStreamRuntimeStatus.Unavailable;
            _error = "No video stream selected";
            return Snapshot();
        }

        _status = VideoStreamRuntimeStatus.Streaming;
        _error = null;
        return Snapshot();
    }

    public VideoStreamRuntimeState Stop()
    {
        _status = _selectedStream is null
            ? VideoStreamRuntimeStatus.Unavailable
            : VideoStreamRuntimeStatus.Stopped;
        _error = null;
        return Snapshot();
    }

    public VideoStreamRuntimeState Fail(string error)
    {
        _status = VideoStreamRuntimeStatus.Error;
        _error = string.IsNullOrWhiteSpace(error) ? "Unknown video stream failure" : error;
        return Snapshot();
    }

    public VideoStreamRuntimeState ApplyServiceState(VideoStreamState serviceState)
    {
        if (serviceState.ActiveStream is { } activeStream && ResolveSelection(activeStream.Id) is null)
        {
            _streams.Add(activeStream);
        }

        _selectedStream = serviceState.ActiveStream is { } stream
            ? ResolveSelection(stream.Id) ?? stream
            : _selectedStream;

        if (serviceState.Error is { Length: > 0 } error)
        {
            _status = VideoStreamRuntimeStatus.Error;
            _error = error;
        }
        else if (_selectedStream is null)
        {
            _status = VideoStreamRuntimeStatus.Unavailable;
            _error = null;
        }
        else
        {
            _status = serviceState.IsStreaming
                ? VideoStreamRuntimeStatus.Streaming
                : VideoStreamRuntimeStatus.Stopped;
            _error = null;
        }

        return Snapshot();
    }

    private VideoStreamDescriptor? ResolveSelection(string? streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId))
        {
            return null;
        }

        return _streams.FirstOrDefault(stream => string.Equals(stream.Id, streamId, StringComparison.OrdinalIgnoreCase));
    }

    private VideoStreamRuntimeState Snapshot()
    {
        return new VideoStreamRuntimeState(_streams.ToArray(), _selectedStream, _status, _error);
    }
}
