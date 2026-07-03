using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using VGC.Comms;
using VGC.Core.Settings;
using VGC.Facts;
using VGC.Payload;

namespace VGC.ViewModels;

public sealed class SettingsFactViewModel : ViewModelBase
{
    private readonly Fact _fact;
    private string? _validationError;

    public SettingsFactViewModel(Fact fact)
    {
        _fact = fact;
    }

    public string Key => _fact.Name;

    public string Label => _fact.MetaData.ShortDescription ?? _fact.Name;

    public string? Units => _fact.MetaData.Units;

    public FactValueType ValueType => _fact.MetaData.ValueType;

    public object? Value
    {
        get => _fact.RawValue;
        set
        {
            var validation = _fact.Validate(value);
            if (validation.IsValid)
            {
                _fact.SetRawValue(value);
                this.RaisePropertyChanged();
                this.RaisePropertyChanged(nameof(DisplayValue));
                ValidationError = null;
            }
            else
            {
                ValidationError = validation.Error;
            }
        }
    }

    public string? ValidationError
    {
        get => _validationError;
        private set => this.RaiseAndSetIfChanged(ref _validationError, value);
    }

    public string DisplayValue => _fact.DisplayValue;
}

public sealed class SettingsGroupViewModel
{
    public SettingsGroupViewModel(SettingsGroup group)
    {
        Name = group.Name;
        Facts = new ObservableCollection<SettingsFactViewModel>(
            group.Facts.Values.Select(static fact => new SettingsFactViewModel(fact)));
    }

    public string Name { get; }

    public ObservableCollection<SettingsFactViewModel> Facts { get; }
}

public enum SettingsPageKind
{
    General,
    CommLinks
}

public sealed record SettingsSectionNode(string Name, int Index);

public sealed record CommLinkListRow(string Name, string Type, bool IsConnected);

public sealed record CommLinkDisplayRow(
    LinkConfigurationItemViewModel Link,
    string Name,
    string Subtitle,
    bool IsConnected,
    string ConnectActionText);

public sealed record CommLinkActionRow(string Name, string Type, bool IsConnected, string ConnectActionText);

public sealed record SettingsPagePresenter(SettingsPageKind Kind, object Content);

public sealed class SettingsPageNode : ViewModelBase
{
    private bool _isExpanded;
    private bool _isSelected;

    public SettingsPageNode(SettingsPageKind kind, string name, IReadOnlyList<string> sections)
    {
        Kind = kind;
        Name = name;
        Sections = sections.Select((section, index) => new SettingsSectionNode(section, index)).ToArray();
    }

    public SettingsPageKind Kind { get; }

    public string Name { get; }

    public IReadOnlyList<SettingsSectionNode> Sections { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}

public interface ISettingsSectionFilterHost
{
    int SectionFilter { get; set; }
}

public sealed class SettingsPageHostState : ViewModelBase, ISettingsSectionFilterHost
{
    private int _sectionFilter = -1;

    public int SectionFilter
    {
        get => _sectionFilter;
        set => this.RaiseAndSetIfChanged(ref _sectionFilter, value);
    }
}

public abstract record SettingsPageContent(SettingsViewModel Owner);

public sealed record GeneralSettingsPageContent(SettingsViewModel Owner) : SettingsPageContent(Owner);

public sealed record CommLinksSettingsPageContent(SettingsViewModel Owner) : SettingsPageContent(Owner);

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsManager _settingsManager;
    private readonly IAppSettingsStore _store;
    private readonly LinkConfigurationViewModel _commLinks;
    private readonly LinkManager? _linkManager;
    private readonly Func<ISerialPortAdapter> _serialPortAdapterFactory;
    private readonly ISerialPortEnumerator _serialPortEnumerator;
    private readonly VideoSettingsRuntime _videoSettings = new();
    private readonly SettingsPageHostState _pageHostState = new();
    private string _ntripStatusText = "NTRIP: not configured";
    private string _bluetoothStatusText = "Bluetooth: idle";
    private string _commLinkStatusText = "No active link action.";
    private string _searchQuery = string.Empty;
    private IReadOnlyList<string> _availableSerialPorts = [];
    private SettingsPageKind _selectedPage = SettingsPageKind.General;
    private int _selectedSectionIndex = -1;
    private IReadOnlyList<SettingsPageNode> _navigationPages = [];
    private bool _showCommLinkEditor;
    private bool _showDeleteCommLinkDialog;
    private LinkConfigurationItemViewModel? _editingCommLink;

