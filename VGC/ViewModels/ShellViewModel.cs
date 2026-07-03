using System.Collections.ObjectModel;
using System.Globalization;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using VGC.Comms;
using VGC.Core;
using VGC.Core.Application;
using VGC.Core.Logging;
using VGC.Core.Settings;
using VGC.Mission;
using VGC.Mavlink;
using VGC.Vehicles;
using VGC.Views.Controls;

namespace VGC.ViewModels;

internal enum ShellDrawerKind
{
    None,
    Vehicle,
    Link,
    Telemetry,
    Tools
}

internal enum ToolDrawerKind
{
    None,
    Analyze,
    Setup,
    Settings
}

public enum IndicatorDrawerKind
{
    None,
    MainStatus,
    FlightMode,
    Link,
    Gps,
    Battery,
    Rc,
    Telemetry,
    Arm,
    Messages
}

public sealed class ShellViewModel : ViewModelBase
{
    public const string AutoConnectTcpEnvironmentVariable = "VGC_AUTOCONNECT_TCP";
    public const string AndroidAutoConnectTcpEnvironmentVariable = "VGC_ANDROID_AUTOCONNECT_TCP";

    public static string? StartupAutoConnectTcpEndpoint { get; set; }

    private readonly IAppLifecycleService _lifecycle;
    private readonly IAppCloseCoordinator _closeCoordinator;
    private readonly IAppLogger _logger;
    private readonly LinkManager _linkManager;
    private readonly MavlinkProtocol _mavlinkProtocol;
    private readonly GcsHeartbeatService _gcsHeartbeatService;
    private readonly MultiVehicleManager _multiVehicleManager;
    private readonly Func<ISerialPortAdapter> _serialPortAdapterFactory;
    private readonly ISerialPortEnumerator _serialPortEnumerator;
    private readonly LinkConfigurationViewModel _commLinks;
    private bool _commLinksLoaded;
    private readonly PlanViewModel _planViewModel;
    private readonly ParameterViewModel _parameterViewModel;
    private readonly SetupViewModel _setupViewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly AnalyzeViewModel _analyzeViewModel;
    private readonly OverviewViewModel _overviewViewModel;
    private readonly FlyViewModel _flyViewModel;
    private readonly NavigationGuard _navigationGuard = new();
    private readonly ToastNotification _toastNotification = new();
    private readonly FirstRunPromptService _firstRunPromptService = new();
    private string _statusText = "Starting";
    private string _toastText = "";
    private bool _showToast;
    private bool _showFirstRunPrompt;
    private string _firstRunPromptTitle = string.Empty;
    private string _firstRunPromptDescription = string.Empty;
    private object _currentWorkspace;
    private object? _currentToolWorkspace;
    private ToolDrawerKind _activeToolDrawerKind;
    private IndicatorDrawerKind _activeIndicatorDrawerKind;
    private ShellDrawerKind _activeDrawerKind;
    private bool _isIndicatorDrawerExpanded;
    private double _indicatorDrawerAnchorX = ScreenMetrics.StandardMargin;
    private string _commLinkStatus = "Select or add a Comm Link.";
    private IReadOnlyList<string> _availableSerialPorts = [];
    private bool _showToolSelect;

