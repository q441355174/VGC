using System.Net.Sockets;

namespace VGC.Payload;

public sealed class DesktopVideoService : IVideoService, IAsyncDisposable
{
    private readonly VideoStreamRuntimeController _controller = new();
    private CancellationTokenSource? _streamCancellation;

    public VideoStreamRuntimeState RuntimeState => _controller.State;

    public void ConfigureStream(VideoStreamDescriptor descriptor)
    {
        var current = _controller.State;
        var streams = current.Streams.ToList();
        if (streams.Any(s => s.Id == descriptor.Id))
        {
            throw new InvalidOperationException($"Video stream '{descriptor.Id}' is already configured.");
        }

        streams.Add(descriptor);
        _controller.LoadStreams(streams, current.SelectedStream?.Id);
    }

    public void RemoveStream(string streamId)
    {
        var current = _controller.State;
        var streams = current.Streams.Where(s => s.Id != streamId).ToList();
        _controller.LoadStreams(streams, current.SelectedStream?.Id == streamId ? null : current.SelectedStream?.Id);
    }

    public async Task StartStreamAsync(VideoStreamDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var current = _controller.State;
        if (current.Status is VideoStreamRuntimeStatus.Connecting or VideoStreamRuntimeStatus.Streaming)
        {
            throw new InvalidOperationException($"Cannot start stream when current status is {current.Status}.");
        }

        _controller.LoadStreams([descriptor], null);
        _controller.SelectStream(descriptor.Id);
        _controller.StartConnecting();

        _streamCancellation?.Dispose();
        _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await ProbeConnectionAsync(descriptor, _streamCancellation.Token).ConfigureAwait(false);
            _controller.MarkStreaming();
        }
        catch (OperationCanceledException)
        {
            _controller.Fail("Stream connection cancelled.");
        }
        catch (Exception ex)
        {
            _controller.Fail(ex.Message);
        }
    }

    public Task StopStreamAsync()
    {
        _streamCancellation?.Cancel();
        _streamCancellation?.Dispose();
        _streamCancellation = null;
        _controller.Stop();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VideoStreamDescriptor>> DiscoverStreamsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<VideoStreamDescriptor>>(_controller.State.Streams.ToArray());
    }

    public Task<VideoStreamState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = _controller.State;
        var videoState = new VideoStreamState(
            state.SelectedStream,
            state.Status == VideoStreamRuntimeStatus.Streaming,
            state.Error);
        return Task.FromResult(videoState);
    }

    public ValueTask DisposeAsync()
    {
        _streamCancellation?.Cancel();
        _streamCancellation?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static async Task ProbeConnectionAsync(VideoStreamDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (descriptor.Protocol == VideoStreamProtocol.Udp)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (descriptor.Uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase))
        {
            var host = descriptor.Uri.Host;
            var port = descriptor.Uri.Port > 0 ? descriptor.Uri.Port : 554;
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
            return;
        }

        await Task.Delay(50, cancellationToken).ConfigureAwait(false);
    }
}