    public SettingsViewModel(
        SettingsManager settingsManager,
        IAppSettingsStore store,
        ILinkConfigurationStore? linkConfigurationStore = null,
        LinkManager? linkManager = null,
        Func<ISerialPortAdapter>? serialPortAdapterFactory = null,
        ISerialPortEnumerator? serialPortEnumerator = null)
    {
        _settingsManager = settingsManager;
        _store = store;
        _commLinks = new LinkConfigurationViewModel(linkConfigurationStore ?? new AppSettingsLinkConfigurationStore(store));
        _linkManager = linkManager;
        _serialPortAdapterFactory = serialPortAdapterFactory ?? (() => new DesktopSerialPortAdapter());
        _serialPortEnumerator = serialPortEnumerator ?? new DesktopSerialPortEnumerator();
        Groups = [];
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync);
        SelectSettingsPageCommand = ReactiveCommand.Create<SettingsPageNode>(node => SelectPage(node.Kind));
        SelectSettingsSectionCommand = ReactiveCommand.Create<SettingsSectionNode>(section => SelectPage(SelectedPage, section.Index));
        ShowGeneralPageCommand = ReactiveCommand.Create(() => SelectPage(SettingsPageKind.General));
        ShowCommLinksPageCommand = ReactiveCommand.Create(() => SelectPage(SettingsPageKind.CommLinks));
        AddCommLinkCommand = ReactiveCommand.Create(AddCommLink);
        DeleteSelectedCommLinkCommand = ReactiveCommand.Create(ShowDeleteSelectedCommLinkDialog);
        ConfirmDeleteCommLinkCommand = ReactiveCommand.Create(ConfirmDeleteSelectedCommLink);
        CancelDeleteCommLinkCommand = ReactiveCommand.Create(CancelDeleteSelectedCommLink);
        EditSelectedCommLinkCommand = ReactiveCommand.Create(OpenSelectedCommLinkEditor);
        SaveEditedCommLinkCommand = ReactiveCommand.CreateFromTask(SaveEditedCommLinkAsync);
        CancelEditedCommLinkCommand = ReactiveCommand.Create(CancelEditedCommLink);
        SaveCommLinksCommand = ReactiveCommand.CreateFromTask(_commLinks.SaveAsync);
        ConnectSelectedCommLinkCommand = ReactiveCommand.CreateFromTask(ConnectSelectedCommLinkAsync);
        DisconnectSelectedCommLinkCommand = ReactiveCommand.CreateFromTask(DisconnectSelectedCommLinkAsync);
        RefreshSerialPortsCommand = ReactiveCommand.CreateFromTask(RefreshSerialPortsAsync);

        if (_linkManager is not null)
        {
            _linkManager.LinksChanged += (_, _) => RaiseCommLinkProjection();
        }