    public ShellViewModel(
        IAppLifecycleService lifecycle,
        IAppCloseCoordinator closeCoordinator,
        IAppLogger logger,
        LinkManager linkManager,
        MavlinkProtocol mavlinkProtocol,
        GcsHeartbeatService gcsHeartbeatService,
        MultiVehicleManager multiVehicleManager,
        SettingsViewModel? settingsViewModel = null,
        Func<ISerialPortAdapter>? serialPortAdapterFactory = null,
        ISerialPortEnumerator? serialPortEnumerator = null,
        ILinkConfigurationStore? linkConfigurationStore = null)
    {
        _lifecycle = lifecycle;
        _closeCoordinator = closeCoordinator;
        _logger = logger;
        _linkManager = linkManager;
        _mavlinkProtocol = mavlinkProtocol;
        _gcsHeartbeatService = gcsHeartbeatService;
        _multiVehicleManager = multiVehicleManager;
        _serialPortAdapterFactory = serialPortAdapterFactory ?? (() => new DesktopSerialPortAdapter());
        _serialPortEnumerator = serialPortEnumerator ?? new DesktopSerialPortEnumerator();
        _commLinks = new LinkConfigurationViewModel(linkConfigurationStore ?? new AppSettingsLinkConfigurationStore(new JsonAppSettingsStore()));
        _planViewModel = new PlanViewModel(multiVehicleManager, linkManager);
        _parameterViewModel = new ParameterViewModel(multiVehicleManager);
        _setupViewModel = new SetupViewModel(multiVehicleManager);
        _settingsViewModel = settingsViewModel ?? new SettingsViewModel(SettingsManager.CreateDefault(), new JsonAppSettingsStore());
        _analyzeViewModel = new AnalyzeViewModel(mavlinkProtocol);
        _overviewViewModel = new OverviewViewModel(linkManager, mavlinkProtocol, gcsHeartbeatService, multiVehicleManager, logger.Entries);
        _flyViewModel = new FlyViewModel(linkManager, mavlinkProtocol, multiVehicleManager);
        _currentWorkspace = _flyViewModel;
        Logs = logger.Entries;
        _linkManager.LinksChanged += (_, _) => RefreshToolbarStatus();
        _linkManager.LinkDiagnosticsChanged += (_, _) => RefreshToolbarStatus();
        _mavlinkProtocol.PacketReceived += (_, _) => RefreshToolbarStatus();
        _gcsHeartbeatService.HeartbeatSent += (_, _) => RefreshToolbarStatus();
        _multiVehicleManager.VehiclesChanged += (_, _) => RefreshToolbarStatus();
        _multiVehicleManager.VehicleUpdated += (_, _) => RefreshToolbarStatus();
        InitializeCommand = ReactiveCommand.CreateFromTask(InitializeAsync);
        ShowConnectDrawerCommand = ReactiveCommand.CreateFromTask(OpenSettingsToolAsync);
        AddCommLinkCommand = ReactiveCommand.Create(AddCommLink);
        DeleteCommLinkCommand = ReactiveCommand.Create(DeleteSelectedCommLink);
        SaveCommLinksCommand = ReactiveCommand.CreateFromTask(SaveCommLinksAsync);
        ConnectSelectedCommLinkCommand = ReactiveCommand.CreateFromTask(ConnectSelectedCommLinkAsync);
        RefreshSerialPortsCommand = ReactiveCommand.CreateFromTask(RefreshSerialPortsAsync);
        DisconnectAllCommand = ReactiveCommand.CreateFromTask(DisconnectAllAsync);
        InjectMockHeartbeatCommand = ReactiveCommand.CreateFromTask(InjectMockHeartbeatAsync);
        ShowVehicleIndicatorCommand = ReactiveCommand.Create(() => OpenShellDrawer(ShellDrawerKind.Vehicle));
        ShowLinkIndicatorCommand = ReactiveCommand.Create(() => OpenShellDrawer(ShellDrawerKind.Link));
        ShowTelemetryIndicatorCommand = ReactiveCommand.Create(() => OpenShellDrawer(ShellDrawerKind.Telemetry));
        ShowToolDrawerCommand = ReactiveCommand.Create(OpenToolSelect);
        CloseShellDrawerCommand = ReactiveCommand.Create(CloseShellDrawer);
        CloseToolDrawerCommand = ReactiveCommand.Create(CloseToolDrawer);
        CloseIndicatorDrawerCommand = ReactiveCommand.Create(CloseIndicatorDrawer);
        ToggleIndicatorDrawerExpandedCommand = ReactiveCommand.Create(ToggleIndicatorDrawerExpanded);
        CloseToolSelectCommand = ReactiveCommand.Create(CloseToolSelect);
        ShowFlyCommand = ReactiveCommand.Create(() => ShowPrimaryWorkspace(_flyViewModel));
        ShowPlanCommand = ReactiveCommand.Create(() => ShowPrimaryWorkspace(_planViewModel));
        ShowParametersCommand = ReactiveCommand.Create(() => OpenSetupTool(showParameters: true));
        ShowSetupCommand = ReactiveCommand.Create(() => OpenSetupTool());
        ShowSettingsCommand = ReactiveCommand.CreateFromTask(OpenSettingsToolAsync);
        ShowAnalyzeCommand = ReactiveCommand.Create(() => OpenToolDrawer(ToolDrawerKind.Analyze, _analyzeViewModel));
    }

    public string ApplicationTitle { get; } = "VGC";

    public string ActiveViewName => IsPlanActive ? "Plan" : "Fly";

    public string ToolbarLinkText => _linkManager.Links.Count == 0
        ? "Disconnected"
        : $"{_linkManager.Links.Count(link => link.IsConnected)}/{_linkManager.Links.Count} Links";

    public string ToolbarVehicleText => _multiVehicleManager.ActiveVehicle is null
        ? "No Vehicle"
        : $"Vehicle {_multiVehicleManager.ActiveVehicle.Id} {_multiVehicleManager.ActiveVehicle.FlightModeName}";

    public string ToolbarTelemetryText => $"MAVLink {_mavlinkProtocol.PacketsReceived} | GCS HB {_gcsHeartbeatService.HeartbeatsSent}";

