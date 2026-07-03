namespace VGC.Comms;

public interface ISerialPortAdapter : IAsyncDisposable
{
    event EventHandler<byte[]>? BytesReceived;

    bool IsOpen { get; }

    Task OpenAsync(SerialLinkConfiguration configuration, CancellationToken cancellationToken = default);

    Task CloseAsync();

    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}

public sealed class SerialLinkTransport : ILinkTransport
{
    private readonly SerialLinkConfiguration _configuration;
    private readonly ISerialPortAdapter _adapter;

    public SerialLinkTransport(SerialLinkConfiguration configuration, ISerialPortAdapter adapter)
    {
        Validate(configuration);
        _configuration = configuration;
        _adapter = adapter;
        _adapter.BytesReceived += OnAdapterBytesReceived;
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration => _configuration;

    public bool IsConnected => _adapter.IsOpen;

    public bool CanSend => IsConnected;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_adapter.IsOpen)
        {
            return;
        }

        await _adapter.OpenAsync(_configuration, cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (!_adapter.IsOpen)
        {
            return;
        }

        await _adapter.CloseAsync().ConfigureAwait(false);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (!_adapter.IsOpen)
        {
            CommunicationError?.Invoke(this, "Serial link is not connected.");
            return;
        }

        var array = bytes.ToArray();
        await _adapter.WriteAsync(array, cancellationToken).ConfigureAwait(false);
        BytesSent?.Invoke(this, new BytesReceivedEventArgs(this, array));
    }

    public async ValueTask DisposeAsync()
    {
        _adapter.BytesReceived -= OnAdapterBytesReceived;
        await DisconnectAsync().ConfigureAwait(false);
        await _adapter.DisposeAsync().ConfigureAwait(false);
    }

    private void OnAdapterBytesReceived(object? sender, byte[] bytes)
    {
        if (!_adapter.IsOpen)
        {
            return;
        }

        BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, bytes));
    }

    private static void Validate(SerialLinkConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.PortName))
        {
            throw new ArgumentException("Serial port name is required.", nameof(configuration));
        }

        if (configuration.BaudRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Serial baud rate must be greater than zero.");
        }

        if (configuration.DataBits is < 5 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(configuration), "Serial data bits must be between 5 and 8.");
        }

        if (!IsKnownParity(configuration.Parity))
        {
            throw new ArgumentException($"Unsupported serial parity '{configuration.Parity}'.", nameof(configuration));
        }

        if (!IsKnownStopBits(configuration.StopBits))
        {
            throw new ArgumentException($"Unsupported serial stop bits '{configuration.StopBits}'.", nameof(configuration));
        }
    }

    private static bool IsKnownParity(string parity)
    {
        return parity is "None" or "Odd" or "Even" or "Mark" or "Space";
    }

    private static bool IsKnownStopBits(string stopBits)
    {
        return stopBits is "One" or "OnePointFive" or "Two";
    }
}
