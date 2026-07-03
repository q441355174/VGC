using System.Net;
using System.Net.Sockets;

namespace VGC.Comms;

public sealed class TcpServerLinkTransport : ILinkTransport
{
    private readonly TcpLinkConfiguration _configuration;
    private readonly object _syncRoot = new();
    private TcpListener? _listener;
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _lifecycleCancellation;
    private Task? _acceptLoopTask;

    public TcpServerLinkTransport(TcpLinkConfiguration configuration)
    {
        if (!configuration.IsServer)
        {
            throw new ArgumentException("TCP server transport requires server mode configuration.", nameof(configuration));
        }

        _configuration = configuration;
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration => _configuration;

    public bool IsConnected { get; private set; }

    public bool CanSend => IsConnected && _stream is not null;

    public int BoundPort
    {
        get
        {
            lock (_syncRoot)
            {
                return _listener?.LocalEndpoint is IPEndPoint endpoint ? endpoint.Port : _configuration.Port;
            }
        }
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (IsConnected)
            {
                return Task.CompletedTask;
            }

            _listener = new TcpListener(ResolveListenAddress(_configuration.Host), _configuration.Port);
            _listener.Start();
            _lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _acceptLoopTask = Task.Run(() => AcceptLoopAsync(_lifecycleCancellation.Token), CancellationToken.None);
            IsConnected = true;
        }

        return Task.CompletedTask;
    }

    public async Task DisconnectAsync()
    {
        Task? acceptLoopTask;
        lock (_syncRoot)
        {
            if (!IsConnected && _listener is null && _client is null)
            {
                return;
            }

            IsConnected = false;
            _lifecycleCancellation?.Cancel();
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
            _client?.Close();
            _client?.Dispose();
            _client = null;
            _listener?.Stop();
            _listener = null;
            acceptLoopTask = _acceptLoopTask;
        }

        if (acceptLoopTask is not null)
        {
            try
            {
                await acceptLoopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            catch (IOException)
            {
            }
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        NetworkStream? stream;
        lock (_syncRoot)
        {
            stream = _stream;
        }

        if (stream is null || !IsConnected)
        {
            CommunicationError?.Invoke(this, "TCP server link has no accepted client.");
            return;
        }

        var array = bytes.ToArray();
        await stream.WriteAsync(array, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        BytesSent?.Invoke(this, new BytesReceivedEventArgs(this, array));
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _lifecycleCancellation?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        TcpListener? listener;
        lock (_syncRoot)
        {
            listener = _listener;
        }

        if (listener is null)
        {
            return;
        }

        try
        {
            var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            lock (_syncRoot)
            {
                _client = client;
                _stream = client.GetStream();
            }

            await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                CommunicationError?.Invoke(this, ex.Message);
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested)
        {
            NetworkStream? stream;
            lock (_syncRoot)
            {
                stream = _stream;
            }

            if (stream is null)
            {
                return;
            }

            try
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    lock (_syncRoot)
                    {
                        _stream?.Dispose();
                        _stream = null;
                        _client?.Dispose();
                        _client = null;
                    }

                    return;
                }

                BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, buffer[..read]));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (IOException ex)
            {
                CommunicationError?.Invoke(this, ex.Message);
                return;
            }
            catch (SocketException ex)
            {
                CommunicationError?.Invoke(this, ex.Message);
                return;
            }
        }
    }

    private static IPAddress ResolveListenAddress(string host)
    {
        if (string.IsNullOrWhiteSpace(host) || host == "0.0.0.0")
        {
            return IPAddress.Any;
        }

        return IPAddress.TryParse(host, out var address)
            ? address
            : Dns.GetHostAddresses(host)[0];
    }
}
