using System.Collections.ObjectModel;
using VGC.Core.Logging;

namespace VGC.Comms;

public sealed class LinkManager : IAsyncDisposable
{
    private readonly ObservableCollection<ILinkTransport> _links = new();
    private readonly Dictionary<ILinkTransport, LinkDiagnosticsAccumulator> _diagnostics = new();
    private readonly IAppLogger _logger;
    private readonly ILinkConfigurationStore? _configurationStore;
    private string? _preferredActiveLinkName;

    public LinkManager(IAppLogger logger, ILinkConfigurationStore? configurationStore = null)
    {
        _logger = logger;
        _configurationStore = configurationStore;
        Links = new ReadOnlyObservableCollection<ILinkTransport>(_links);
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler? LinksChanged;

    public event EventHandler? LinkDiagnosticsChanged;

    public ReadOnlyObservableCollection<ILinkTransport> Links { get; }

    public string? PreferredActiveLinkName => _preferredActiveLinkName;

    public ILinkTransport? ActiveLink => SelectActiveLink();

    public async Task<ILinkTransport> CreateConnectedUdpLinkAsync(int localPort, CancellationToken cancellationToken = default)
    {
        return await CreateConnectedUdpLinkAsync(new UdpLinkConfiguration("UDP Link", localPort), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ILinkTransport> CreateConnectedUdpLinkAsync(UdpLinkConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var link = new UdpLinkTransport(configuration);
        Attach(link);
        await link.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _links.Add(link);
        LinksChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"UDP link listening on port {configuration.LocalPort}.");
        return link;
    }

    public async Task<MockLinkTransport> CreateConnectedMockLinkAsync(CancellationToken cancellationToken = default)
    {
        return await CreateConnectedMockLinkAsync("Mock Link", cancellationToken).ConfigureAwait(false);
    }

    public async Task<MockLinkTransport> CreateConnectedMockLinkAsync(string name, CancellationToken cancellationToken = default)
    {
        var link = new MockLinkTransport(name);
        Attach(link);
        await link.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _links.Add(link);
        LinksChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info("Mock link connected.");
        return link;
    }

    public async Task<ILinkTransport> CreateConnectedTcpClientLinkAsync(TcpLinkConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var link = new TcpClientLinkTransport(configuration);
        Attach(link);
        await link.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _links.Add(link);
        LinksChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"TCP client link connected to {configuration.Host}:{configuration.Port}.");
        return link;
    }

    public async Task<ILinkTransport> CreateConnectedTcpServerLinkAsync(TcpLinkConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var link = new TcpServerLinkTransport(configuration);
        Attach(link);
        await link.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _links.Add(link);
        LinksChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"TCP server link listening on {configuration.Host}:{link.BoundPort}.");
        return link;
    }

    public async Task<ILinkTransport> CreateConnectedSerialLinkAsync(
        SerialLinkConfiguration configuration,
        ISerialPortAdapter adapter,
        CancellationToken cancellationToken = default)
    {
        var link = new SerialLinkTransport(configuration, adapter);
        Attach(link);
        await link.ConnectAsync(cancellationToken).ConfigureAwait(false);
        _links.Add(link);
        LinksChanged?.Invoke(this, EventArgs.Empty);
        _logger.Info($"Serial link connected on {configuration.PortName} at {configuration.BaudRate} baud.");
        return link;
    }

    public ILinkTransport? SelectActiveLink()
    {
        var sendCapableLinks = _links.Where(static link => link is { IsConnected: true, CanSend: true }).ToArray();
        if (sendCapableLinks.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_preferredActiveLinkName))
        {
            var preferred = sendCapableLinks.FirstOrDefault(link => string.Equals(link.Configuration.Name, _preferredActiveLinkName, StringComparison.Ordinal));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return sendCapableLinks[0];
    }

    public void SetPreferredActiveLink(string? name)
    {
        _preferredActiveLinkName = string.IsNullOrWhiteSpace(name) ? null : name;
        LinksChanged?.Invoke(this, EventArgs.Empty);
    }

    public LinkConfigurationState CaptureConfigurationState()
    {
        return new LinkConfigurationState
        {
            PreferredActiveLinkName = ActiveLink?.Configuration.Name ?? _preferredActiveLinkName,
            Links = _links
                .Select(static link => PersistedLinkConfiguration.FromRuntime(link.Configuration))
                .ToList()
        };
    }

    public IReadOnlyList<LinkDiagnosticsSnapshot> GetDiagnostics()
    {
        return _links
            .Select(link => _diagnostics.TryGetValue(link, out var diagnostics)
                ? diagnostics.ToSnapshot(link)
                : new LinkDiagnosticsAccumulator().ToSnapshot(link))
            .ToList();
    }

