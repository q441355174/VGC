using System.Net.Sockets;
using System.Text;

namespace VGC.Positioning;

public sealed record NtripConfiguration(
    string Host,
    int Port,
    string Mountpoint,
    string? Username,
    string? Password);

public sealed record NtripMountpoint(
    string Name,
    string Format,
    string Details);

public enum NtripClientState
{
    Disconnected,
    Connecting,
    Connected,
    Streaming,
    Failed
}

public sealed class NtripClient : IAsyncDisposable
{
    private readonly object _syncRoot = new();
    private TcpClient? _client;
    private NetworkStream? _stream;
    private CancellationTokenSource? _streamCancellation;
    private Task? _streamTask;
    private NtripConfiguration? _configuration;

    public event EventHandler<byte[]>? RtcmDataReceived;

    public event EventHandler<NtripClientState>? StateChanged;

    public NtripClientState State { get; private set; } = NtripClientState.Disconnected;

    public long TotalBytesReceived { get; private set; }

    public int RtcmPacketCount { get; private set; }

    public string? LastError { get; private set; }

    public async Task<IReadOnlyList<NtripMountpoint>> GetMountpointsAsync(
        string host,
        int port,
        CancellationToken cancellationToken = default)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        await using var stream = client.GetStream();

        var request = $"GET / HTTP/1.1\r\nHost: {host}:{port}\r\nNtrip-Version: Ntrip/2.0\r\nUser-Agent: VGC\r\n\r\n";
        var requestBytes = Encoding.ASCII.GetBytes(request);
        await stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        var responseBuffer = new byte[16384];
        var read = await stream.ReadAsync(responseBuffer, cancellationToken).ConfigureAwait(false);
        if (read == 0)
        {
            return [];
        }

        var response = Encoding.ASCII.GetString(responseBuffer, 0, read);
        return ParseSourceTable(response);
    }

    public async Task ConnectAsync(NtripConfiguration configuration, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(configuration.Host) ||
            configuration.Port <= 0 ||
            string.IsNullOrWhiteSpace(configuration.Mountpoint))
        {
            throw new ArgumentException("NTRIP host, port, and mountpoint are required.");
        }

        lock (_syncRoot)
        {
            if (State is NtripClientState.Connecting or NtripClientState.Connected or NtripClientState.Streaming)
            {
                return;
            }

            _configuration = configuration;
        }

        SetState(NtripClientState.Connecting);

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(configuration.Host, configuration.Port, cancellationToken).ConfigureAwait(false);

            lock (_syncRoot)
            {
                _stream = _client.GetStream();
            }

            var request = BuildMountpointRequest(configuration);
            var requestBytes = Encoding.ASCII.GetBytes(request);
            await _stream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            var headerBuffer = new byte[4096];
            var headerRead = await _stream.ReadAsync(headerBuffer, cancellationToken).ConfigureAwait(false);
            if (headerRead == 0)
            {
                throw new IOException("NTRIP caster closed connection before sending response.");
            }

            var headerResponse = Encoding.ASCII.GetString(headerBuffer, 0, headerRead);
            if (!headerResponse.Contains("200", StringComparison.OrdinalIgnoreCase) &&
                !headerResponse.Contains("ICY", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException($"NTRIP caster rejected connection: {headerResponse.Split('\r', '\n')[0]}");
            }

            SetState(NtripClientState.Connected);

            lock (_syncRoot)
            {
                _streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _streamTask = Task.Run(() => ReceiveLoopAsync(_streamCancellation.Token), CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(NtripClientState.Failed);
            await DisconnectAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        Task? streamTask;
        lock (_syncRoot)
        {
            _streamCancellation?.Cancel();
            _stream?.Close();
            _stream?.Dispose();
            _stream = null;
            _client?.Close();
            _client?.Dispose();
            _client = null;
            streamTask = _streamTask;
            _streamTask = null;
        }

        if (streamTask is not null)
        {
            try
            {
                await streamTask.ConfigureAwait(false);
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

        _streamCancellation?.Dispose();
        _streamCancellation = null;

        if (State != NtripClientState.Failed)
        {
            SetState(NtripClientState.Disconnected);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        SetState(NtripClientState.Streaming);

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
                    SetState(NtripClientState.Disconnected);
                    return;
                }

                TotalBytesReceived += read;
                RtcmPacketCount++;
                RtcmDataReceived?.Invoke(this, buffer[..read]);
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
                LastError = ex.Message;
                SetState(NtripClientState.Failed);
                return;
            }
            catch (SocketException ex)
            {
                LastError = ex.Message;
                SetState(NtripClientState.Failed);
                return;
            }
        }
    }

    private void SetState(NtripClientState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private static string BuildMountpointRequest(NtripConfiguration configuration)
    {
        var sb = new StringBuilder();
        sb.Append($"GET /{configuration.Mountpoint.TrimStart('/')} HTTP/1.1\r\n");
        sb.Append($"Host: {configuration.Host}:{configuration.Port}\r\n");
        sb.Append("Ntrip-Version: Ntrip/2.0\r\n");
        sb.Append("User-Agent: VGC\r\n");

        if (!string.IsNullOrEmpty(configuration.Username))
        {
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{configuration.Username}:{configuration.Password ?? string.Empty}"));
            sb.Append($"Authorization: Basic {credentials}\r\n");
        }

        sb.Append("\r\n");
        return sb.ToString();
    }

    private static IReadOnlyList<NtripMountpoint> ParseSourceTable(string response)
    {
        var mountpoints = new List<NtripMountpoint>();
        var lines = response.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!line.StartsWith("STR;", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fields = line.Split(';');
            if (fields.Length < 4)
            {
                continue;
            }

            mountpoints.Add(new NtripMountpoint(
                Name: fields[1],
                Format: fields.Length > 3 ? fields[3] : string.Empty,
                Details: fields.Length > 2 ? fields[2] : string.Empty));
        }

        return mountpoints;
    }
}