        _commLinks.PropertyChanged += (_, _) => RaiseCommLinkProjection();
        _commLinks.Links.CollectionChanged += (_, _) => RaiseCommLinkProjection();
        BuildNavigationPages();
    }

    public VideoSettingsRuntime VideoSettings => _videoSettings;

    public ObservableCollection<SettingsGroupViewModel> Groups { get; }

    public ObservableCollection<LinkConfigurationItemViewModel> CommLinks => _commLinks.Links;

    public IReadOnlyList<CommLinkListRow> CommLinkRows => _commLinks.Links.Select(link => new CommLinkListRow(
        link.Name,
        link.Type.ToString(),
        _linkManager?.Links.Any(item => string.Equals(item.Configuration.Name, link.Name, StringComparison.Ordinal) && item.IsConnected) == true)).ToArray();

    public IReadOnlyList<CommLinkDisplayRow> CommLinkDisplayRows => _commLinks.Links.Select(link =>
    {
        var isConnected = _linkManager?.Links.Any(item => string.Equals(item.Configuration.Name, link.Name, StringComparison.Ordinal) && item.IsConnected) == true;
        return new CommLinkDisplayRow(
            link,
            link.Name,
            $"{link.Type} · {(isConnected ? "Connected" : "Saved")}",
            isConnected,
            isConnected ? "Disconnect" : "Connect");
    }).ToArray();

    public CommLinkDisplayRow? SelectedCommLinkDisplayRow => SelectedCommLink is null
        ? null
        : CommLinkDisplayRows.FirstOrDefault(row => ReferenceEquals(row.Link, SelectedCommLink));

    public string SelectedCommLinkActionText => SelectedCommLinkIsConnected ? "Disconnect" : "Connect";

    public IReadOnlyList<SettingsPageNode> NavigationPages => FilterNavigationPages();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (string.Equals(_searchQuery, value, StringComparison.Ordinal))
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _searchQuery, value);
            this.RaisePropertyChanged(nameof(NavigationPages));
        }
    }

    public SettingsPageKind SelectedPage
    {
        get => _selectedPage;
        private set => this.RaiseAndSetIfChanged(ref _selectedPage, value);
    }

    public int SelectedSectionIndex
    {
        get => _selectedSectionIndex;
        private set => this.RaiseAndSetIfChanged(ref _selectedSectionIndex, value);
    }

    public bool IsGeneralSection => SelectedPage == SettingsPageKind.General;

    public bool IsCommLinksSection => SelectedPage == SettingsPageKind.CommLinks;

    public SettingsPagePresenter CurrentPagePresenter => SelectedPage switch
    {
        SettingsPageKind.CommLinks => new SettingsPagePresenter(SettingsPageKind.CommLinks, new CommLinksSettingsPageContent(this)),
        _ => new SettingsPagePresenter(SettingsPageKind.General, new GeneralSettingsPageContent(this))
    };

    public int CurrentSectionFilter => _pageHostState.SectionFilter;

    public LinkConfigurationItemViewModel? SelectedCommLink
    {
        get => _commLinks.SelectedLink;
        set
        {
            _commLinks.SelectedLink = value;
            RaiseCommLinkProjection();
        }
    }

    public IReadOnlyList<string> AvailableSerialPorts
    {
        get => _availableSerialPorts;
        private set => this.RaiseAndSetIfChanged(ref _availableSerialPorts, value);
    }

    public string CommLinkStatusText
    {
        get => _commLinkStatusText;
        private set => this.RaiseAndSetIfChanged(ref _commLinkStatusText, value);
    }

    public bool SelectedCommLinkIsConnected => SelectedCommLink is not null
        && _linkManager?.Links.Any(link => string.Equals(link.Configuration.Name, SelectedCommLink.Name, StringComparison.Ordinal) && link.IsConnected) == true;

    public string SelectedCommLinkSummary => SelectedCommLink is null
        ? "Select a saved link to inspect or edit it."
        : BuildSelectedCommLinkSummary(SelectedCommLink);

    public bool ShowGeneralPage => SelectedPage == SettingsPageKind.General;

    public bool ShowCommLinksPage => SelectedPage == SettingsPageKind.CommLinks;

    public bool ShowCommLinkEditor
    {
        get => _showCommLinkEditor;
        private set => this.RaiseAndSetIfChanged(ref _showCommLinkEditor, value);
    }

    public bool ShowDeleteCommLinkDialog
    {
        get => _showDeleteCommLinkDialog;
        private set => this.RaiseAndSetIfChanged(ref _showDeleteCommLinkDialog, value);
    }

    public LinkConfigurationItemViewModel? EditingCommLink
    {
        get => _editingCommLink;
        private set => this.RaiseAndSetIfChanged(ref _editingCommLink, value);
    }

    public string DeleteCommLinkPrompt => SelectedCommLink is null
        ? "Select a link to delete."
        : $"Delete '{SelectedCommLink.Name}'?";

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    public ReactiveCommand<SettingsPageNode, Unit> SelectSettingsPageCommand { get; }

    public ReactiveCommand<SettingsSectionNode, Unit> SelectSettingsSectionCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowGeneralPageCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowCommLinksPageCommand { get; }

    public ReactiveCommand<Unit, Unit> AddCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteSelectedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> ConfirmDeleteCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelDeleteCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> EditSelectedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveEditedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelEditedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommLinksCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectSelectedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> DisconnectSelectedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshSerialPortsCommand { get; }

    public string NtripStatusText
    {
        get => _ntripStatusText;
        private set => this.RaiseAndSetIfChanged(ref _ntripStatusText, value);
    }

    public string BluetoothStatusText
    {
        get => _bluetoothStatusText;
        private set => this.RaiseAndSetIfChanged(ref _bluetoothStatusText, value);
    }

    public void SetNtripConfig(string host, int port, string mountPoint)
    {
        NtripStatusText = string.IsNullOrWhiteSpace(host)
            ? "NTRIP: not configured"
            : $"NTRIP: {host}:{port}/{mountPoint}";
    }

    public void StartBluetoothScan()
    {
        BluetoothStatusText = "Bluetooth: scanning...";
    }

    public void SelectPage(SettingsPageKind kind, int sectionIndex = -1)
    {
        SelectedPage = kind;
        SelectedSectionIndex = sectionIndex;
        _pageHostState.SectionFilter = sectionIndex;
        foreach (var node in _navigationPages)
        {
            node.IsSelected = node.Kind == kind;
            node.IsExpanded = node.Kind == kind && node.Sections.Count > 0;
        }

        this.RaisePropertyChanged(nameof(IsGeneralSection));
        this.RaisePropertyChanged(nameof(IsCommLinksSection));
        this.RaisePropertyChanged(nameof(CurrentSectionFilter));
        this.RaisePropertyChanged(nameof(CurrentPagePresenter));
        this.RaisePropertyChanged(nameof(NavigationPages));
    }

    private Unit AddCommLink()
    {
        _commLinks.Add(new PersistedLinkConfiguration($"Link {_commLinks.Links.Count + 1}", LinkType.Udp, 14550));
        RaiseCommLinkProjection();
        return Unit.Default;
    }

    private Unit ShowDeleteSelectedCommLinkDialog()
    {
        ShowDeleteCommLinkDialog = SelectedCommLink is not null;
        this.RaisePropertyChanged(nameof(DeleteCommLinkPrompt));
        return Unit.Default;
    }

    private Unit ConfirmDeleteSelectedCommLink()
    {
        _commLinks.DeleteSelected();
        ShowDeleteCommLinkDialog = false;
        RaiseCommLinkProjection();
        this.RaisePropertyChanged(nameof(DeleteCommLinkPrompt));
        return Unit.Default;
    }

    private Unit CancelDeleteSelectedCommLink()
    {
        ShowDeleteCommLinkDialog = false;
        return Unit.Default;
    }

    private Unit OpenSelectedCommLinkEditor()
    {
        if (SelectedCommLink is null)
        {
            return Unit.Default;
        }

        EditingCommLink = new LinkConfigurationItemViewModel(SelectedCommLink.ToConfiguration(), SelectedCommLink.IsPreferred);
        ShowCommLinkEditor = true;
        return Unit.Default;
    }

    private async Task<Unit> SaveEditedCommLinkAsync()
    {
        if (SelectedCommLink is null || EditingCommLink is null)
        {
            return Unit.Default;
        }

        SelectedCommLink.Name = EditingCommLink.Name;
        SelectedCommLink.Type = EditingCommLink.Type;
        SelectedCommLink.LocalPort = EditingCommLink.LocalPort;
        SelectedCommLink.TargetHost = EditingCommLink.TargetHost;
        SelectedCommLink.TargetPort = EditingCommLink.TargetPort;
        SelectedCommLink.Host = EditingCommLink.Host;
        SelectedCommLink.Port = EditingCommLink.Port;
        SelectedCommLink.IsServer = EditingCommLink.IsServer;
        SelectedCommLink.SerialPortName = EditingCommLink.SerialPortName;
        SelectedCommLink.BaudRate = EditingCommLink.BaudRate;
        await _commLinks.SaveAsync().ConfigureAwait(false);
        CommLinkStatusText = $"Saved {SelectedCommLink.Name}.";
        ShowCommLinkEditor = false;
        EditingCommLink = null;
        RaiseCommLinkProjection();
        return Unit.Default;
    }

    private Unit CancelEditedCommLink()
    {
        ShowCommLinkEditor = false;
        EditingCommLink = null;
        return Unit.Default;
    }

    private void RaiseCommLinkProjection()
    {
        this.RaisePropertyChanged(nameof(CommLinks));
        this.RaisePropertyChanged(nameof(CommLinkRows));
        this.RaisePropertyChanged(nameof(CommLinkDisplayRows));
        this.RaisePropertyChanged(nameof(SelectedCommLink));
        this.RaisePropertyChanged(nameof(SelectedCommLinkDisplayRow));
        this.RaisePropertyChanged(nameof(SelectedCommLinkIsConnected));
        this.RaisePropertyChanged(nameof(SelectedCommLinkActionText));
        this.RaisePropertyChanged(nameof(SelectedCommLinkSummary));
    }

    private static string BuildSelectedCommLinkSummary(LinkConfigurationItemViewModel link)
    {
        return link.Type switch
        {
            LinkType.Serial => $"Serial {link.SerialPortName} @ {link.BaudRate}",
            LinkType.Tcp => $"TCP {(string.IsNullOrWhiteSpace(link.Host) ? "127.0.0.1" : link.Host)}:{link.Port}{(link.IsServer ? " server" : string.Empty)}",
            LinkType.Udp => $"UDP local {link.LocalPort} target {link.TargetHost}:{link.TargetPort}",
            _ => link.Type.ToString()
        };
    }

    private void BuildNavigationPages()
    {
        _navigationPages =
        [
            new SettingsPageNode(SettingsPageKind.General, "General", ["General"]),
            new SettingsPageNode(SettingsPageKind.CommLinks, "Comm Links", ["Links"])
        ];
        this.RaisePropertyChanged(nameof(NavigationPages));
    }

    private IReadOnlyList<SettingsPageNode> FilterNavigationPages()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            return _navigationPages;
        }

        var query = SearchQuery.Trim();
        var matches = _navigationPages.Where(node => node.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || node.Sections.Any(section => section.Name.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        foreach (var node in _navigationPages)
        {
            node.IsExpanded = string.IsNullOrWhiteSpace(SearchQuery)
                ? node.IsSelected && node.Sections.Count > 0
                : matches.Contains(node);
        }
        return matches;
    }

    public async Task ConnectSelectedCommLinkAsync()
    {
        if (_linkManager is null)
        {
            CommLinkStatusText = "Link manager unavailable.";
            return;
        }

        var selected = SelectedCommLink;
        if (selected is null)
        {
            CommLinkStatusText = "Select a link first.";
            return;
        }

        await _commLinks.SaveAsync().ConfigureAwait(false);
        var runtime = selected.ToConfiguration().ToRuntimeConfiguration();
        switch (runtime)
        {
            case UdpLinkConfiguration udp:
                await _linkManager.CreateConnectedUdpLinkAsync(udp).ConfigureAwait(false);
                break;
            case TcpLinkConfiguration tcp when tcp.IsServer:
                await _linkManager.CreateConnectedTcpServerLinkAsync(tcp).ConfigureAwait(false);
                break;
            case TcpLinkConfiguration tcp:
                await _linkManager.CreateConnectedTcpClientLinkAsync(tcp).ConfigureAwait(false);
                break;
            case SerialLinkConfiguration serial:
                await _linkManager.CreateConnectedSerialLinkAsync(serial, _serialPortAdapterFactory()).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unsupported link type {selected.Type}.");
        }

        CommLinkStatusText = $"Connected {selected.Name}.";
        RaiseCommLinkProjection();
    }

    public async Task DisconnectSelectedCommLinkAsync()
    {
        if (_linkManager is null || SelectedCommLink is null)
        {
            return;
        }

        var link = _linkManager.Links.FirstOrDefault(item => string.Equals(item.Configuration.Name, SelectedCommLink.Name, StringComparison.Ordinal));
        if (link is null)
        {
            CommLinkStatusText = "Selected link is not active.";
            return;
        }

        await link.DisconnectAsync().ConfigureAwait(false);
        await link.DisposeAsync().ConfigureAwait(false);
        CommLinkStatusText = $"Disconnected {SelectedCommLink.Name}.";
        RaiseCommLinkProjection();
    }

    public async Task RefreshSerialPortsAsync()
    {
        var ports = await _serialPortEnumerator.EnumerateAsync().ConfigureAwait(false);
        AvailableSerialPorts = ports.Select(static port => port.PortName).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (SelectedCommLink is { IsSerial: true } selected && string.IsNullOrWhiteSpace(selected.SerialPortName) && AvailableSerialPorts.Count > 0)
        {
            selected.SerialPortName = AvailableSerialPorts[0];
        }

        CommLinkStatusText = AvailableSerialPorts.Count == 0 ? "No serial ports found." : $"Found {AvailableSerialPorts.Count} serial port(s).";
        RaiseCommLinkProjection();
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _settingsManager.LoadAsync(_store, cancellationToken).ConfigureAwait(false);
        await _commLinks.LoadAsync(cancellationToken).ConfigureAwait(false);
        Groups.Clear();
        foreach (var group in _settingsManager.Groups.Values)
        {
            Groups.Add(new SettingsGroupViewModel(group));
        }

        if (_commLinks.Links.Count == 0)
        {
            _commLinks.Add(new PersistedLinkConfiguration("UDP Link", LinkType.Udp, 14550));
        }

        SelectPage(SelectedPage, SelectedSectionIndex);
        BuildNavigationPages();
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        return _settingsManager.SaveAsync(_store, cancellationToken);
    }
}
