using System.Collections.ObjectModel;
using ReactiveUI;
using VGC.Facts;
using VGC.Firmware;
using VGC.Input;
using VGC.Setup;
using VGC.Vehicles;

namespace VGC.ViewModels;

public enum SetupPageKind
{
    Summary,
    Parameters,
    Component
}

public enum SetupDetailTabKind
{
    None,
    Safety,
    Sensors,
    Radio,
    FlightModes,
    Generic
}

public sealed class SetupViewModel : ViewModelBase
{
    private readonly MultiVehicleManager _vehicles;
    private readonly FirmwarePluginManager _firmwarePluginManager;
    private readonly VehicleSetupComponentCatalog _catalog;
    private readonly VehicleSetupStatusService _statusService;
    private readonly MotorSafetySetupService _motorSafetyService;
    private readonly SafetyConfigRuntime _safetyConfigRuntime;
    private readonly SensorCalibrationWorkflow _sensorCalibration;
    private readonly RadioCalibrationService _radioCalibrationService;
    private readonly ParameterEditService _parameterEditService;
    private readonly ParameterViewModel _parameterEditor;
    private readonly AirframeSelectionRuntime _airframeSelection = new();
    private readonly PidTuningRuntime _pidTuning = new();
    private readonly JoystickConfigRuntime _joystickConfig = new();
    private AirframeSelectionSnapshot? _airframeSnapshot;
    private PidTuningSnapshot? _pidTuningSnapshot;
    private JoystickConfigSnapshot? _joystickConfigSnapshot;
    private string _summary = "No active vehicle";
    private string _firmwareText = "Firmware: -";
    private string _vehicleTypeText = "Vehicle type: -";
    private MotorSafetySetupStatus _motorStatus = new("Motors", false, true, MotorSafetyActionState.Idle, "No active vehicle", "No device risk data");
    private MotorSafetySetupStatus _safetyStatus = new("Safety", false, true, MotorSafetyActionState.Idle, "No active vehicle", "No device risk data");
    private VehicleSetupComponentStatus? _selectedComponent;
    private SafetyConfigProjection? _safetyConfig;
    private SensorCalibrationSnapshot _sensorCalibrationSnapshot;
    private IReadOnlyList<RadioChannelCalibration> _radioChannels = [];
    private IReadOnlyList<FlightModeMapping> _flightModeMappings = [];
    private SetupPageKind _pageKind = SetupPageKind.Summary;
    private SetupDetailTabKind _detailTabKind = SetupDetailTabKind.None;
    private string _lastParameterEditStatus = "";

    public SetupViewModel(
        MultiVehicleManager vehicles,
        FirmwarePluginManager? firmwarePluginManager = null,
        VehicleSetupComponentCatalog? catalog = null,
        VehicleSetupStatusService? statusService = null,
        MotorSafetySetupService? motorSafetyService = null)
    {
        _vehicles = vehicles;
        _firmwarePluginManager = firmwarePluginManager ?? new FirmwarePluginManager();
        _catalog = catalog ?? new VehicleSetupComponentCatalog();
        _statusService = statusService ?? new VehicleSetupStatusService();
        _motorSafetyService = motorSafetyService ?? new MotorSafetySetupService();
        _safetyConfigRuntime = new SafetyConfigRuntime();
        _sensorCalibration = new SensorCalibrationWorkflow();
        _radioCalibrationService = new RadioCalibrationService();
        _parameterEditService = new ParameterEditService();
        _parameterEditor = new ParameterViewModel(vehicles);
        _sensorCalibrationSnapshot = _sensorCalibration.Snapshot;
        Components = [];
        _vehicles.VehiclesChanged += (_, _) => Refresh();
        _vehicles.VehicleUpdated += (_, _) => Refresh();
        Refresh();
    }

    public string Title => "Setup";

    public ObservableCollection<VehicleSetupComponentStatus> Components { get; }

    public string Summary
    {
        get => _summary;
        private set => this.RaiseAndSetIfChanged(ref _summary, value);
    }

    public string FirmwareText
    {
        get => _firmwareText;
        private set => this.RaiseAndSetIfChanged(ref _firmwareText, value);
    }

    public string VehicleTypeText
    {
        get => _vehicleTypeText;
        private set => this.RaiseAndSetIfChanged(ref _vehicleTypeText, value);
    }

    public MotorSafetySetupStatus MotorStatus
    {
        get => _motorStatus;
        private set => this.RaiseAndSetIfChanged(ref _motorStatus, value);
    }

    public MotorSafetySetupStatus SafetyStatus
    {
        get => _safetyStatus;
        private set => this.RaiseAndSetIfChanged(ref _safetyStatus, value);
    }