    public async Task LoadConfigurationStateAsync(CancellationToken cancellationToken = default)
    {
        if (_configurationStore is null)
        {
            return;
        }

        await _configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _preferredActiveLinkName = _configurationStore.Current.PreferredActiveLinkName;
    }

    public async Task SaveConfigurationStateAsync(CancellationToken cancellationToken = default)
    {
        if (_configurationStore is null)
        {
            return;
        }

        await _configurationStore.SaveAsync(CaptureConfigurationState(), cancellationToken).ConfigureAwait(false);
    }

    public async Task DisconnectAllAsync()
    {
        foreach (var link in _links.ToArray())
        {
            await link.DisconnectAsync().ConfigureAwait(false);
            await link.DisposeAsync().ConfigureAwait(false);
        }

        _links.Clear();
        _diagnostics.Clear();
        LinkDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        LinksChanged?.Invoke(this, EventArgs.Empty);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAllAsync().ConfigureAwait(false);
    }

    private void Attach(ILinkTransport link)
    {
        _diagnostics[link] = new LinkDiagnosticsAccumulator();
        link.BytesReceived += (_, args) =>
        {
            TrackReceived(args.Link, args.Bytes.Length);
            BytesReceived?.Invoke(this, args);
        };
        link.BytesSent += (_, args) => TrackSent(args.Link, args.Bytes.Length);
        link.CommunicationError += (_, error) =>
        {
            TrackError(link, error);
            _logger.Warning(error);
        };
        LinkDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void TrackReceived(ILinkTransport link, int byteCount)
    {
        if (_diagnostics.TryGetValue(link, out var diagnostics))
        {
            diagnostics.AddReceived(byteCount);
            LinkDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TrackSent(ILinkTransport link, int byteCount)
    {
        if (_diagnostics.TryGetValue(link, out var diagnostics))
        {
            diagnostics.AddSent(byteCount);
            LinkDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TrackError(ILinkTransport link, string error)
    {
        if (_diagnostics.TryGetValue(link, out var diagnostics))
        {
            diagnostics.SetError(error);
            LinkDiagnosticsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<AutoConnectResult> AutoConnectSavedLinksAsync(CancellationToken cancellationToken = default)
    {
        if (_configurationStore is null)
        {
            return new AutoConnectResult(null, []);
        }

        await _configurationStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var state = _configurationStore.Current;
        var results = new List<AutoConnectLinkResult>();

        foreach (var config in state.Links)
        {
            if (_links.Any(l => l.Configuration.Name == config.Name))
            {
                results.Add(new AutoConnectLinkResult(
                    config,
                    AutoConnectOutcome.AlreadyConnected,
                    "Link already connected.",
                    config.Name));
                continue;
            }

            try
            {
                var connected = await TryCreateAutoConnectedLinkAsync(config, cancellationToken).ConfigureAwait(false);
                results.Add(connected);
            }
            catch (Exception ex)
            {
                results.Add(new AutoConnectLinkResult(config, AutoConnectOutcome.Failed, ex.Message));
                _logger.Warning($"Auto-connect to {config.Name} failed: {ex.Message}");
            }
        }

        if (state.PreferredActiveLinkName is { } preferred)
        {
            _preferredActiveLinkName = preferred;
        }

        return new AutoConnectResult(_preferredActiveLinkName, results);
    }

    private async Task<AutoConnectLinkResult> TryCreateAutoConnectedLinkAsync(
        PersistedLinkConfiguration config,
        CancellationToken cancellationToken)
    {
        switch (config.Type)
        {
            case LinkType.Udp:
            {
                if (config.LocalPort is not { } port)
                {
                    return new AutoConnectLinkResult(config, AutoConnectOutcome.Failed, "UDP local port is required.");
                }

                await CreateConnectedUdpLinkAsync(
                    new UdpLinkConfiguration(config.Name, port, config.TargetHost, config.TargetPort),
                    cancellationToken).ConfigureAwait(false);
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Connected, "UDP link connected.", config.Name);
            }

            case LinkType.Mock:
                await CreateConnectedMockLinkAsync(config.Name, cancellationToken).ConfigureAwait(false);
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Connected, "Mock link connected.", config.Name);

            case LinkType.Tcp when config.IsServer:
                await CreateConnectedTcpServerLinkAsync(
                    new TcpLinkConfiguration(config.Name, config.Host ?? "127.0.0.1", config.Port ?? 0, true),
                    cancellationToken).ConfigureAwait(false);
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Connected, "TCP server link connected.", config.Name);

            case LinkType.Tcp:
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Unsupported, "TCP client auto-connect requires an explicit operator action.");

            case LinkType.Serial:
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Unsupported, "Serial auto-connect requires a platform serial adapter.");

            case LinkType.LogReplay:
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Unsupported, "Log replay auto-connect requires a replay source.");

            default:
                return new AutoConnectLinkResult(config, AutoConnectOutcome.Unsupported, $"{config.Type} auto-connect is not supported.");
        }
    }
}
