using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using VGC.Comms;

namespace VGC.ViewModels;

public sealed class LinkConfigurationItemViewModel : ViewModelBase
{
    private string _name;
    private LinkType _type;
    private int? _localPort;
    private string? _targetHost;
    private int? _targetPort;
    private string? _host;
    private int? _port;
    private bool _isServer;
    private string? _serialPortName;
    private int? _baudRate;
    private string? _filePath;
    private bool _isPreferred;

    public LinkConfigurationItemViewModel(PersistedLinkConfiguration configuration, bool isPreferred = false)
    {
        _name = configuration.Name;
        _type = configuration.Type;
        _localPort = configuration.LocalPort;
        _targetHost = configuration.TargetHost;
        _targetPort = configuration.TargetPort;
        _host = configuration.Host;
        _port = configuration.Port;
        _isServer = configuration.IsServer;
        _serialPortName = configuration.SerialPortName;
        _baudRate = configuration.BaudRate;
        _filePath = configuration.FilePath;
        _isPreferred = isPreferred;
    }

    public string Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    public LinkType Type
    {
        get => _type;
        set
        {
            if (_type == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _type, value);
            this.RaisePropertyChanged(nameof(IsUdp));
            this.RaisePropertyChanged(nameof(IsTcp));
            this.RaisePropertyChanged(nameof(IsSerial));
            this.RaisePropertyChanged(nameof(TypeSummaryText));
            this.RaisePropertyChanged(nameof(LinkRowSubtitle));
        }
    }

    public bool IsUdp => Type == LinkType.Udp;

    public bool IsTcp => Type == LinkType.Tcp;

    public bool IsSerial => Type == LinkType.Serial;

    public int? LocalPort
    {
        get => _localPort;
        set => this.RaiseAndSetIfChanged(ref _localPort, value);
    }

    public string? TargetHost
    {
        get => _targetHost;
        set => this.RaiseAndSetIfChanged(ref _targetHost, value);
    }

    public int? TargetPort
    {
        get => _targetPort;
        set => this.RaiseAndSetIfChanged(ref _targetPort, value);
    }

    public string? Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int? Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public bool IsServer
    {
        get => _isServer;
        set => this.RaiseAndSetIfChanged(ref _isServer, value);
    }

    public string? SerialPortName
    {
        get => _serialPortName;
        set => this.RaiseAndSetIfChanged(ref _serialPortName, value);
    }

    public int? BaudRate
    {
        get => _baudRate;
        set => this.RaiseAndSetIfChanged(ref _baudRate, value);
    }

    public string? FilePath
    {
        get => _filePath;
        set => this.RaiseAndSetIfChanged(ref _filePath, value);
    }

    public bool IsPreferred
    {
        get => _isPreferred;
        set
        {
            if (this.RaiseAndSetIfChanged(ref _isPreferred, value))
            {
                this.RaisePropertyChanged(nameof(IsConnected));
                this.RaisePropertyChanged(nameof(ConnectActionText));
                this.RaisePropertyChanged(nameof(ConnectionStateText));
                this.RaisePropertyChanged(nameof(PreferredStateText));
                this.RaisePropertyChanged(nameof(TypeSummaryText));
                this.RaisePropertyChanged(nameof(LinkRowSubtitle));
            }
        }
    }

    public bool IsConnected => IsPreferred;

    public string ConnectActionText => IsConnected ? "Disconnect" : "Connect";

    public string ConnectionStateText => IsConnected ? "Connected" : "Saved";

    public string PreferredStateText => IsPreferred ? "Preferred" : string.Empty;

    public string TypeSummaryText => $"{Type} · {ConnectionStateText}";

    public string LinkRowSubtitle => TypeSummaryText;

    public PersistedLinkConfiguration ToConfiguration()
    {
        return new PersistedLinkConfiguration(
            string.IsNullOrWhiteSpace(Name) ? $"{Type} Link" : Name.Trim(),
            Type,
            LocalPort,
            TargetHost,
            TargetPort,
            SerialPortName: SerialPortName,
            BaudRate: BaudRate,
            Host: Host,
            Port: Port,
            IsServer: IsServer,
            FilePath: FilePath);
    }
}

public sealed class LinkConfigurationViewModel : ViewModelBase
{
    private readonly ILinkConfigurationStore _store;
    private LinkConfigurationItemViewModel? _selectedLink;

    public LinkConfigurationViewModel(ILinkConfigurationStore store)
    {
        _store = store;
        Links = new ObservableCollection<LinkConfigurationItemViewModel>();
        AddCommand = ReactiveCommand.Create(() =>
        {
            Add(new PersistedLinkConfiguration($"Link {Links.Count + 1}", LinkType.Udp, 14550 + Links.Count));
            return Unit.Default;
        });
        DeleteSelectedCommand = ReactiveCommand.Create(() =>
        {
            DeleteSelected();
            return Unit.Default;
        });
        SelectPreferredCommand = ReactiveCommand.Create(() =>
        {
            SelectPreferred(SelectedLink);
            return Unit.Default;
        });
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
    }

    public ObservableCollection<LinkConfigurationItemViewModel> Links { get; }

    public LinkConfigurationItemViewModel? SelectedLink
    {
        get => _selectedLink;
        set => this.RaiseAndSetIfChanged(ref _selectedLink, value);
    }

    public ReactiveCommand<Unit, Unit> AddCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteSelectedCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectPreferredCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        Links.Clear();
        foreach (var link in _store.Current.Links)
        {
            Links.Add(new LinkConfigurationItemViewModel(
                link,
                string.Equals(link.Name, _store.Current.PreferredActiveLinkName, StringComparison.Ordinal)));
        }

        SelectedLink = Links.FirstOrDefault(static link => link.IsPreferred) ?? Links.FirstOrDefault();
    }

    public LinkConfigurationItemViewModel Add(PersistedLinkConfiguration configuration)
    {
        var item = new LinkConfigurationItemViewModel(configuration, Links.Count == 0);
        Links.Add(item);
        SelectedLink = item;
        if (item.IsPreferred)
        {
            SelectPreferred(item);
        }

        return item;
    }

    public void DeleteSelected()
    {
        if (SelectedLink is null)
        {
            return;
        }

        var index = Links.IndexOf(SelectedLink);
        var wasPreferred = SelectedLink.IsPreferred;
        Links.Remove(SelectedLink);
        SelectedLink = Links.Count == 0 ? null : Links[Math.Min(index, Links.Count - 1)];
        if (wasPreferred && SelectedLink is not null)
        {
            SelectPreferred(SelectedLink);
        }
    }

    public void SelectPreferred(LinkConfigurationItemViewModel? link)
    {
        if (link is null || !Links.Contains(link))
        {
            return;
        }

        foreach (var item in Links)
        {
            item.IsPreferred = ReferenceEquals(item, link);
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var state = new LinkConfigurationState
        {
            PreferredActiveLinkName = Links.FirstOrDefault(static link => link.IsPreferred)?.Name,
            Links = Links.Select(static link => link.ToConfiguration()).ToList()
        };

        return _store.SaveAsync(state, cancellationToken);
    }
}
