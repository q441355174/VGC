using System.Net;
using System.Net.Sockets;

namespace VGC.Comms;

public sealed class UdpLinkTransport : ILinkTransport
{
    private readonly UdpLinkConfiguration _configuration;
    private readonly object _syncRoot = new();
    private UdpClient? _client;
    private IPEndPoint? _lastRemoteEndpoint;
    private CancellationTokenSource? _receiveLoopCancellation;
    private Task? _receiveLoopTask;

    public UdpLinkTransport(UdpLinkConfiguration configuration)
    {
        _configuration = configuration;
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration => _configuration;

    public bool IsConnected { get; private set; }

    public bool CanSend => IsConnected && _lastRemoteEndpoint is not null;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (IsConnected)
            {
                return Task.CompletedTask;
            }

            _client = new UdpClient(_configuration.LocalPort);
            if (!string.IsNullOrWhiteSpace(_configuration.TargetHost) && _configuration.TargetPort is { } targetPort)
            {
                _lastRemoteEndpoint = new IPEndPoint(Dns.GetHostAddresses(_configuration.TargetHost)[0], targetPort);
            }

            _receiveLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCancellation.Token), CancellationToken.None);
            IsConnected = true;
        }

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        Task? receiveLoopTask;
        lock (_syncRoot)
        {
            if (!IsConnected)
            {
                return;
            }

            IsConnected = false;
            _receiveLoopCancellation?.Cancel();
            _client?.Close();
            _client?.Dispose();
            _client = null;
            receiveLoopTask = _receiveLoopTask;
        }

        if (receiveLoopTask is not null)
        {
            try
            {
                await receiveLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        UdpClient? client;
        IPEndPoint? endpoint;
        lock (_syncRoot)
        {
            client = _client;
            endpoint = _lastRemoteEndpoint;
        }

        if (client is null || endpoint is null)
        {
            return;
        }

        var array = bytes.ToArray();
        await client.SendAsync(array, endpoint, cancellationToken).ConfigureAwait(false);
        BytesSent?.Invoke(this, new BytesReceivedEventArgs(this, array));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _receiveLoopCancellation?.Dispose();
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpClient? client;
            lock (_syncRoot)
            {
                client = _client;
            }

            if (client is null)
            {
                return;
            }

            try
            {
                var result = await client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                _lastRemoteEndpoint = result.RemoteEndPoint;
                BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, result.Buffer));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                CommunicationError?.Invoke(this, ex.Message);
            }
        }
    }
}