    public VehicleSetupComponentStatus? SelectedComponent
    {
        get => _selectedComponent;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedComponent, value);
            PageKind = value is null ? PageKind : SetupPageKind.Component;
            OnComponentSelected(value);
        }
    }

    public SetupPageKind PageKind
    {
        get => _pageKind;
        private set => this.RaiseAndSetIfChanged(ref _pageKind, value);
    }

    public SetupDetailTabKind DetailTabKind
    {
        get => _detailTabKind;
        private set => this.RaiseAndSetIfChanged(ref _detailTabKind, value);
    }

    public string SelectedDetailTab => DetailTabKind switch
    {
        SetupDetailTabKind.Safety => "safety",
        SetupDetailTabKind.Sensors => "sensors",
        SetupDetailTabKind.Radio => "radio",
        SetupDetailTabKind.FlightModes => "flight-modes",
        SetupDetailTabKind.Generic => SelectedComponent?.Id ?? "none",
        _ => "none"
    };

    public SafetyConfigProjection? SafetyConfig
    {
        get => _safetyConfig;
        private set => this.RaiseAndSetIfChanged(ref _safetyConfig, value);
    }

    public SensorCalibrationSnapshot SensorCalibrationState
    {
        get => _sensorCalibrationSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _sensorCalibrationSnapshot, value);
    }

    public IReadOnlyList<RadioChannelCalibration> RadioChannels
    {
        get => _radioChannels;
        private set => this.RaiseAndSetIfChanged(ref _radioChannels, value);
    }

    public IReadOnlyList<FlightModeMapping> FlightModeMappings
    {
        get => _flightModeMappings;
        private set => this.RaiseAndSetIfChanged(ref _flightModeMappings, value);
    }

    public string LastParameterEditStatus
    {
        get => _lastParameterEditStatus;
        private set => this.RaiseAndSetIfChanged(ref _lastParameterEditStatus, value);
    }

    public AirframeSelectionSnapshot? AirframeSnapshot
    {
        get => _airframeSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _airframeSnapshot, value);
    }

    public ParameterViewModel ParameterEditor => _parameterEditor;

    public PidTuningSnapshot? PidTuningSnapshot
    {
        get => _pidTuningSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _pidTuningSnapshot, value);
    }

    public JoystickConfigSnapshot? JoystickConfigSnapshot
    {
        get => _joystickConfigSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _joystickConfigSnapshot, value);
    }

    public bool HasDetailView => PageKind == SetupPageKind.Component && DetailTabKind != SetupDetailTabKind.None;

    public bool ShowSummaryPage => PageKind == SetupPageKind.Summary;

    public bool ShowParametersPage => PageKind == SetupPageKind.Parameters;

    public string SummaryPageText => $"{Summary}\n{FirmwareText}\n{VehicleTypeText}";

    public void ShowSummary()
    {
        SelectSetupPage(SetupPageKind.Summary);
    }

    public void ShowParameters()
    {
        SelectSetupPage(SetupPageKind.Parameters);
    }

    public void HideSpecialPages()
    {
        SelectSetupPage(SetupPageKind.Component, _selectedComponent, _detailTabKind);
    }

    public bool ParametersReady => _vehicles.ActiveVehicle?.ParameterManager.Count > 0;

    public string ParametersStatusText => ParametersReady ? "Parameters ready" : "Waiting for parameters...";

    public bool IsSummarySelected => PageKind == SetupPageKind.Summary;

    public bool IsParametersSelected => PageKind == SetupPageKind.Parameters;

    public string ComponentTreeHeaderText => "Vehicle Setup";

    public bool ShowComponentTreeHeader => true;

    public bool ShowComponentList => HasVehicleConnection;

    public bool ShowSetupSidebarFooter => true;

    public string SetupSidebarFooterText => SetupTreeFooterText;

    public string SpecialPageSelectionText => IsParametersSelected ? "Parameters" : "Summary";

    public string SelectedComponentId => SelectedComponent?.Id ?? string.Empty;

    public bool HasSpecialPageSelection => IsSummarySelected || IsParametersSelected;

    public bool HasComponentSelection => SelectedComponent is not null && !HasSpecialPageSelection;

    public string SetupSelectionSummaryText => HasComponentSelection ? SelectedComponentTitle : SpecialPageSelectionText;

    public bool ShowSetupSelectionSummary => true;

    public string SetupStateSummaryText => HasVehicleConnection ? (ParametersReady ? "Ready" : "Waiting for parameters") : "Disconnected";

    public bool ShowSetupStateSummary => true;

    public bool IsComponentSelected(string componentId) => PageKind == SetupPageKind.Component && string.Equals(SelectedComponent?.Id, componentId, StringComparison.Ordinal);

    public string ComponentReadinessText(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.Readiness.ToString() ?? string.Empty;

    public bool ComponentIsBlocked(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.Readiness == VehicleSetupReadiness.Blocked;

    public bool ComponentIsAvailable(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.IsAvailable == true;

    public string ComponentStatusSummary(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.StatusText ?? string.Empty;

    public string ComponentMissingSummary(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal)) is { MissingParameters.Count: > 0 } component
        ? string.Join(", ", component.MissingParameters)
        : string.Empty;

    public bool ComponentHasMissingSummary(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal)) is { MissingParameters.Count: > 0 };

    public string ComponentTitleText(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.Title ?? componentId;

    public string ComponentSummaryText(string componentId) => Components.FirstOrDefault(component => string.Equals(component.Id, componentId, StringComparison.Ordinal))?.Summary ?? string.Empty;

    public string SpecialPageButtonSummaryText => SetupStateSummaryText;

    public bool ShowSpecialPageButtonSummary => true;

    public string SetupDetailModeText => ShowParametersPage ? "Parameters" : ShowSummaryPage ? "Summary" : SelectedComponentTitle;

    public bool ShowSetupDetailMode => true;

    public string SetupDetailStateText => SetupStateSummaryText;

    public bool ShowSetupDetailState => true;

    public string SetupDisconnectedTitleText => "Vehicle Setup";

    public string SetupDisconnectedBodyText => "Connect a vehicle to begin setup.";

    public string SetupMissingParametersTitleText => "Parameters unavailable";

    public string SetupMissingParametersBodyText => MissingParametersText;

    public string SetupWaitingParametersTitleText => "Waiting for parameters";

    public string SetupWaitingParametersBodyText => ParametersUnavailableText;

    public string SetupSummaryTitleText => "Vehicle Summary";

    public string SetupParametersTitleText => "Parameters";

    public string SetupNoSelectionTitleText => "Select a component from the list";

    public string SetupNoSelectionBodyText => "to view and configure its settings";

    public string SetupSelectedCardTitleText => SelectedComponentCardTitle;

    public string SetupSelectedCardBodyText => SelectedComponentCardSummary;

    public string SetupSelectedCardStatusText => SelectedComponentCardStatus;

    public bool ShowSetupSelectedCard => ShowSelectedComponentCard;

    public bool ShowSetupNoSelectionCard => ShowComponentSelectionHint;

    public bool ShowSetupDisconnectedCard => ShowDisconnectedState;

    public bool ShowSetupMissingParametersCard => ShowMissingParametersState;

    public bool ShowSetupWaitingParametersCard => ShowParametersUnavailableBanner;

    public bool ShowSetupSummaryCardHost => ShowSummaryDetails;

    public bool ShowSetupParametersCardHost => ShowParametersReadyBanner;

    public bool ShowSetupDetailHost => ShowRegularSetupDetails;

    public bool ShowSetupPanelContent => ShowSetupPanelHost;

    public bool ShowSetupSpecialButtons => true;

    public string SummaryButtonText => "Summary";

    public string ParametersButtonText => "Parameters";

    public bool ShowSummaryButton => true;

    public bool ShowParametersButton => true;

    public bool ShowSetupFooter => true;

    public string SetupFooterText => SetupSidebarFooterText;

    public bool ShowSetupSidebarSummaryText => true;

    public string SetupSidebarSummaryText => SetupSelectionSummaryText;

    public bool ShowSetupSidebarStateText => true;

    public string SetupSidebarStateText => SetupStateSummaryText;

    public bool ShowSetupComponentCountText => true;

    public string SetupComponentCountText => SetupTreeFooterText;

    public bool ShowSetupHeaderMeta => true;

    public string SetupHeaderMetaText => SetupStateSummaryText;

    public bool ShowSetupHeaderSummary => true;

    public string SetupHeaderSummaryText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderVehicleText => true;

    public string SetupHeaderVehicleText => Summary;

    public bool ShowSetupHeaderFirmwareText => true;

    public string SetupHeaderFirmwareText => FirmwareText;

    public bool ShowSetupHeaderVehicleTypeText => true;

    public string SetupHeaderVehicleTypeText => VehicleTypeText;

    public bool ShowSetupHeaderCountsText => true;

    public string SetupHeaderCountsText => SetupTreeFooterText;

    public bool ShowSetupHeaderConnectionText => true;

    public string SetupHeaderConnectionText => VehicleConnectionText;

    public bool ShowSetupHeaderParameterStateText => true;

    public string SetupHeaderParameterStateText => ParameterReadyText;

    public bool ShowSetupHeaderModeText => true;

    public string SetupHeaderModeText => SetupDetailModeText;

    public bool ShowSetupHeaderDetailStateText => true;

    public string SetupHeaderDetailStateText => SetupDetailStateText;

    public bool ShowSetupHeaderSelectionText => true;

    public string SetupHeaderSelectionText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderFooterText => true;

    public string SetupHeaderFooterText => SetupFooterText;

    public bool ShowSetupHeaderPanelStateText => true;

    public string SetupHeaderPanelStateText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelSelectionText => true;

    public string SetupHeaderPanelSelectionText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelCountsText => true;

    public string SetupHeaderPanelCountsText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelVehicleText => true;

    public string SetupHeaderPanelVehicleText => Summary;

    public bool ShowSetupHeaderPanelFirmwareText => true;

    public string SetupHeaderPanelFirmwareText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeText => true;

    public string SetupHeaderPanelVehicleTypeText => VehicleTypeText;

    public bool ShowSetupHeaderPanelParameterStateText => true;

    public string SetupHeaderPanelParameterStateText => ParameterReadyText;

    public bool ShowSetupHeaderPanelConnectionText => true;

    public string SetupHeaderPanelConnectionText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelModeText => true;

    public string SetupHeaderPanelModeText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelFooterText => true;

    public string SetupHeaderPanelFooterText => SetupFooterText;

    public bool ShowSetupHeaderPanelMetaText => true;

    public string SetupHeaderPanelMetaText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelHintText => true;

    public string SetupHeaderPanelHintText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelBadgeText => true;

    public string SetupHeaderPanelBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelSummaryText => true;

    public string SetupHeaderPanelSummaryText => Summary;

    public bool ShowSetupHeaderPanelStatusText => true;

    public string SetupHeaderPanelStatusText => ParameterReadyText;

    public bool ShowSetupHeaderPanelSelectionSummaryText => true;

    public string SetupHeaderPanelSelectionSummaryText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelStateSummaryText => true;

    public string SetupHeaderPanelStateSummaryText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelCountSummaryText => true;

    public string SetupHeaderPanelCountSummaryText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelConnectionSummaryText => true;

    public string SetupHeaderPanelConnectionSummaryText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelModeSummaryText => true;

    public string SetupHeaderPanelModeSummaryText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelVehicleSummaryText => true;

    public string SetupHeaderPanelVehicleSummaryText => Summary;

    public bool ShowSetupHeaderPanelFirmwareSummaryText => true;

    public string SetupHeaderPanelFirmwareSummaryText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeSummaryText => true;

    public string SetupHeaderPanelVehicleTypeSummaryText => VehicleTypeText;

    public bool ShowSetupHeaderPanelParameterSummaryText => true;

    public string SetupHeaderPanelParameterSummaryText => ParameterReadyText;

    public bool ShowSetupHeaderPanelFooterSummaryText => true;

    public string SetupHeaderPanelFooterSummaryText => SetupFooterText;

    public bool ShowSetupHeaderPanelSelectionBadgeText => true;

    public string SetupHeaderPanelSelectionBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelStateBadgeText => true;

    public string SetupHeaderPanelStateBadgeText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelCountsBadgeText => true;

    public string SetupHeaderPanelCountsBadgeText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelConnectionBadgeText => true;

    public string SetupHeaderPanelConnectionBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelModeBadgeText => true;

    public string SetupHeaderPanelModeBadgeText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelVehicleBadgeText => true;

    public string SetupHeaderPanelVehicleBadgeText => Summary;

    public bool ShowSetupHeaderPanelFirmwareBadgeText => true;

    public string SetupHeaderPanelFirmwareBadgeText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeBadgeText => true;

    public string SetupHeaderPanelVehicleTypeBadgeText => VehicleTypeText;

    public bool ShowSetupHeaderPanelParameterBadgeText => true;

    public string SetupHeaderPanelParameterBadgeText => ParameterReadyText;

    public bool ShowSetupHeaderPanelFooterBadgeText => true;

    public string SetupHeaderPanelFooterBadgeText => SetupFooterText;

    public bool ShowSetupHeaderPanelReadyBadgeText => true;

    public string SetupHeaderPanelReadyBadgeText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelSelectedBadgeText => true;

    public string SetupHeaderPanelSelectedBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelInfoBadgeText => true;

    public string SetupHeaderPanelInfoBadgeText => Summary;

    public bool ShowSetupHeaderPanelCountsInfoText => true;

    public string SetupHeaderPanelCountsInfoText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelStateInfoText => true;

    public string SetupHeaderPanelStateInfoText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelConnectionInfoText => true;

    public string SetupHeaderPanelConnectionInfoText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelParameterInfoText => true;

    public string SetupHeaderPanelParameterInfoText => ParameterReadyText;

    public bool ShowSetupHeaderPanelModeInfoText => true;

    public string SetupHeaderPanelModeInfoText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelSelectionInfoText => true;

    public string SetupHeaderPanelSelectionInfoText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelVehicleInfoText => true;

    public string SetupHeaderPanelVehicleInfoText => Summary;

    public bool ShowSetupHeaderPanelFirmwareInfoText => true;

    public string SetupHeaderPanelFirmwareInfoText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeInfoText => true;

    public string SetupHeaderPanelVehicleTypeInfoText => VehicleTypeText;

    public bool ShowSetupHeaderPanelFooterInfoText => true;

    public string SetupHeaderPanelFooterInfoText => SetupFooterText;

    public bool ShowSetupHeaderPanelReadyInfoText => true;

    public string SetupHeaderPanelReadyInfoText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelSelectedInfoText => true;

    public string SetupHeaderPanelSelectedInfoText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelSummaryInfoText => true;

    public string SetupHeaderPanelSummaryInfoText => Summary;

    public bool ShowSetupHeaderPanelCountsInfoBadgeText => true;

    public string SetupHeaderPanelCountsInfoBadgeText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelStateInfoBadgeText => true;

    public string SetupHeaderPanelStateInfoBadgeText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelConnectionInfoBadgeText => true;

    public string SetupHeaderPanelConnectionInfoBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelParameterInfoBadgeText => true;

    public string SetupHeaderPanelParameterInfoBadgeText => ParameterReadyText;

    public bool ShowSetupHeaderPanelModeInfoBadgeText => true;

    public string SetupHeaderPanelModeInfoBadgeText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelSelectionInfoBadgeText => true;

    public string SetupHeaderPanelSelectionInfoBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelVehicleInfoBadgeText => true;

    public string SetupHeaderPanelVehicleInfoBadgeText => Summary;

    public bool ShowSetupHeaderPanelFirmwareInfoBadgeText => true;

    public string SetupHeaderPanelFirmwareInfoBadgeText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeInfoBadgeText => true;

    public string SetupHeaderPanelVehicleTypeInfoBadgeText => VehicleTypeText;

    public bool ShowSetupHeaderPanelFooterInfoBadgeText => true;

    public string SetupHeaderPanelFooterInfoBadgeText => SetupFooterText;

    public bool ShowSetupHeaderPanelReadyInfoBadgeText => true;

    public string SetupHeaderPanelReadyInfoBadgeText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelSelectedInfoBadgeText => true;

    public string SetupHeaderPanelSelectedInfoBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelSummaryInfoBadgeText => true;

    public string SetupHeaderPanelSummaryInfoBadgeText => Summary;

    public bool ShowSetupHeaderPanelMetaInfoText => true;

    public string SetupHeaderPanelMetaInfoText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelMetaInfoBadgeText => true;

    public string SetupHeaderPanelMetaInfoBadgeText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelHintInfoText => true;

    public string SetupHeaderPanelHintInfoText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelHintInfoBadgeText => true;

    public string SetupHeaderPanelHintInfoBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelBadgeInfoText => true;

    public string SetupHeaderPanelBadgeInfoText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelBadgeInfoBadgeText => true;

    public string SetupHeaderPanelBadgeInfoBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelStatusInfoText => true;

    public string SetupHeaderPanelStatusInfoText => ParameterReadyText;

    public bool ShowSetupHeaderPanelStatusInfoBadgeText => true;

    public string SetupHeaderPanelStatusInfoBadgeText => ParameterReadyText;

    public bool ShowSetupHeaderPanelDescriptionInfoText => true;

    public string SetupHeaderPanelDescriptionInfoText => Summary;

    public bool ShowSetupHeaderPanelDescriptionInfoBadgeText => true;

    public string SetupHeaderPanelDescriptionInfoBadgeText => Summary;

    public bool ShowSetupHeaderPanelFooterDescriptionText => true;

    public string SetupHeaderPanelFooterDescriptionText => SetupFooterText;

    public bool ShowSetupHeaderPanelFooterDescriptionBadgeText => true;

    public string SetupHeaderPanelFooterDescriptionBadgeText => SetupFooterText;

    public bool ShowSetupHeaderPanelConnectionDescriptionText => true;

    public string SetupHeaderPanelConnectionDescriptionText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelConnectionDescriptionBadgeText => true;

    public string SetupHeaderPanelConnectionDescriptionBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelModeDescriptionText => true;

    public string SetupHeaderPanelModeDescriptionText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelModeDescriptionBadgeText => true;

    public string SetupHeaderPanelModeDescriptionBadgeText => SetupDetailModeText;

    public bool ShowSetupHeaderPanelSelectionDescriptionText => true;

    public string SetupHeaderPanelSelectionDescriptionText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelSelectionDescriptionBadgeText => true;

    public string SetupHeaderPanelSelectionDescriptionBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelVehicleDescriptionText => true;

    public string SetupHeaderPanelVehicleDescriptionText => Summary;

    public bool ShowSetupHeaderPanelVehicleDescriptionBadgeText => true;

    public string SetupHeaderPanelVehicleDescriptionBadgeText => Summary;

    public bool ShowSetupHeaderPanelFirmwareDescriptionText => true;

    public string SetupHeaderPanelFirmwareDescriptionText => FirmwareText;

    public bool ShowSetupHeaderPanelFirmwareDescriptionBadgeText => true;

    public string SetupHeaderPanelFirmwareDescriptionBadgeText => FirmwareText;

    public bool ShowSetupHeaderPanelVehicleTypeDescriptionText => true;

    public string SetupHeaderPanelVehicleTypeDescriptionText => VehicleTypeText;

    public bool ShowSetupHeaderPanelVehicleTypeDescriptionBadgeText => true;

    public string SetupHeaderPanelVehicleTypeDescriptionBadgeText => VehicleTypeText;

    public bool ShowSetupHeaderPanelParameterDescriptionText => true;

    public string SetupHeaderPanelParameterDescriptionText => ParameterReadyText;

    public bool ShowSetupHeaderPanelParameterDescriptionBadgeText => true;

    public string SetupHeaderPanelParameterDescriptionBadgeText => ParameterReadyText;

    public bool ShowSetupHeaderPanelCountsDescriptionText => true;

    public string SetupHeaderPanelCountsDescriptionText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelCountsDescriptionBadgeText => true;

    public string SetupHeaderPanelCountsDescriptionBadgeText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelStateDescriptionText => true;

    public string SetupHeaderPanelStateDescriptionText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelStateDescriptionBadgeText => true;

    public string SetupHeaderPanelStateDescriptionBadgeText => SetupStateSummaryText;

    public bool ShowSetupHeaderPanelMetaDescriptionText => true;

    public string SetupHeaderPanelMetaDescriptionText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelMetaDescriptionBadgeText => true;

    public string SetupHeaderPanelMetaDescriptionBadgeText => SetupTreeFooterText;

    public bool ShowSetupHeaderPanelHintDescriptionText => true;

    public string SetupHeaderPanelHintDescriptionText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelHintDescriptionBadgeText => true;

    public string SetupHeaderPanelHintDescriptionBadgeText => SetupSelectionSummaryText;

    public bool ShowSetupHeaderPanelBadgeDescriptionText => true;

    public string SetupHeaderPanelBadgeDescriptionText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelBadgeDescriptionBadgeText => true;

    public string SetupHeaderPanelBadgeDescriptionBadgeText => VehicleConnectionText;

    public bool ShowSetupHeaderPanelStatusDescriptionText => true;

    public string SetupHeaderPanelStatusDescriptionText => ParameterReadyText;

    public bool ShowSetupHeaderPanelStatusDescriptionBadgeText => true;

    public string SetupHeaderPanelStatusDescriptionBadgeText => ParameterReadyText;

    public bool ShowSetupHeaderPanelSummaryDescriptionText => true;

    public string SetupHeaderPanelSummaryDescriptionText => Summary;

    public bool ShowSetupHeaderPanelSummaryDescriptionBadgeText => true;

    public string SetupHeaderPanelSummaryDescriptionBadgeText => Summary;

    public string SummaryStatusText => _vehicles.ActiveVehicle is null ? "Disconnected" : "Vehicle connected";

    public string ParametersShortcutText => "Open Parameter Editor";

    public string SetupPrerequisiteText => SelectedComponent?.StatusText ?? string.Empty;

    public bool HasSelectedComponent => SelectedComponent is not null;

    public string SelectedComponentTitle => SelectedComponent?.Title ?? string.Empty;

    public string SelectedComponentSummary => SelectedComponent?.Summary ?? string.Empty;

    public string SelectedComponentStatusText => SelectedComponent?.StatusText ?? string.Empty;

    public string SelectedComponentMissingText => SelectedComponent is { MissingParameters.Count: > 0 } component
        ? string.Join(", ", component.MissingParameters)
        : string.Empty;

    public bool HasSelectedComponentMissingParameters => SelectedComponent is { MissingParameters.Count: > 0 };

    public bool CanShowParameters => _vehicles.ActiveVehicle is not null;

    public bool HasVehicleConnection => _vehicles.ActiveVehicle is not null;

    public string VehicleConnectionText => HasVehicleConnection ? "Connected" : "Disconnected";

    public string ParameterReadyText => ParametersReady ? "Parameter cache available." : "Parameter cache not ready.";

    public bool ShowParametersUnavailableBanner => ShowParametersPage && !ParametersReady;

    public bool ShowParametersReadyBanner => ShowParametersPage && ParametersReady;

    public string ParametersUnavailableText => "No parameter cache yet.";

    public string SetupTreeFooterText => $"Available {AvailableComponentCount} | Blocked {BlockedComponentCount}";

    public bool ShowSetupTreeFooter => true;

    public bool ShowSummaryDetails => ShowSummaryPage && HasVehicleConnection && ParametersReady;

    public bool ShowParametersDetails => ShowParametersPage && ParametersReady;

    public bool ShowRegularSetupDetails => !ShowSummaryPage && !ShowParametersPage && ParametersReady;

    public bool ShowMissingParametersState => HasVehicleConnection && !ParametersReady && !ShowParametersPage;

    public bool ShowDisconnectedState => !HasVehicleConnection;

    public bool ShowSetupPanelHost => true;

    public string MissingParametersText => "Vehicle did not return a complete parameter set. Setup pages stay unavailable until parameters are ready.";

    public bool ShowComponentSelectionHint => !HasDetailView && !ShowSummaryPage && !ShowParametersPage;

    public bool ShowSelectedComponentCard => HasSelectedComponent;

    public string SelectedComponentCardTitle => SelectedComponentTitle;

    public string SelectedComponentCardSummary => SelectedComponentSummary;

    public string SelectedComponentCardStatus => SelectedComponentStatusText;

    public int AvailableComponentCount => Components.Count(component => component.IsAvailable);

    public int BlockedComponentCount => Components.Count(component => component.Readiness == VehicleSetupReadiness.Blocked);

    public void SelectComponent(string componentId)
    {
        var component = Components.FirstOrDefault(c => c.Id == componentId);
        if (component is not null)
        {
            SelectedComponent = component;
        }
    }

    public void StartSensorCalibration(SensorCalibrationType calibrationType)
    {
        _sensorCalibration.RequestStart(calibrationType);
        SensorCalibrationState = _sensorCalibration.Snapshot;
    }

    public void ConfirmSensorCalibration()
    {
        _sensorCalibration.ConfirmStart();
        SensorCalibrationState = _sensorCalibration.Snapshot;
    }

    public void CancelSensorCalibration()
    {
        _sensorCalibration.Cancel();
        SensorCalibrationState = _sensorCalibration.Snapshot;
    }

    public void ResetSensorCalibration()
    {
        _sensorCalibration.Reset();
        SensorCalibrationState = _sensorCalibration.Snapshot;
    }

    public void CommitSafetyParameterEdit(string parameterName, string value)
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            LastParameterEditStatus = "No active vehicle";
            return;
        }

        var result = _parameterEditService.Commit(vehicle.ParameterManager, 1, parameterName, value);
        LastParameterEditStatus = result.StatusText;
        RefreshSafetyConfig();
    }

    private void OnComponentSelected(VehicleSetupComponentStatus? component)
    {
        if (component is null)
        {
            DetailTabKind = SetupDetailTabKind.None;
            RaiseSetupNavigationProperties();
            return;
        }

        DetailTabKind = component.Id switch
        {
            "safety" => SetupDetailTabKind.Safety,
            "sensors" => SetupDetailTabKind.Sensors,
            "radio" => SetupDetailTabKind.Radio,
            "flight-modes" => SetupDetailTabKind.FlightModes,
            _ => SetupDetailTabKind.Generic
        };

        switch (component.Id)
        {
            case "safety":
                RefreshSafetyConfig();
                break;
            case "sensors":
                SensorCalibrationState = _sensorCalibration.Snapshot;
                break;
            case "radio":
                RefreshRadioConfig();
                break;
            case "flight-modes":
                RefreshFlightModes();
                break;
            case "airframe":
                RefreshAirframeSelection();
                break;
            case "motors":
                RefreshPidTuning();
                break;
            case "joystick":
                RefreshJoystickConfig();
                break;
        }

        RaiseSetupNavigationProperties();
    }

    private void SelectSetupPage(SetupPageKind page, VehicleSetupComponentStatus? component = null, SetupDetailTabKind? detailTab = null)
    {
        PageKind = page;
        if (!ReferenceEquals(_selectedComponent, component))
        {
            this.RaiseAndSetIfChanged(ref _selectedComponent, component, nameof(SelectedComponent));
        }
        DetailTabKind = detailTab ?? (page == SetupPageKind.Component && component is not null ? DetailTabKind : SetupDetailTabKind.None);
        RaiseSetupNavigationProperties();
    }

    private void RaiseSetupNavigationProperties()
    {
        this.RaisePropertyChanged(nameof(SelectedDetailTab));
        this.RaisePropertyChanged(nameof(HasDetailView));
        this.RaisePropertyChanged(nameof(ShowSummaryPage));
        this.RaisePropertyChanged(nameof(ShowParametersPage));
        this.RaisePropertyChanged(nameof(IsSummarySelected));
        this.RaisePropertyChanged(nameof(IsParametersSelected));
        this.RaisePropertyChanged(nameof(HasSpecialPageSelection));
        this.RaisePropertyChanged(nameof(HasComponentSelection));
        this.RaisePropertyChanged(nameof(SpecialPageSelectionText));
        this.RaisePropertyChanged(nameof(SetupSelectionSummaryText));
        this.RaisePropertyChanged(nameof(SetupDetailModeText));
        this.RaisePropertyChanged(nameof(SelectedComponentId));
    }

    private void RefreshSafetyConfig()
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            SafetyConfig = null;
            return;
        }

        var firmware = _firmwarePluginManager.GetPlugin(vehicle.Autopilot);
        var isArduPilot = firmware.Name.Contains("ArduPilot", StringComparison.OrdinalIgnoreCase) ||
                          firmware.Name.Contains("APM", StringComparison.OrdinalIgnoreCase);
        SafetyConfig = _safetyConfigRuntime.Project(vehicle.ParameterManager, isArduPilot);
    }

    private void RefreshRadioConfig()
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            RadioChannels = [];
            return;
        }

        RadioChannels = _radioCalibrationService.BuildChannelMap(vehicle.ParameterManager);
    }

    private void RefreshFlightModes()
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            FlightModeMappings = [];
            return;
        }

        var firmware = _firmwarePluginManager.GetPlugin(vehicle.Autopilot);
        var modes = firmware.Behavior.FlightModes;
        var pwmStep = modes.Count > 0 ? 1000 / modes.Count : 1000;
        FlightModeMappings = modes
            .Select((mode, index) =>
            {
                var pwmLow = 1000 + index * pwmStep;
                var pwmHigh = index == modes.Count - 1 ? 2000 : 1000 + (index + 1) * pwmStep - 1;
                return new FlightModeMapping(
                    index + 1,
                    mode.Name,
                    mode.CanSet ? "Settable" : "Read-only",
                    $"{pwmLow}-{pwmHigh}",
                    index == 0);
            })
            .ToArray();
    }

    private void RefreshAirframeSelection()
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            AirframeSnapshot = null;
            return;
        }

        _airframeSelection.GetAvailableAirframes(vehicle.VehicleType);
        AirframeSnapshot = _airframeSelection.Snapshot;
    }

    private void RefreshPidTuning()
    {
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            PidTuningSnapshot = null;
            return;
        }

        PidTuningSnapshot = _pidTuning.LoadFromParameters(vehicle.ParameterManager);
    }

    private void RefreshJoystickConfig()
    {
        JoystickConfigSnapshot = _joystickConfig.Snapshot;
    }

    public void SelectAirframe(AirframeType type)
    {
        AirframeSnapshot = _airframeSelection.Select(type);
    }

    public void UpdatePidParameter(string name, double value)
    {
        PidTuningSnapshot = _pidTuning.UpdateParameter(name, value);
    }

    public void LoadJoystickDevice(JoystickDevice device)
    {
        JoystickConfigSnapshot = _joystickConfig.LoadDevice(device);
    }

    public void MapJoystickAxis(int axisIndex, JoystickAxisFunction function, bool reversed = false)
    {
        JoystickConfigSnapshot = _joystickConfig.MapAxis(axisIndex, function, reversed);
    }

    public void MapJoystickButton(int buttonIndex, string action)
    {
        JoystickConfigSnapshot = _joystickConfig.MapButton(buttonIndex, action);
    }

    private void Refresh()
    {
        Components.Clear();
        var vehicle = _vehicles.ActiveVehicle;
        if (vehicle is null)
        {
            Summary = "No active vehicle";
            FirmwareText = "Firmware: -";
            VehicleTypeText = "Vehicle type: -";
            MotorStatus = new MotorSafetySetupStatus("Motors", false, true, MotorSafetyActionState.Idle, "No active vehicle", "No device risk data");
            SafetyStatus = new MotorSafetySetupStatus("Safety", false, true, MotorSafetyActionState.Idle, "No active vehicle", "No device risk data");
            SafetyConfig = null;
            RadioChannels = [];
            FlightModeMappings = [];
            this.RaisePropertyChanged(nameof(AvailableComponentCount));
            this.RaisePropertyChanged(nameof(BlockedComponentCount));
            return;
        }

        var firmware = _firmwarePluginManager.GetPlugin(vehicle.Autopilot);
        var components = _catalog.GetComponents(firmware, vehicle.VehicleType);
        Summary = $"Vehicle {vehicle.Id} setup summary";
        FirmwareText = $"Firmware: {firmware.Name}";
        VehicleTypeText = $"Vehicle type: {vehicle.VehicleType}";
        foreach (var component in components)
        {
            Components.Add(_statusService.Project(component, vehicle.ParameterManager));
        }

        var motorComponent = components.FirstOrDefault(component => component.Id == "motors");
        var safetyComponent = components.FirstOrDefault(component => component.Id == "safety");
        if (motorComponent is not null)
        {
            MotorStatus = _motorSafetyService.Project(motorComponent, vehicle, _firmwarePluginManager);
        }

        if (safetyComponent is not null)
        {
            SafetyStatus = _motorSafetyService.Project(safetyComponent, vehicle, _firmwarePluginManager);
        }

        Summary = $"Vehicle {vehicle.Id} setup summary | Blocked {BlockedComponentCount}";
        this.RaisePropertyChanged(nameof(AvailableComponentCount));
        this.RaisePropertyChanged(nameof(BlockedComponentCount));

        // Refresh detail view if component is selected
        if (_selectedComponent is not null)
        {
            OnComponentSelected(_selectedComponent);
        }
    }
}

public sealed record FlightModeMapping(int Slot, string ModeName, string Description, string PwmRange, bool IsActive);
