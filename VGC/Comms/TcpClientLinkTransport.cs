using System.Net.Sockets;

namespace VGC.Comms;

public sealed class TcpClientLinkTransport : ILinkTransport
{
    private readonly TcpLinkConfiguration _configuration;
    private readonly object _syncRoot = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _receiveLoopCancellation;
    private Task? _receiveLoopTask;

    public TcpClientLinkTransport(TcpLinkConfiguration configuration)
    {
        if (configuration.IsServer)
        {
            throw new ArgumentException("TCP client transport requires client mode configuration.", nameof(configuration));
        }

        _configuration = configuration;
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration => _configuration;

    public bool IsConnected { get; private set; }

    public bool CanSend => IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (IsConnected)
            {
                return;
            }

            _client = new TcpClient();
        }

        try
        {
            await _client.ConnectAsync(_configuration.Host, _configuration.Port, cancellationToken).ConfigureAwait(false);

            lock (_syncRoot)
            {
                _stream = _client.GetStream();
                _receiveLoopCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_receiveLoopCancellation.Token), CancellationToken.None);
                IsConnected = true;
            }
        }
        catch
        {
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        Task? receiveLoopTask;
        lock (_syncRoot)
        {
            if (!IsConnected && _client is null)
            {
                return;
            }

            IsConnected = false;
            _receiveLoopCancellation?.Cancel();
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
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
            CommunicationError?.Invoke(this, "TCP client link is not connected.");
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
        _receiveLoopCancellation?.Dispose();
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
                    IsConnected = false;
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
                IsConnected = false;
                CommunicationError?.Invoke(this, ex.Message);
                return;
            }
            catch (SocketException ex)
            {
                IsConnected = false;
                CommunicationError?.Invoke(this, ex.Message);
                return;
            }
        }
    }
}