    public string ToolbarLinkDiagnosticsText
    {
        get
        {
            var diagnostics = _linkManager.GetDiagnostics();
            if (diagnostics.Count == 0)
            {
                return "RX 0 B | TX 0 B";
            }

            var bytesReceived = diagnostics.Sum(static item => item.BytesReceived);
            var bytesSent = diagnostics.Sum(static item => item.BytesSent);
            var lastError = diagnostics.LastOrDefault(static item => !string.IsNullOrWhiteSpace(item.LastError))?.LastError;
            var summary = $"RX {FormatBytes(bytesReceived)} | TX {FormatBytes(bytesSent)}";
            return string.IsNullOrWhiteSpace(lastError) ? summary : $"{summary} | Err {lastError}";
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public LinkType[] CommLinkTypes { get; } = [LinkType.Serial, LinkType.Tcp, LinkType.Udp];

    public int[] StandardBaudRates { get; } = [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

    public ObservableCollection<LinkConfigurationItemViewModel> CommLinks => _commLinks.Links;

    public LinkConfigurationItemViewModel? SelectedCommLink
    {
        get => _commLinks.SelectedLink;
        set
        {
            _commLinks.SelectedLink = value;
            RefreshCommLinkProjection();
        }
    }

    public IReadOnlyList<string> AvailableSerialPorts
    {
        get => _availableSerialPorts;
        private set => this.RaiseAndSetIfChanged(ref _availableSerialPorts, value);
    }

    public string CommLinkStatus
    {
        get => _commLinkStatus;
        private set => this.RaiseAndSetIfChanged(ref _commLinkStatus, value);
    }

    public bool IsSelectedCommLinkUdp => SelectedCommLink?.Type == LinkType.Udp;

    public bool IsSelectedCommLinkTcp => SelectedCommLink?.Type == LinkType.Tcp;

    public bool IsSelectedCommLinkSerial => SelectedCommLink?.Type == LinkType.Serial;

    public string SelectedCommLinkSummary => SelectedCommLink is null ? "No Comm Link selected" : BuildCommLinkSummary(SelectedCommLink);

    public ReadOnlyObservableCollection<LogEntry> Logs { get; }

    public ReactiveCommand<Unit, Unit> InitializeCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowConnectDrawerCommand { get; }

    public ReactiveCommand<Unit, Unit> AddCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> SaveCommLinksCommand { get; }

    public ReactiveCommand<Unit, Unit> ConnectSelectedCommLinkCommand { get; }

    public ReactiveCommand<Unit, Unit> RefreshSerialPortsCommand { get; }

    public ReactiveCommand<Unit, Unit> DisconnectAllCommand { get; }

    public ReactiveCommand<Unit, Unit> InjectMockHeartbeatCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowVehicleIndicatorCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowLinkIndicatorCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowTelemetryIndicatorCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowToolDrawerCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseShellDrawerCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseToolDrawerCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseIndicatorDrawerCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleIndicatorDrawerExpandedCommand { get; }

    public ReactiveCommand<Unit, Unit> CloseToolSelectCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowFlyCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowPlanCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowParametersCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowSetupCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowSettingsCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowAnalyzeCommand { get; }

    public NavigationGuard NavigationGuard => _navigationGuard;

    public ToastNotification Toast => _toastNotification;

    public string ToastText
    {
        get => _toastText;
        private set => this.RaiseAndSetIfChanged(ref _toastText, value);
    }

    public bool ShowToast
    {
        get => _showToast;
        private set => this.RaiseAndSetIfChanged(ref _showToast, value);
    }

    public bool ShowFirstRunPrompt
    {
        get => _showFirstRunPrompt;
        private set => this.RaiseAndSetIfChanged(ref _showFirstRunPrompt, value);
    }

    public string FirstRunPromptTitle
    {
        get => _firstRunPromptTitle;
        private set => this.RaiseAndSetIfChanged(ref _firstRunPromptTitle, value);
    }

    public string FirstRunPromptDescription
    {
        get => _firstRunPromptDescription;
        private set => this.RaiseAndSetIfChanged(ref _firstRunPromptDescription, value);
    }

    public void DismissFirstRunPrompt()
    {
        var pending = _firstRunPromptService.GetPendingPrompts();
        if (pending.Count > 0)
        {
            _firstRunPromptService.MarkCompleted(pending[0].Id);
        }

        AdvanceFirstRunPrompt();
    }

    private void AdvanceFirstRunPrompt()
    {
        var pending = _firstRunPromptService.GetPendingPrompts();
        if (pending.Count > 0)
        {
            FirstRunPromptTitle = pending[0].Title;
            FirstRunPromptDescription = pending[0].Description;
            ShowFirstRunPrompt = true;
        }
        else
        {
            ShowFirstRunPrompt = false;
            FirstRunPromptTitle = string.Empty;
            FirstRunPromptDescription = string.Empty;
        }
    }

    public void ShowToastMessage(string text, ToastSeverity severity = ToastSeverity.Info, int timeoutMs = 3000)
    {
        var msg = _toastNotification.Show(text, severity, timeoutMs);
        ToastText = text;
        ShowToast = true;
        _ = HideToastAfterDelay(timeoutMs);
    }

    private async Task HideToastAfterDelay(int delayMs)
    {
        await Task.Delay(delayMs).ConfigureAwait(false);
        ShowToast = false;
    }

    public object CurrentWorkspace
    {
        get => _currentWorkspace;
        private set
        {
            if (ReferenceEquals(_currentWorkspace, value))
            {
                return;
            }

            // Navigation guard check
            if (!_navigationGuard.AllowViewSwitch())
            {
                ShowToastMessage(_navigationGuard.BlockedMessage, ToastSeverity.Warning);
                return;
            }

            this.RaiseAndSetIfChanged(ref _currentWorkspace, value);
            this.RaisePropertyChanged(nameof(IsOverviewActive));
            this.RaisePropertyChanged(nameof(IsFlyActive));
            this.RaisePropertyChanged(nameof(IsPlanActive));
            this.RaisePropertyChanged(nameof(IsParametersActive));
            this.RaisePropertyChanged(nameof(IsSetupActive));
            this.RaisePropertyChanged(nameof(IsSettingsActive));
            this.RaisePropertyChanged(nameof(IsAnalyzeActive));
            this.RaisePropertyChanged(nameof(ActiveViewName));
        }
    }

    public object? CurrentToolWorkspace
    {
        get => _currentToolWorkspace;
        private set => this.RaiseAndSetIfChanged(ref _currentToolWorkspace, value);
    }

    public bool IsToolDrawerActive => _activeToolDrawerKind != ToolDrawerKind.None;

    public bool ShowToolSelect
    {
        get => _showToolSelect;
        private set => this.RaiseAndSetIfChanged(ref _showToolSelect, value);
    }

    public bool IsIndicatorDrawerActive => _activeIndicatorDrawerKind != IndicatorDrawerKind.None;

    public bool IsIndicatorDrawerExpanded
    {
        get => _isIndicatorDrawerExpanded;
        private set => this.RaiseAndSetIfChanged(ref _isIndicatorDrawerExpanded, value);
    }

    public double IndicatorDrawerAnchorX
    {
        get => _indicatorDrawerAnchorX;
        private set => this.RaiseAndSetIfChanged(ref _indicatorDrawerAnchorX, value);
    }

    public bool ShowIndicatorDrawerExpandButton => IsIndicatorDrawerActive && !IsIndicatorDrawerExpanded && !IndicatorDrawerWaitForParameters;

    public string IndicatorDrawerExpandedText => _activeIndicatorDrawerKind switch
    {
        IndicatorDrawerKind.MainStatus => BuildArmIndicatorText(),
        IndicatorDrawerKind.FlightMode => "Mode changes and advanced flight-mode actions appear here when supported by the active vehicle.",
        IndicatorDrawerKind.Gps => _flyViewModel.PositionText,
        IndicatorDrawerKind.Battery => _flyViewModel.BatteryText,
        IndicatorDrawerKind.Rc => _flyViewModel.LinkText,
        IndicatorDrawerKind.Telemetry => ToolbarLinkDiagnosticsText,
        IndicatorDrawerKind.Arm => _flyViewModel.MainStatusDetailText,
        IndicatorDrawerKind.Messages => BuildMessagesIndicatorText(),
        _ => IndicatorDrawerText
    };

    public string IndicatorDrawerTitle => _activeIndicatorDrawerKind switch
    {
        IndicatorDrawerKind.MainStatus => "Vehicle Status",
        IndicatorDrawerKind.FlightMode => "Flight Modes",
        IndicatorDrawerKind.Link => "Link Status",
        IndicatorDrawerKind.Gps => "GPS",
        IndicatorDrawerKind.Battery => "Battery",
        IndicatorDrawerKind.Rc => "RC Link",
        IndicatorDrawerKind.Telemetry => "Telemetry",
        IndicatorDrawerKind.Arm => "Arming",
        IndicatorDrawerKind.Messages => "Vehicle Messages",
        _ => string.Empty
    };

    public string IndicatorDrawerText => _activeIndicatorDrawerKind switch
    {
        IndicatorDrawerKind.MainStatus => _flyViewModel.MainStatusDetailText,
        IndicatorDrawerKind.FlightMode => BuildFlightModeIndicatorText(),
        IndicatorDrawerKind.Link => _flyViewModel.LinkDiagnosticText,
        IndicatorDrawerKind.Gps => BuildGpsIndicatorText(),
        IndicatorDrawerKind.Battery => BuildBatteryIndicatorText(),
        IndicatorDrawerKind.Rc => BuildRcIndicatorText(),
        IndicatorDrawerKind.Telemetry => BuildTelemetryIndicatorText(),
        IndicatorDrawerKind.Arm => BuildArmIndicatorText(),
        IndicatorDrawerKind.Messages => BuildMessagesIndicatorText(),
        _ => string.Empty
    };

    public bool IndicatorDrawerWaitForParameters => _activeIndicatorDrawerKind == IndicatorDrawerKind.FlightMode && !_multiVehicleManager.Vehicles.Any();

    public string IndicatorDrawerWaitingText => "Waiting for parameters...";

    public bool ShowIndicatorPrimaryAction => _activeIndicatorDrawerKind == IndicatorDrawerKind.MainStatus && _flyViewModel.HasActiveVehicle;

    public string IndicatorPrimaryActionText => _flyViewModel.ArmText == "Armed" ? "Disarm" : "Arm";

    public bool ShowIndicatorSecondaryAction => _activeIndicatorDrawerKind == IndicatorDrawerKind.MainStatus && _flyViewModel.HasActiveVehicle;

    public string IndicatorSecondaryActionText => "Takeoff";

    public ReactiveCommand<Unit, GuidedActionStatus> IndicatorPrimaryActionCommand => _flyViewModel.ArmText == "Armed"
        ? _flyViewModel.RequestDisarmActionCommand
        : _flyViewModel.RequestArmActionCommand;

    public ReactiveCommand<Unit, GuidedActionStatus> IndicatorSecondaryActionCommand => _flyViewModel.RequestTakeoffActionCommand;

    public string ToolDrawerTitle => _activeToolDrawerKind switch
    {
        ToolDrawerKind.Analyze => "Analyze Tools",
        ToolDrawerKind.Setup => "Vehicle Configuration",
        ToolDrawerKind.Settings => "Application Settings",
        _ => string.Empty
    };

    public string ToolDrawerIcon => _activeToolDrawerKind switch
    {
        ToolDrawerKind.Analyze => "A",
        ToolDrawerKind.Setup => "G",
        ToolDrawerKind.Settings => "Q",
        _ => string.Empty
    };

    public string ToolDrawerBackText => IsPlanActive ? "Plan" : "Fly";

    private Unit ShowPrimaryWorkspace(object workspace)
    {
        CurrentWorkspace = workspace;
        CloseToolDrawer();
        CloseToolSelect();
        return Unit.Default;
    }

    private Unit OpenSetupTool(bool showParameters = false)
    {
        OpenToolDrawer(ToolDrawerKind.Setup, showParameters ? _parameterViewModel : _setupViewModel);
        return Unit.Default;
    }

    private async Task<Unit> OpenSettingsToolAsync()
    {
        if (_settingsViewModel.Groups.Count == 0)
        {
            await _settingsViewModel.LoadAsync().ConfigureAwait(false);
        }

        await EnsureCommLinksLoadedAsync().ConfigureAwait(false);
        OpenToolDrawer(ToolDrawerKind.Settings, _settingsViewModel);
        CloseToolSelect();
        return Unit.Default;
    }

    private Unit OpenToolDrawer(ToolDrawerKind kind, object workspace)
    {
        if (!_navigationGuard.AllowViewSwitch())
        {
            ShowToastMessage(_navigationGuard.BlockedMessage, ToastSeverity.Warning);
            return Unit.Default;
        }

        _activeToolDrawerKind = kind;
        CurrentToolWorkspace = workspace;
        CloseToolSelect();
        RaiseToolDrawerProjection();
        return Unit.Default;
    }

    private Unit CloseToolDrawer()
    {
        _activeToolDrawerKind = ToolDrawerKind.None;
        CurrentToolWorkspace = null;
        RaiseToolDrawerProjection();
        return Unit.Default;
    }

    public Unit OpenIndicatorDrawer(IndicatorDrawerKind kind, double anchorX = ScreenMetrics.StandardMargin)
    {
        _activeIndicatorDrawerKind = kind;
        IndicatorDrawerAnchorX = anchorX;
        IsIndicatorDrawerExpanded = false;
        RaiseIndicatorDrawerProjection();
        return Unit.Default;
    }

    private Unit ToggleIndicatorDrawerExpanded()
    {
        IsIndicatorDrawerExpanded = !IsIndicatorDrawerExpanded;
        RaiseIndicatorDrawerProjection();
        return Unit.Default;
    }

    private Unit CloseIndicatorDrawer()
    {
        _activeIndicatorDrawerKind = IndicatorDrawerKind.None;
        IsIndicatorDrawerExpanded = false;
        RaiseIndicatorDrawerProjection();
        return Unit.Default;
    }

    private Unit OpenToolSelect()
    {
        ShowToolSelect = true;
        return Unit.Default;
    }

    private Unit CloseToolSelect()
    {
        ShowToolSelect = false;
        return Unit.Default;
    }

    private void RaiseToolDrawerProjection()
    {
        this.RaisePropertyChanged(nameof(IsToolDrawerActive));
        this.RaisePropertyChanged(nameof(ToolDrawerTitle));
        this.RaisePropertyChanged(nameof(ToolDrawerIcon));
    }

    private void RaiseIndicatorDrawerProjection()
    {
        this.RaisePropertyChanged(nameof(IsIndicatorDrawerActive));
        this.RaisePropertyChanged(nameof(IndicatorDrawerTitle));
        this.RaisePropertyChanged(nameof(IndicatorDrawerText));
        this.RaisePropertyChanged(nameof(IndicatorDrawerWaitForParameters));
        this.RaisePropertyChanged(nameof(IndicatorDrawerWaitingText));
        this.RaisePropertyChanged(nameof(IndicatorDrawerAnchorX));
        this.RaisePropertyChanged(nameof(IsIndicatorDrawerExpanded));
        this.RaisePropertyChanged(nameof(ShowIndicatorDrawerExpandButton));
        this.RaisePropertyChanged(nameof(IndicatorDrawerExpandedText));
        this.RaisePropertyChanged(nameof(ShowIndicatorPrimaryAction));
        this.RaisePropertyChanged(nameof(IndicatorPrimaryActionText));
        this.RaisePropertyChanged(nameof(ShowIndicatorSecondaryAction));
        this.RaisePropertyChanged(nameof(IndicatorSecondaryActionText));
        this.RaisePropertyChanged(nameof(IndicatorPrimaryActionCommand));
        this.RaisePropertyChanged(nameof(IndicatorSecondaryActionCommand));
    }

    public bool IsOverviewActive => CurrentWorkspace == _overviewViewModel;

    public bool IsFlyActive => CurrentWorkspace == _flyViewModel;

    public bool IsPlanActive => CurrentWorkspace == _planViewModel;

    public bool IsParametersActive => CurrentWorkspace == _parameterViewModel;

    public bool IsSetupActive => CurrentWorkspace == _setupViewModel;

    public bool IsSettingsActive => CurrentWorkspace == _settingsViewModel;

    public bool IsAnalyzeActive => CurrentWorkspace == _analyzeViewModel;

    public bool IsShellDrawerOpen => _activeDrawerKind != ShellDrawerKind.None;

    public bool IsToolDrawerOpen => _activeDrawerKind == ShellDrawerKind.Tools;

    public bool IsVehicleDrawerOpen => _activeDrawerKind == ShellDrawerKind.Vehicle;

    public bool IsLinkDrawerOpen => _activeDrawerKind == ShellDrawerKind.Link;

    public bool IsTelemetryDrawerOpen => _activeDrawerKind == ShellDrawerKind.Telemetry;

    public string ShellDrawerTitle => _activeDrawerKind switch
    {
        ShellDrawerKind.Vehicle => "Vehicle Status",
        ShellDrawerKind.Link => "Comm Links",
        ShellDrawerKind.Telemetry => "Telemetry",
        ShellDrawerKind.Tools => "Tools",
        _ => string.Empty
    };

    public string ShellDrawerSummary => _activeDrawerKind switch
    {
        ShellDrawerKind.Vehicle => ToolbarVehicleText,
        ShellDrawerKind.Link => CommLinkStatus,
        ShellDrawerKind.Telemetry => ToolbarTelemetryText,
        ShellDrawerKind.Tools => $"Current view: {ActiveViewName}",
        _ => string.Empty
    };

    public string ShellDrawerDetailText => _activeDrawerKind switch
    {
        ShellDrawerKind.Vehicle => BuildVehicleDrawerDetail(),
        ShellDrawerKind.Link => BuildLinkDrawerDetail(),
        ShellDrawerKind.Telemetry => BuildTelemetryDrawerDetail(),
        ShellDrawerKind.Tools => "Open operator tools without changing the map-first Fly/Plan workspace model.",
        _ => string.Empty
    };

    public string IndicatorDetailGpsText => BuildIndicatorDetailGps();

    public string IndicatorDetailBatteryText => BuildIndicatorDetailBattery();

    public string IndicatorDetailRcText => BuildIndicatorDetailRc();

    private string BuildFlightModeIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null) return "No active vehicle.";
        return $"Current: {vehicle.FlightModeName}\nBase mode: {vehicle.BaseMode}\nCustom mode: {vehicle.CustomMode}\nMode changes will be sent through guided actions when supported by the active vehicle.";
    }

    private string BuildGpsIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null) return "No active vehicle GPS data.";
        var coordinate = vehicle.Coordinate is { } c ? $"{c.Latitude:F6}, {c.Longitude:F6}" : "No coordinate";
        var altitude = vehicle.RelativeAltitudeMeters is { } alt ? $"{alt:F1} m relative" : "No relative altitude";
        return $"Fix: {vehicle.GpsFixType?.ToString(CultureInfo.InvariantCulture) ?? "No fix"}\nSatellites: {vehicle.SatelliteCount?.ToString(CultureInfo.InvariantCulture) ?? "—"}\nPosition: {coordinate}\nAltitude: {altitude}";
    }

    private string BuildBatteryIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null) return "No active vehicle battery data.";
        return $"Voltage: {FormatOptional(vehicle.BatteryVoltage, "F2", " V")}\nRemaining: {vehicle.BatteryRemainingPercent?.ToString(CultureInfo.InvariantCulture) ?? "—"}%";
    }

    private string BuildRcIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null) return _flyViewModel.LinkDiagnosticText;
        return $"{_flyViewModel.LinkDiagnosticText}\nVehicle drop rate: {FormatOptional(vehicle.CommunicationDropRatePermille / 10.0, "F1", "%")}\nVehicle errors: {vehicle.CommunicationErrors?.ToString(CultureInfo.InvariantCulture) ?? "—"}";
    }

    private string BuildTelemetryIndicatorText()
    {
        var stats = _mavlinkProtocol.Statistics.Snapshot();
        return $"Packets received: {stats.TotalPacketsReceived}\nPackets lost: {stats.TotalPacketsLost}\nPacket loss: {stats.PacketLossPercent:F1}%\n{_flyViewModel.TelemetryText}";
    }

    private string BuildArmIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null) return "No active vehicle.";
        return $"State: {_flyViewModel.ArmText}\nSystem status: {vehicle.SystemStatus?.ToString(CultureInfo.InvariantCulture) ?? "—"}\nEstimator: {(vehicle.EstimatorOk ? "Healthy" : "Check required")}";
    }

    private string BuildMessagesIndicatorText()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle?.StatusMessages.Count > 0)
        {
            return string.Join("\n", vehicle.StatusMessages.TakeLast(8).Select(message => $"{message.ReceivedAt:HH:mm:ss} [{message.Severity}] {message.Text}"));
        }

        return _flyViewModel.OperatorLayout.WarningSummary;
    }

    private static string FormatOptional(double? value, string format, string suffix) => value is { } number
        ? number.ToString(format, CultureInfo.InvariantCulture) + suffix
        : "—";

    /// <summary>
    /// Performs close checks (unsaved mission, pending parameter writes, active connections).
    /// Returns messages that need user confirmation, or empty if OK to close.
    /// </summary>
    public IReadOnlyList<string> GetCloseWarnings()
    {
        var warnings = new List<string>();

        // Check for active vehicle connections
        if (_multiVehicleManager.ActiveVehicle is not null)
        {
            warnings.Add("There are active vehicle connections. Closing will disconnect.");
        }

        // Check for pending parameter writes
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle?.ParameterManager.PendingWriteCount > 0)
        {
            warnings.Add($"There are {vehicle.ParameterManager.PendingWriteCount} pending parameter writes that will be lost.");
        }

        return warnings;
    }

    public async Task InitializeAsync()
    {
        await _lifecycle.InitializeAsync().ConfigureAwait(false);
        if (!_lifecycle.IsInitialized)
        {
            StatusText = "Not initialized";
            return;
        }

        if (_firstRunPromptService.HasPendingPrompts)
        {
            AdvanceFirstRunPrompt();
        }

        if (await AutoConnectTcpFromEnvironmentAsync().ConfigureAwait(false))
        {
            return;
        }

        StatusText = "Ready for QGC porting M1";
    }

    public Task<AppCloseCheck> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        return _closeCoordinator.CanCloseAsync(cancellationToken);
    }

    private async Task OpenConnectDrawerAsync()
    {
        await EnsureCommLinksLoadedAsync().ConfigureAwait(false);
        OpenShellDrawer(ShellDrawerKind.Link);
    }

    public async Task EnsureCommLinksLoadedAsync()
    {
        if (_commLinksLoaded)
        {
            return;
        }

        await _commLinks.LoadAsync().ConfigureAwait(false);
        if (_commLinks.Links.Count == 0)
        {
            _commLinks.Add(new PersistedLinkConfiguration("UDP Link", LinkType.Udp, 14550));
        }

        _commLinksLoaded = true;
        RefreshCommLinkProjection();
    }

    private Unit AddCommLink()
    {
        var link = _commLinks.Add(new PersistedLinkConfiguration($"Link {_commLinks.Links.Count + 1}", LinkType.Serial, SerialPortName: AvailableSerialPorts.FirstOrDefault() ?? string.Empty, BaudRate: 57600));
        _commLinks.SelectPreferred(link);
        CommLinkStatus = "New Comm Link added.";
        RefreshCommLinkProjection();
        return Unit.Default;
    }

    private Unit DeleteSelectedCommLink()
    {
        _commLinks.DeleteSelected();
        CommLinkStatus = "Comm Link deleted.";
        RefreshCommLinkProjection();
        return Unit.Default;
    }

    private async Task SaveCommLinksAsync()
    {
        await _commLinks.SaveAsync().ConfigureAwait(false);
        CommLinkStatus = "Comm Links saved.";
    }

    public async Task ConnectSelectedCommLinkAsync()
    {
        try
        {
            await EnsureCommLinksLoadedAsync().ConfigureAwait(false);
            var selected = SelectedCommLink ?? throw new InvalidOperationException("Select a Comm Link first.");
            _commLinks.SelectPreferred(selected);
            await _commLinks.SaveAsync().ConfigureAwait(false);
            var link = await ConnectCommLinkAsync(selected.ToConfiguration()).ConfigureAwait(false);
            _linkManager.SetPreferredActiveLink(link.Configuration.Name);
            await _gcsHeartbeatService.StartAsync().ConfigureAwait(false);
            CommLinkStatus = $"Connected {link.Configuration.Name}. Waiting for MAVLink HEARTBEAT.";
            StatusText = CommLinkStatus;
            RefreshCommLinkProjection();
        }
        catch (Exception ex)
        {
            CommLinkStatus = $"Connect failed: {ex.Message}";
            StatusText = CommLinkStatus;
            _logger.Warning(CommLinkStatus);
        }
    }

    private Task<ILinkTransport> ConnectCommLinkAsync(PersistedLinkConfiguration persisted)
    {
        return persisted.ToRuntimeConfiguration() switch
        {
            UdpLinkConfiguration udp => ConnectUdpAsync(udp),
            TcpLinkConfiguration tcp when tcp.IsServer => _linkManager.CreateConnectedTcpServerLinkAsync(tcp),
            TcpLinkConfiguration tcp => _linkManager.CreateConnectedTcpClientLinkAsync(tcp),
            SerialLinkConfiguration serial => _linkManager.CreateConnectedSerialLinkAsync(serial, _serialPortAdapterFactory()),
            _ => throw new InvalidOperationException($"{persisted.Type} links are not supported for manual connection.")
        };
    }

    private Task<ILinkTransport> ConnectUdpAsync(UdpLinkConfiguration configuration)
    {
        ValidatePort(configuration.LocalPort, "UDP local port");
        if (configuration.TargetPort is { } targetPort)
        {
            ValidatePort(targetPort, "UDP target port");
        }

        return _linkManager.CreateConnectedUdpLinkAsync(configuration);
    }

    public async Task RefreshSerialPortsAsync()
    {
        var ports = await _serialPortEnumerator.EnumerateAsync().ConfigureAwait(false);
        AvailableSerialPorts = ports.Select(static port => port.PortName).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (SelectedCommLink is { Type: LinkType.Serial } selected && string.IsNullOrWhiteSpace(selected.SerialPortName) && AvailableSerialPorts.Count > 0)
        {
            selected.SerialPortName = AvailableSerialPorts[0];
        }

        CommLinkStatus = AvailableSerialPorts.Count == 0
            ? "No serial ports found."
            : $"Found {AvailableSerialPorts.Count} serial port(s).";
        RefreshCommLinkProjection();
    }

    private void RefreshCommLinkProjection()
    {
        this.RaisePropertyChanged(nameof(CommLinks));
        this.RaisePropertyChanged(nameof(SelectedCommLink));
        this.RaisePropertyChanged(nameof(IsSelectedCommLinkUdp));
        this.RaisePropertyChanged(nameof(IsSelectedCommLinkTcp));
        this.RaisePropertyChanged(nameof(IsSelectedCommLinkSerial));
        this.RaisePropertyChanged(nameof(SelectedCommLinkSummary));
    }

    private static string BuildCommLinkSummary(LinkConfigurationItemViewModel link)
    {
        return link.Type switch
        {
            LinkType.Serial => $"Serial: {link.SerialPortName ?? ""} @ {link.BaudRate ?? 57600}",
            LinkType.Tcp => $"TCP: {(string.IsNullOrWhiteSpace(link.Host) ? "127.0.0.1" : link.Host)}:{link.Port ?? 5760}{(link.IsServer ? " (server)" : "")}",
            LinkType.Udp => $"UDP: local {link.LocalPort ?? 14550}",
            _ => link.Type.ToString()
        };
    }

    private static void ValidatePort(int port, string label)
    {
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException($"{label} must be 1-65535.");
        }
    }

    private async Task<bool> AutoConnectTcpFromEnvironmentAsync()
    {
        var endpoint = await ResolveStartupTcpEndpointAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        if (!TryParseEndpoint(endpoint, out var host, out var port, out var error))
        {
            var message = $"{AutoConnectTcpEnvironmentVariable} ignored: {error}";
            _logger.Warning(message);
            Console.WriteLine(message);
            StatusText = message;
            return true;
        }

        try
        {
            var existing = _linkManager.Links.FirstOrDefault(link =>
                link is { IsConnected: true, Configuration: TcpLinkConfiguration config }
                && string.Equals(config.Host, host, StringComparison.OrdinalIgnoreCase)
                && config.Port == port
                && !config.IsServer);

            if (existing is null)
            {
                await _linkManager.CreateConnectedTcpClientLinkAsync(
                    new TcpLinkConfiguration("Android TCP AutoConnect", host, port)).ConfigureAwait(false);
            }

            await _gcsHeartbeatService.StartAsync().ConfigureAwait(false);
            var message = $"{AutoConnectTcpEnvironmentVariable} connected {host}:{port}";
            _logger.Info(message);
            Console.WriteLine(message);
            StatusText = $"Android TCP auto-connected to {host}:{port}";
        }
        catch (Exception ex)
        {
            var message = $"{AutoConnectTcpEnvironmentVariable} failed {host}:{port}: {ex.Message}";
            _logger.Error(message);
            Console.WriteLine(message);
            StatusText = message;
        }

        return true;
    }

    private static async Task<string?> ResolveStartupTcpEndpointAsync()
    {
        var endpoint = ReadStartupTcpEndpoint();
        if (!string.IsNullOrWhiteSpace(endpoint) || !OperatingSystem.IsAndroid())
        {
            return endpoint;
        }

        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100).ConfigureAwait(false);
            endpoint = ReadStartupTcpEndpoint();
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                return endpoint;
            }
        }

        return endpoint;
    }

    private static string? ReadStartupTcpEndpoint()
    {
        var endpoint = StartupAutoConnectTcpEndpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = Environment.GetEnvironmentVariable(AutoConnectTcpEnvironmentVariable);
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = Environment.GetEnvironmentVariable(AndroidAutoConnectTcpEnvironmentVariable);
        }

        return endpoint;
    }

    private async Task DisconnectAllAsync()
    {
        await _gcsHeartbeatService.StopAsync().ConfigureAwait(false);
        await _linkManager.DisconnectAllAsync().ConfigureAwait(false);
        StatusText = "All links disconnected";
    }

    private async Task InjectMockHeartbeatAsync()
    {
        var mockLink = _linkManager.Links.OfType<MockLinkTransport>().FirstOrDefault();
        if (mockLink is null)
        {
            mockLink = await _linkManager.CreateConnectedMockLinkAsync().ConfigureAwait(false);
        }

        mockLink.EmitIncoming(MavlinkTestFrames.Heartbeat());
        StatusText = "Injected mock MAVLink HEARTBEAT";
    }

    private void RefreshToolbarStatus()
    {
        RunOnUi(() =>
        {
            this.RaisePropertyChanged(nameof(ToolbarLinkText));
            this.RaisePropertyChanged(nameof(ToolbarLinkDiagnosticsText));
            this.RaisePropertyChanged(nameof(ToolbarVehicleText));
            this.RaisePropertyChanged(nameof(ToolbarTelemetryText));
            RefreshShellDrawerProjection();
        });
    }

    private Unit OpenShellDrawer(ShellDrawerKind kind)
    {
        _activeDrawerKind = kind;
        RefreshShellDrawerProjection();
        return Unit.Default;
    }

    private Unit CloseShellDrawer()
    {
        _activeDrawerKind = ShellDrawerKind.None;
        RefreshShellDrawerProjection();
        return Unit.Default;
    }

    private void RefreshShellDrawerProjection()
    {
        this.RaisePropertyChanged(nameof(IsShellDrawerOpen));
        this.RaisePropertyChanged(nameof(IsToolDrawerOpen));
        this.RaisePropertyChanged(nameof(IsVehicleDrawerOpen));
        this.RaisePropertyChanged(nameof(IsLinkDrawerOpen));
        this.RaisePropertyChanged(nameof(IsTelemetryDrawerOpen));
        this.RaisePropertyChanged(nameof(ShellDrawerTitle));
        this.RaisePropertyChanged(nameof(ShellDrawerSummary));
        this.RaisePropertyChanged(nameof(ShellDrawerDetailText));
        this.RaisePropertyChanged(nameof(IndicatorDetailGpsText));
        this.RaisePropertyChanged(nameof(IndicatorDetailBatteryText));
        this.RaisePropertyChanged(nameof(IndicatorDetailRcText));
    }

    private string BuildVehicleDrawerDetail()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null)
        {
            return "No active vehicle. Connect a MAVLink link and wait for heartbeat before setup, parameters, or guided actions become meaningful.";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"Vehicle {vehicle.Id}\nMode: {vehicle.FlightModeName}\nArmed: {(vehicle.BaseMode & 0x80) != 0}\nGPS: {vehicle.GpsFixType?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} fix, {vehicle.SatelliteCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a"} sats\nBattery: {vehicle.BatteryRemainingPercent?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}%\n\n--- GPS Detail ---\n{IndicatorDetailGpsText}\n\n--- Battery Detail ---\n{IndicatorDetailBatteryText}\n\n--- RC Detail ---\n{IndicatorDetailRcText}");
    }

    private string BuildIndicatorDetailGps()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null)
        {
            return "No GPS data available.";
        }

        var gps = vehicle.Gps;
        var fixType = vehicle.GpsFixType?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
        var satellites = vehicle.SatelliteCount?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
        var hdop = gps.Hdop?.DisplayValue ?? "n/a";
        var vdop = gps.Vdop?.DisplayValue ?? "n/a";
        return $"Fix type: {fixType}\nSatellites: {satellites}\nHDOP: {hdop}\nVDOP: {vdop}";
    }

    private string BuildIndicatorDetailBattery()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null)
        {
            return "No battery data available.";
        }

        var battery = vehicle.Battery;
        var voltage = battery.Voltage?.DisplayValue ?? "n/a";
        var current = battery.Current?.DisplayValue ?? "n/a";
        var remaining = battery.RemainingPercent?.DisplayValue ?? "n/a";
        var temperature = battery.Temperature?.DisplayValue ?? "n/a";
        var cellCount = battery.BatteryCount.ToString(CultureInfo.InvariantCulture);
        return $"Voltage: {voltage}\nCurrent: {current}\nRemaining: {remaining}\nTemperature: {temperature}\nCells: {cellCount}";
    }

    private string BuildIndicatorDetailRc()
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null)
        {
            return "No RC data available.";
        }

        var radio = vehicle.Radio;
        var rssi = radio.Rssi?.DisplayValue ?? "n/a";
        var remoteRssi = radio.RemoteRssi?.DisplayValue ?? "n/a";
        var noise = radio.Noise?.DisplayValue ?? "n/a";
        var errors = radio.Errors?.DisplayValue ?? "n/a";
        return $"RSSI: {rssi}\nRemote RSSI: {remoteRssi}\nNoise: {noise}\nErrors: {errors}";
    }

    private string BuildLinkDrawerDetail()
    {
        var diagnostics = _linkManager.GetDiagnostics();
        if (diagnostics.Count == 0)
        {
            return "Create or select a Comm Link, then Connect.";
        }

        return string.Join(
            Environment.NewLine,
            diagnostics.Select(static item =>
                $"{item.Name}: RX {FormatBytes(item.BytesReceived)}, TX {FormatBytes(item.BytesSent)}, error {(string.IsNullOrWhiteSpace(item.LastError) ? "none" : item.LastError)}"));
    }

    private string BuildTelemetryDrawerDetail()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Packets received: {_mavlinkProtocol.PacketsReceived}\nGCS heartbeats sent: {_gcsHeartbeatService.HeartbeatsSent}\nVehicles: {_multiVehicleManager.Vehicles.Count}\nLatest status: {StatusText}");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        var kib = bytes / 1024.0;
        return kib < 1024
            ? $"{kib:F1} KiB"
            : $"{kib / 1024.0:F1} MiB";
    }

    private static bool TryParseEndpoint(string endpoint, out string host, out int port, out string error)
    {
        endpoint = endpoint.Trim();
        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1)
        {
            host = string.Empty;
            port = 0;
            error = "expected host:port";
            return false;
        }

        host = endpoint[..separator].Trim();
        var portText = endpoint[(separator + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            port = 0;
            error = "host is empty";
            return false;
        }

        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port)
            || port is < 1 or > 65535)
        {
            error = "port must be 1-65535";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void RunOnUi(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
