namespace VGC.Comms;

public sealed class BluetoothLinkConfiguration : LinkConfiguration
{
    public BluetoothLinkConfiguration(string name, string address, string deviceName, bool isLowEnergy = false)
        : base(name, LinkType.Bluetooth)
    {
        Address = address;
        DeviceName = deviceName;
        IsLowEnergy = isLowEnergy;
    }

    public string Address { get; }

    public string DeviceName { get; }

    public bool IsLowEnergy { get; }
}

public sealed record BluetoothDevice(
    string Name,
    string Address,
    bool IsLowEnergy,
    bool IsPaired);

public interface IBluetoothPlatform
{
    event EventHandler<byte[]>? DataReceived;

    Task<IReadOnlyList<BluetoothDevice>> ScanAsync(CancellationToken cancellationToken = default);

    Task ConnectAsync(BluetoothDevice device, bool lowEnergy, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}

public sealed class BluetoothLinkTransport : ILinkTransport
{
    private readonly BluetoothLinkConfiguration _configuration;
    private readonly IBluetoothPlatform _platform;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _connectionCancellation;
    private bool _connected;

    public BluetoothLinkTransport(BluetoothLinkConfiguration configuration, IBluetoothPlatform platform)
    {
        if (string.IsNullOrWhiteSpace(configuration.Address))
        {
            throw new ArgumentException("Bluetooth address is required.", nameof(configuration));
        }

        _configuration = configuration;
        _platform = platform;
        _platform.DataReceived += OnPlatformDataReceived;
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration => _configuration;

    public bool IsConnected
    {
        get
        {
            lock (_syncRoot)
            {
                return _connected;
            }
        }
        private set
        {
            lock (_syncRoot)
            {
                _connected = value;
            }
        }
    }

    public bool CanSend => IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        lock (_syncRoot)
        {
            if (_connected)
            {
                return;
            }

            _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            var device = new BluetoothDevice(
                _configuration.DeviceName,
                _configuration.Address,
                _configuration.IsLowEnergy,
                IsPaired: true);

            await _platform.ConnectAsync(device, _configuration.IsLowEnergy, _connectionCancellation.Token).ConfigureAwait(false);
            IsConnected = true;
        }
        catch (Exception ex)
        {
            await DisconnectAsync().ConfigureAwait(false);
            CommunicationError?.Invoke(this, $"Bluetooth connect failed: {ex.Message}");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        CancellationTokenSource? cts;
        lock (_syncRoot)
        {
            if (!_connected && _connectionCancellation is null)
            {
                return;
            }

            _connected = false;
            cts = _connectionCancellation;
            _connectionCancellation = null;
        }

        cts?.Cancel();

        try
        {
            await _platform.DisconnectAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            CommunicationError?.Invoke(this, $"Bluetooth disconnect error: {ex.Message}");
        }
        finally
        {
            cts?.Dispose();
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            CommunicationError?.Invoke(this, "Bluetooth link is not connected.");
            return;
        }

        try
        {
            await _platform.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            BytesSent?.Invoke(this, new BytesReceivedEventArgs(this, bytes.ToArray()));
        }
        catch (Exception ex)
        {
            IsConnected = false;
            CommunicationError?.Invoke(this, $"Bluetooth write failed: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _platform.DataReceived -= OnPlatformDataReceived;
        await DisconnectAsync().ConfigureAwait(false);
    }

    private void OnPlatformDataReceived(object? sender, byte[] data)
    {
        if (!IsConnected)
        {
            return;
        }

        BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, data));
    }
}
