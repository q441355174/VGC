using VGC.Comms;
using VGC.Core.Logging;

namespace VGC.Mavlink;

public sealed class GcsHeartbeatService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(1);
    private readonly LinkManager _linkManager;
    private readonly MavlinkOutboundRouter _outboundRouter;
    private readonly IAppLogger _logger;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _sendLoopTask;

    public GcsHeartbeatService(
        LinkManager linkManager,
        MavlinkFrameWriter frameWriter,
        IAppLogger logger,
        MavlinkOutboundRouter? outboundRouter = null)
    {
        _linkManager = linkManager;
        _outboundRouter = outboundRouter ?? new MavlinkOutboundRouter(frameWriter);
        _logger = logger;
    }

    public bool IsRunning { get; private set; }

    public uint HeartbeatsSent { get; private set; }

    public event EventHandler? HeartbeatSent;

    public Task StartAsync()
    {
        if (IsRunning)
        {
            return Task.CompletedTask;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _sendLoopTask = Task.Run(() => SendLoopAsync(_cancellationTokenSource.Token));
        IsRunning = true;
        _logger.Info("GCS heartbeat sender started.");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            return;
        }

        _cancellationTokenSource?.Cancel();
        if (_sendLoopTask is not null)
        {
            try
            {
                await _sendLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        _sendLoopTask = null;
        IsRunning = false;
        _logger.Info("GCS heartbeat sender stopped.");
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await SendHeartbeatToConnectedLinksAsync(cancellationToken).ConfigureAwait(false);
            await Task.Delay(HeartbeatInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SendHeartbeatToConnectedLinksAsync(CancellationToken cancellationToken)
    {
        foreach (var link in _linkManager.Links.Where(link => link is { IsConnected: true, CanSend: true }).ToArray())
        {
            await _outboundRouter.SendGcsHeartbeatAsync(link, cancellationToken: cancellationToken).ConfigureAwait(false);
            HeartbeatsSent++;
            HeartbeatSent?.Invoke(this, EventArgs.Empty);
        }
    }
}
