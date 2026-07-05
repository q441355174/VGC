using System.Diagnostics;

namespace VGC.Payload;

public enum VideoProtocol
{
    Rtsp,
    Rtp,
    Udp,
    WebRtc,
    Tcp
}

public enum VideoDecoderState
{
    Idle,
    Connecting,
    Decoding,
    Paused,
    Stopped,
    Failed
}

public sealed record VideoFrame(
    int Width,
    int Height,
    string PixelFormat,
    byte[] Data,
    TimeSpan Timestamp);

public sealed record VideoDecoderStatistics(
    long FrameCount,
    long DroppedFrames,
    double CurrentFps,
    TimeSpan Uptime);

public sealed record VideoDecoderSnapshot(
    VideoDecoderState State,
    string? StreamUri,
    VideoProtocol? Protocol,
    VideoDecoderStatistics Statistics,
    string? LastError)
{
    public bool IsActive => State is VideoDecoderState.Decoding or VideoDecoderState.Connecting;

    public string StatusText => State switch
    {
        VideoDecoderState.Idle => "Decoder idle",
        VideoDecoderState.Connecting => "Decoder connecting",
        VideoDecoderState.Decoding => $"Decoding {Statistics.CurrentFps:F1} fps",
        VideoDecoderState.Paused => "Decoder paused",
        VideoDecoderState.Stopped => "Decoder stopped",
        VideoDecoderState.Failed => LastError is { Length: > 0 } ? $"Decoder failed: {LastError}" : "Decoder failed",
        _ => "Decoder unknown"
    };
}

public interface IVideoDecoder
{
    VideoDecoderState State { get; }

    string? LastError { get; }

    event EventHandler<VideoFrame>? FrameReceived;

    Task StartAsync(string uri, VideoProtocol protocol, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public sealed class NullVideoDecoder : IVideoDecoder
{
    public VideoDecoderState State => VideoDecoderState.Idle;

    public string? LastError => null;

    public event EventHandler<VideoFrame>? FrameReceived;

    public Task StartAsync(string uri, VideoProtocol protocol, CancellationToken cancellationToken = default)
    {
        _ = FrameReceived;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class SyntheticVideoDecoder : IVideoDecoder
{
    private readonly int _frameCount;
    private readonly int _width;
    private readonly int _height;
    private VideoDecoderState _state = VideoDecoderState.Idle;

    public SyntheticVideoDecoder(int frameCount = 3, int width = 16, int height = 16)
    {
        _frameCount = Math.Max(0, frameCount);
        _width = Math.Max(1, width);
        _height = Math.Max(1, height);
    }

    public VideoDecoderState State => _state;

    public string? LastError => null;

    public event EventHandler<VideoFrame>? FrameReceived;

    public Task StartAsync(string uri, VideoProtocol protocol, CancellationToken cancellationToken = default)
    {
        _state = VideoDecoderState.Decoding;
        for (var i = 0; i < _frameCount && !cancellationToken.IsCancellationRequested; i++)
        {
            FrameReceived?.Invoke(this, new VideoFrame(
                _width,
                _height,
                "RGB24",
                new byte[_width * _height * 3],
                TimeSpan.FromMilliseconds(i * 33)));
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _state = VideoDecoderState.Stopped;
        return Task.CompletedTask;
    }
}

public sealed class VideoDecodePipeline : IAsyncDisposable
{
    private readonly IVideoDecoder _decoder;
    private readonly Stopwatch _uptimeWatch = new();

    private string? _streamUri;
    private VideoProtocol? _protocol;
    private VideoDecoderState _state = VideoDecoderState.Idle;
    private string? _lastError;

    private long _frameCount;
    private long _droppedFrames;
    private double _currentFps;

    private long _fpsFrameCount;
    private readonly Stopwatch _fpsWatch = new();

    private CancellationTokenSource? _cancellation;

    public VideoDecodePipeline(IVideoDecoder decoder)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _decoder.FrameReceived += OnFrameReceived;
    }

    public event EventHandler<VideoFrame>? FrameReceived;

    public event EventHandler<VideoDecoderState>? StateChanged;

    public VideoDecoderSnapshot Snapshot => BuildSnapshot();

    public async Task StartAsync(string uri, VideoProtocol protocol, CancellationToken cancellationToken = default)
    {
        if (_state is VideoDecoderState.Decoding or VideoDecoderState.Connecting)
        {
            throw new InvalidOperationException($"Cannot start pipeline when state is {_state}.");
        }

        _streamUri = uri;
        _protocol = protocol;
        _frameCount = 0;
        _droppedFrames = 0;
        _currentFps = 0;
        _fpsFrameCount = 0;
        _lastError = null;

        _cancellation?.Dispose();
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        TransitionState(VideoDecoderState.Connecting);
        _uptimeWatch.Restart();
        _fpsWatch.Restart();

        try
        {
            await _decoder.StartAsync(uri, protocol, _cancellation.Token).ConfigureAwait(false);
            TransitionState(VideoDecoderState.Decoding);
        }
        catch (OperationCanceledException)
        {
            TransitionState(VideoDecoderState.Stopped);
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            TransitionState(VideoDecoderState.Failed);
        }
    }

    public async Task StopAsync()
    {
        _cancellation?.Cancel();

        try
        {
            await _decoder.StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Best-effort stop.
        }

        _uptimeWatch.Stop();
        _fpsWatch.Stop();
        _cancellation?.Dispose();
        _cancellation = null;
        TransitionState(VideoDecoderState.Stopped);
    }

    public void Pause()
    {
        if (_state != VideoDecoderState.Decoding)
        {
            return;
        }

        TransitionState(VideoDecoderState.Paused);
    }

    public void Resume()
    {
        if (_state != VideoDecoderState.Paused)
        {
            return;
        }

        TransitionState(VideoDecoderState.Decoding);
    }

    public ValueTask DisposeAsync()
    {
        _decoder.FrameReceived -= OnFrameReceived;
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _uptimeWatch.Stop();
        _fpsWatch.Stop();
        return ValueTask.CompletedTask;
    }

    private void OnFrameReceived(object? sender, VideoFrame frame)
    {
        if (_state == VideoDecoderState.Paused)
        {
            Interlocked.Increment(ref _droppedFrames);
            return;
        }

        Interlocked.Increment(ref _frameCount);
        Interlocked.Increment(ref _fpsFrameCount);
        UpdateFps();

        FrameReceived?.Invoke(this, frame);
    }

    private void UpdateFps()
    {
        var elapsed = _fpsWatch.Elapsed.TotalSeconds;
        if (elapsed >= 1.0)
        {
            _currentFps = Interlocked.Read(ref _fpsFrameCount) / elapsed;
            Interlocked.Exchange(ref _fpsFrameCount, 0);
            _fpsWatch.Restart();
        }
    }

    private void TransitionState(VideoDecoderState newState)
    {
        if (_state == newState)
        {
            return;
        }

        _state = newState;
        StateChanged?.Invoke(this, newState);
    }

    private VideoDecoderSnapshot BuildSnapshot()
    {
        var statistics = new VideoDecoderStatistics(
            Interlocked.Read(ref _frameCount),
            Interlocked.Read(ref _droppedFrames),
            _currentFps,
            _uptimeWatch.Elapsed);

        return new VideoDecoderSnapshot(
            _state,
            _streamUri,
            _protocol,
            statistics,
            _lastError);
    }
}
