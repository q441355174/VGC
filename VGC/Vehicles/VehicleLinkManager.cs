using VGC.Comms;

namespace VGC.Vehicles;

public sealed class VehicleLinkManager
{
    private ILinkTransport? _activeLink;
    private long _bytesReceived;
    private long _bytesSent;
    private DateTimeOffset? _lastReceivedAt;
    private DateTimeOffset? _lastSentAt;
    private int _linkErrors;

    public ILinkTransport? ActiveLink
    {
        get => _activeLink;
        set
        {
            if (_activeLink is { } oldLink)
            {
                oldLink.BytesReceived -= OnBytesReceived;
                oldLink.BytesSent -= OnBytesSent;
                oldLink.CommunicationError -= OnCommunicationError;
            }

            _activeLink = value;
            _bytesReceived = 0;
            _bytesSent = 0;
            _lastReceivedAt = null;
            _lastSentAt = null;
            _linkErrors = 0;

            if (_activeLink is { } newLink)
            {
                newLink.BytesReceived += OnBytesReceived;
                newLink.BytesSent += OnBytesSent;
                newLink.CommunicationError += OnCommunicationError;
            }
        }
    }

    public long BytesReceived => _bytesReceived;

    public long BytesSent => _bytesSent;

    public int LinkErrors => _linkErrors;

    public bool HasActiveLink => _activeLink is { IsConnected: true };

    public bool CanSend => _activeLink is { CanSend: true };

    public string LinkStatus => _activeLink switch
    {
        null => "No link",
        { IsConnected: true, CanSend: true } => $"Connected ({_activeLink.Configuration.Name})",
        { IsConnected: true } => "Connected (read-only)",
        _ => "Disconnected"
    };

    public double LinkQualityPercent => _activeLink switch
    {
        { IsConnected: true } when _linkErrors == 0 => 100,
        { IsConnected: true } when _linkErrors < 5 => 80,
        { IsConnected: true } => 50,
        _ => 0
    };

    public event EventHandler? LinkChanged;

    private void OnBytesReceived(object? sender, BytesReceivedEventArgs args)
    {
        _bytesReceived += args.Bytes.Length;
        _lastReceivedAt = DateTimeOffset.UtcNow;
    }

    private void OnBytesSent(object? sender, BytesReceivedEventArgs args)
    {
        _bytesSent += args.Bytes.Length;
        _lastSentAt = DateTimeOffset.UtcNow;
    }

    private void OnCommunicationError(object? sender, string error)
    {
        _linkErrors++;
    }

    public void Detach()
    {
        ActiveLink = null;
        LinkChanged?.Invoke(this, EventArgs.Empty);
    }
}
