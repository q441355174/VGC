namespace VGC.Comms;

public sealed class MavlinkForwardingService : IDisposable
{
    private readonly LinkManager _linkManager;
    private readonly HashSet<string> _destinationNames = new(StringComparer.Ordinal);
    private int _isForwarding;
    private bool _isEnabled;

    public MavlinkForwardingService(LinkManager linkManager)
    {
        _linkManager = linkManager;
        _linkManager.BytesReceived += OnBytesReceived;
    }

    public bool IsEnabled => _isEnabled;

    public long ForwardedPackets { get; private set; }

    public long DroppedPackets { get; private set; }

    public long ForwardedBytes { get; private set; }

    public IReadOnlyCollection<string> DestinationNames => _destinationNames;

    public void Configure(IEnumerable<string> destinationLinkNames, bool isEnabled)
    {
        _destinationNames.Clear();
        foreach (var name in destinationLinkNames.Where(static name => !string.IsNullOrWhiteSpace(name)))
        {
            _destinationNames.Add(name);
        }

        _isEnabled = isEnabled;
    }

    public void Dispose()
    {
        _linkManager.BytesReceived -= OnBytesReceived;
    }

    private async void OnBytesReceived(object? sender, BytesReceivedEventArgs args)
    {
        if (!_isEnabled || _destinationNames.Count == 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref _isForwarding, 1) == 1)
        {
            DroppedPackets++;
            return;
        }

        try
        {
            foreach (var destination in _linkManager.Links)
            {
                if (ReferenceEquals(destination, args.Link)
                    || !destination.IsConnected
                    || !destination.CanSend
                    || !_destinationNames.Contains(destination.Configuration.Name))
                {
                    continue;
                }

                await destination.WriteAsync(args.Bytes).ConfigureAwait(false);
                ForwardedPackets++;
                ForwardedBytes += args.Bytes.Length;
            }
        }
        catch
        {
            DroppedPackets++;
        }
        finally
        {
            Volatile.Write(ref _isForwarding, 0);
        }
    }
}
