namespace VGC.Comms;

public sealed record SerialPortInfo(
    string PortName,
    string Description,
    int VendorId,
    int ProductId);

public interface ISerialPortEnumerator
{
    Task<IReadOnlyList<SerialPortInfo>> EnumerateAsync(CancellationToken cancellationToken = default);
}

public sealed class DesktopSerialAutoDetect : IAsyncDisposable
{
    private readonly ISerialPortEnumerator _enumerator;
    private readonly TimeSpan _pollInterval;
    private readonly object _syncRoot = new();
    private Dictionary<string, SerialPortInfo> _knownPorts = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _pollCancellation;
    private Task? _pollTask;

    public DesktopSerialAutoDetect(ISerialPortEnumerator enumerator, TimeSpan? pollInterval = null)
    {
        _enumerator = enumerator;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
    }

    public event EventHandler<SerialPortInfo>? DeviceAdded;

    public event EventHandler<SerialPortInfo>? DeviceRemoved;

    public bool IsRunning { get; private set; }

    public IReadOnlyList<SerialPortInfo> KnownPorts
    {
        get
        {
            lock (_syncRoot)
            {
                return _knownPorts.Values.ToArray();
            }
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (IsRunning)
            {
                return;
            }

            _pollCancellation = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoopAsync(_pollCancellation.Token), CancellationToken.None);
            IsRunning = true;
        }
    }

    public async Task StopAsync()
    {
        Task? pollTask;
        lock (_syncRoot)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            _pollCancellation?.Cancel();
            pollTask = _pollTask;
        }

        if (pollTask is not null)
        {
            try
            {
                await pollTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _pollCancellation?.Dispose();
        _pollCancellation = null;
        _pollTask = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var current = await _enumerator.EnumerateAsync(cancellationToken).ConfigureAwait(false);
                ProcessChanges(current);
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Enumeration failures are transient; continue polling.
            }
        }
    }

    private void ProcessChanges(IReadOnlyList<SerialPortInfo> current)
    {
        var currentByName = new Dictionary<string, SerialPortInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var port in current)
        {
            currentByName[port.PortName] = port;
        }

        List<SerialPortInfo> added;
        List<SerialPortInfo> removed;

        lock (_syncRoot)
        {
            added = currentByName
                .Where(kvp => !_knownPorts.ContainsKey(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            removed = _knownPorts
                .Where(kvp => !currentByName.ContainsKey(kvp.Key))
                .Select(kvp => kvp.Value)
                .ToList();

            _knownPorts = currentByName;
        }

        foreach (var port in removed)
        {
            DeviceRemoved?.Invoke(this, port);
        }

        foreach (var port in added)
        {
            DeviceAdded?.Invoke(this, port);
        }
    }
}
