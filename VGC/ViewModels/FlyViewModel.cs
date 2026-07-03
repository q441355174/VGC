using System.Reactive;
using ReactiveUI;
using VGC.Comms;
using VGC.Input;
using VGC.Maps;
using VGC.Mavlink;
using VGC.Payload;
using VGC.Setup;
using VGC.Vehicles;

namespace VGC.ViewModels;

public sealed record FlyViewOperatorLayout(
    string VehicleSummary,
    string ModeSummary,
    string ArmSummary,
    string LinkSummary,
    string GpsSummary,
    string BatterySummary,
    string PositionSummary,
    string AttitudeSummary,
    string SpeedSummary,
    string EstimatorSummary,
    string MapSummary,
    string MapProviderSummary,
    string WarningSummary,
    string PayloadSummary,
    bool HasActiveVehicle,
    bool HasWarning,
    bool HasPayloadActivity,
    bool IsMapFollowingVehicle,
    bool IsEstimatorHealthy);

public sealed record FlyToolbarIndicatorItem(IndicatorDrawerKind Kind, string Label, string Value, bool IsVisible = true);

public sealed class FlyViewModel : ViewModelBase
{
    private const byte SafetyArmed = 0x80;
    private readonly LinkManager _linkManager;
    private readonly MavlinkProtocol _mavlinkProtocol;
    private readonly MultiVehicleManager _multiVehicleManager;
    private readonly VehicleMapOverlayProjector _overlayProjector = new();
    private readonly MapProviderHost _mapProviderHost;
    private readonly MapInteractionState _mapInteractionState = new();
    private readonly VideoStreamRuntimeController _videoRuntime;
    private readonly CameraRuntimeController _cameraRuntime;
    private readonly GimbalRuntimeController _gimbalRuntime;
    private readonly GuidedActionController _guidedActions;
    private readonly PreflightChecklistService _preflightService = new();
    private readonly VirtualJoystickRuntime _virtualJoystick = new();
    private readonly ProximityRadarRuntime _proximityRadar = new();
    private readonly CustomActionRuntime _customActions = new();
    private readonly GimbalTouchControl _gimbalTouchControl = new();
    private ProximityRadarSnapshot? _proximityRadarSnapshot;
    private CustomActionSnapshot? _customActionSnapshot;
    private GimbalTouchState? _gimbalTouchState;
    private PreflightChecklist _preflight = new("No vehicle", [], true, false, "Preflight: no vehicle");
    private readonly List<VehicleCoordinate> _trajectory = [];
    private VehicleCoordinate? _homeCoordinate;
    private bool _virtualJoystickVisible;
    private bool _wasMissionActive;
    private bool _showMissionCompleteDialog;
    private string _missionCompleteText = string.Empty;

    public FlyViewModel(
        LinkManager linkManager,
        MavlinkProtocol mavlinkProtocol,
        MultiVehicleManager multiVehicleManager,
        MapProviderHost? mapProviderHost = null,
        VideoStreamRuntimeController? videoRuntime = null,
        CameraRuntimeController? cameraRuntime = null,
        GimbalRuntimeController? gimbalRuntime = null,
        GuidedActionController? guidedActions = null)
    {
        _linkManager = linkManager;
        _mavlinkProtocol = mavlinkProtocol;
        _multiVehicleManager = multiVehicleManager;
        _mapProviderHost = mapProviderHost ?? MapProviderHost.CreateLocalOnly();
        _videoRuntime = videoRuntime ?? new VideoStreamRuntimeController();
        _cameraRuntime = cameraRuntime ?? new CameraRuntimeController();
        _gimbalRuntime = gimbalRuntime ?? new GimbalRuntimeController();
        _guidedActions = guidedActions ?? new GuidedActionController();
        _linkManager.LinksChanged += (_, _) => Refresh();
        _mavlinkProtocol.PacketReceived += (_, _) => Refresh();
        _mavlinkProtocol.CommandAckReceived += (_, ack) =>
        {
            if (_guidedActions.HandleCommandAck(ack))
            {
                RefreshGuidedActions();
            }
        };
        _multiVehicleManager.VehiclesChanged += (_, _) => Refresh();
        _multiVehicleManager.VehicleUpdated += (_, _) => Refresh();
        RecenterMapCommand = ReactiveCommand.Create(() =>
        {
            RecenterMap();
            return Unit.Default;
        });
        CaptureImageCommand = ReactiveCommand.Create(() =>
        {
            _cameraRuntime.BeginImageCapture();
            RefreshPayload();
            return Unit.Default;
        });
        RecordVideoCommand = ReactiveCommand.Create(() =>
        {
            _cameraRuntime.BeginVideoRecordingCommand();
            RefreshPayload();
            return Unit.Default;
        });
        TiltGimbalDownCommand = ReactiveCommand.Create(() =>
        {
            _gimbalRuntime.BeginSetAttitude(new GimbalCommand(PitchDegrees: -45, YawDegrees: 0, LockYaw: true));
            RefreshPayload();
            return Unit.Default;
        });
        RequestArmActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.Arm));
        RequestDisarmActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.Disarm));
        RequestTakeoffActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.Takeoff));
        RequestLandActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.Land));
        RequestReturnActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.ReturnToLaunch));
        RequestPauseActionCommand = ReactiveCommand.Create(() => RequestGuidedAction(GuidedActionKind.Pause));
        ConfirmGuidedActionCommand = ReactiveCommand.CreateFromTask(ConfirmGuidedActionAsync);
    }

    public string Title { get; } = "Fly";

    public FlyViewOperatorLayout OperatorLayout => new(
        VehicleSummary: VehicleText,
        ModeSummary: ModeText,
        ArmSummary: ArmText,
        LinkSummary: LinkDiagnosticText,
        GpsSummary: GpsText,
        BatterySummary: BatteryText,
        PositionSummary: PositionText,
        AttitudeSummary: AttitudeText,
        SpeedSummary: SpeedText,
        EstimatorSummary: EstimatorText,
        MapSummary: MapRuntimeText,
        MapProviderSummary: $"{MapProviderText} | {MapProviderHostText} | {MapFollowText}",
        WarningSummary: BuildWarningSummary(),
        PayloadSummary: $"{PayloadVideoText} | {PayloadCameraReadyText} | {PayloadGimbalAttitudeText}",
        HasActiveVehicle: HasActiveVehicle,
        HasWarning: HasWarning,
        HasPayloadActivity: HasPayloadActivity,
        IsMapFollowingVehicle: IsMapFollowingVehicle,
        IsEstimatorHealthy: ActiveVehicle?.EstimatorOk == true);

    public string VehicleText => ActiveVehicle is null
        ? "No active vehicle"
        : $"Vehicle {ActiveVehicle.Id} {ActiveVehicle.VehicleType}";

    public string ModeText => ActiveVehicle?.FlightModeName ?? "No mode";

    public string ArmText => ActiveVehicle is null
        ? "No vehicle"
        : (ActiveVehicle.BaseMode & SafetyArmed) == SafetyArmed
            ? "Armed"
            : "Disarmed";

    public bool IsCommunicationLost => ActiveVehicle?.IsCommunicationLost == true;

    public string MainStatusText
    {
        get
        {
            if (ActiveVehicle is null)
            {
                return "Disconnected - Click to manually connect";
            }

            if (IsCommunicationLost)
            {
                return "Comms Lost";
            }

            if ((ActiveVehicle.BaseMode & SafetyArmed) == SafetyArmed)
            {
                return ActiveVehicle.GroundSpeedMs is > 0.5 ? "Flying" : "Armed";
            }

            if (ActiveVehicle.EstimatorOk)
            {
                return HasWarning ? "Ready" : "Ready";
            }

            return "Not Ready";
        }
    }

    public string MainStatusColor => ActiveVehicle is null
        ? "#4a2c6d"
        : IsCommunicationLost
            ? "#fb4f45"
            : (ActiveVehicle.BaseMode & SafetyArmed) == SafetyArmed
                ? "#17b93e"
                : ActiveVehicle.EstimatorOk
                    ? (HasWarning ? "#f9d838" : "#17b93e")
                    : "#fb4f45";

    public IReadOnlyList<FlyToolbarIndicatorItem> ToolbarIndicators =>
    [
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Gps, "GPS", GpsText),
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Battery, "BAT", BatteryText),
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Rc, "RC", LinkDiagnosticText),
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Telemetry, "TEL", TelemetryText),
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Arm, "ARM", ArmText),
        new FlyToolbarIndicatorItem(IndicatorDrawerKind.Messages, "MSG", OperatorLayout.WarningSummary)
    ];

    public string MainStatusDetailText => $"{MainStatusText}\n{VehicleText}\nMode: {ModeText}\nArm: {ArmText}\nGPS: {GpsText}\nBattery: {BatteryText}\nLink: {LinkText}";

    public string GpsText => ActiveVehicle?.GpsFixType is { } fixType
        ? ActiveVehicle.SatelliteCount is { } satellites
            ? $"GPS fix {fixType} | {satellites} sats"
            : $"GPS fix {fixType}"
        : "No GPS";

    public string BatteryText
    {
        get
        {
            var vehicle = ActiveVehicle;
            if (vehicle is null)
            {
                return "No battery";
            }

            return vehicle.BatteryVoltage switch
            {
                { } voltage when vehicle.BatteryRemainingPercent is { } remaining => $"{voltage:F1} V | {remaining}%",
                { } voltage => $"{voltage:F1} V",
                _ when vehicle.BatteryRemainingPercent is { } remaining => $"{remaining}%",
                _ => "No battery"
            };
        }
    }

    public string LinkText
    {
        get
        {
            if (_linkManager.ActiveLink is { } active)
            {
                return active.IsConnected
                    ? $"Active link: {active.Configuration.Name}"
                    : $"Active link disconnected: {active.Configuration.Name}";
            }

            if (_linkManager.Links.Count == 0)
            {
                return "No links";
            }

            var connectedCount = _linkManager.Links.Count(static link => link.IsConnected);
            return connectedCount == 0
                ? "No connected links"
                : $"{connectedCount} connected link(s), no send-capable link";
        }
    }

    public string PositionText
    {
        get
        {
            var vehicle = ActiveVehicle;
            if (vehicle?.Coordinate is not { } coordinate)
            {
                return "No position";
            }

            var altitude = vehicle.RelativeAltitudeMeters is { } relativeAltitude
                ? $" | RelAlt {relativeAltitude:F1} m"
                : string.Empty;
            return $"{coordinate.Latitude:F6}, {coordinate.Longitude:F6}{altitude}";
        }
    }

    public string TelemetryText
    {
        get
        {
            var stats = _mavlinkProtocol.Statistics.Snapshot();
            return stats.TotalPacketsLost == 0
                ? $"MAVLink {stats.TotalPacketsReceived} packets"
                : $"MAVLink {stats.TotalPacketsReceived} packets | loss {stats.PacketLossPercent:F1}%";
        }
    }

    public string LinkDiagnosticText
    {
        get
        {
            var stats = _mavlinkProtocol.Statistics.Snapshot();
            var activeLinkName = _linkManager.ActiveLink?.Configuration.Name;
            var activeStats = activeLinkName is null
                ? stats.LinkStats.OrderByDescending(static s => s.LastSeenAt).FirstOrDefault()
                : stats.LinkStats.FirstOrDefault(s => s.LinkId == activeLinkName);
            var vehicle = ActiveVehicle;
            var packetLoss = activeStats is null ? stats.PacketLossPercent : activeStats.PacketLossPercent;
            var vehicleDrops = vehicle?.CommunicationDropRatePermille is { } dropRate
                ? $" | vehicle drop {dropRate / 10.0:F1}%"
                : string.Empty;
            var vehicleErrors = vehicle?.CommunicationErrors is { } errors
                ? $" | errors {errors}"
                : string.Empty;
            if (stats.TotalPacketsReceived == 0)
            {
                return LinkText;
            }

            return activeStats is null
                ? $"Packets {stats.TotalPacketsReceived} | loss {packetLoss:F1}%{vehicleDrops}{vehicleErrors}"
                : $"{activeStats.LinkId}: {activeStats.TotalPacketsReceived} packets | loss {packetLoss:F1}%{vehicleDrops}{vehicleErrors}";
        }
    }

    public bool HasActiveVehicle => ActiveVehicle is not null;

    public string MapPlaceholderText => MapRuntimeText;

    public string MapRuntimeText => MapDisplayFrame.HasActiveVehicle
        ? $"{MapProviderHostState.RuntimeLabel} | {MapDisplayFrame.ActiveVehicle?.Label}"
        : $"{MapProviderHostState.RuntimeLabel} | waiting for vehicle position";

    public string MapProviderText => MapDisplayFrame.ProviderName;

    public MapProviderHostState MapProviderHostState => _mapProviderHost.State;

    public string MapProviderHostText => MapProviderHostState.IsLocalFallback
        ? $"Host: {MapProviderHostState.RuntimeLabel}"
        : $"Host: provider {MapProviderHostState.ActiveProvider.DisplayName}";

    public string FlyInsetStatusText => $"PiP {MapFollowText} | Zoom {MapDisplayFrame.Viewport.ZoomLevel:F0}";

    public string FlyLayerStatusText => $"Map {MapProviderText} | Payload {(HasPayloadActivity ? "Active" : "Idle")}";

    public string FlyWidgetStatusText => $"Widgets {(HasActiveVehicle ? "Visible" : "Waiting")}";

    public string FlyVideoPlaceholderText => HasPayloadActivity ? PayloadVideoText : "Video inactive";

    public bool ShowVideoPlaceholder => true;

    public string FlyMapRuntimeText => MapRuntimeText;

    public string FlyMapProviderRuntimeText => MapProviderHostText;

    public string FlyPipRuntimeText => MapVehicleMarkerText;

    public string FlyTopRightRuntimeText => OperatorLayout.PayloadSummary;

    public string FlyBottomRightRuntimeText => OperatorLayout.WarningSummary;

    public string FlyHudRuntimeText => OperatorLayout.AttitudeSummary;

    public string FlyJoystickRuntimeText => VirtualJoystickVisible ? "Joystick visible" : "Joystick hidden";

    public string FlyPreflightRuntimeText => Preflight.Summary;

    public string FlyMissionRuntimeText => MissionCompleteText;

    public string FlyIndicatorRuntimeText => MainStatusText;

    public string FlyLinkRuntimeText => LinkDiagnosticText;

    public string FlyPayloadRuntimeText => PayloadVideoText;

    public string FlyMapOverlayRuntimeText => MapTrajectoryText;

    public string FlyProviderRuntimeText => MapProviderText;

    public string FlyVehicleRuntimeText => VehicleText;

    public string FlyModeRuntimeText => ModeText;

    public string FlyWarningRuntimeText => BuildWarningSummary();

    public string FlyEstimatorRuntimeText => EstimatorText;

    public string FlyPositionRuntimeText => PositionText;

    public string FlySpeedRuntimeText => SpeedText;

    public string FlyBatteryRuntimeText => BatteryText;

    public string FlyGpsRuntimeText => GpsText;

    public string FlyArmRuntimeText => ArmText;

    public string FlyTelemetryRuntimeText => TelemetryText;

    public string FlyStatusRuntimeText => MainStatusDetailText;

    public string FlyOverlayRuntimeText => OperatorLayout.MapSummary;

    public string FlyGuidedRuntimeText => PendingGuidedActionText;

    public string FlyCameraRuntimeText => PayloadCameraModeText;

    public string FlyGimbalRuntimeText => PayloadGimbalAttitudeText;

    public string FlyWidgetOverlayRuntimeText => OperatorLayout.EstimatorSummary;

    public string FlyRightPanelRuntimeText => OperatorLayout.PayloadSummary;

    public string FlyBottomPanelRuntimeText => OperatorLayout.PositionSummary;

    public string FlyToolStripRuntimeText => HasActiveVehicle ? "Tools ready" : "Vehicle required";

    public string FlyPipStatusText => FlyPipRuntimeText;

    public string FlyTelemetryPanelStatusText => FlyBottomPanelRuntimeText;

    public string FlyOverlayPanelStatusText => FlyTopRightRuntimeText;

    public string FlyMapHolderStatusText => FlyMapRuntimeText;

    public string FlyCustomLayerStatusText => FlyLayerStatusText;

    public string FlyWidgetLayerStatusText => FlyWidgetStatusText;

    public string FlyPipLayerStatusText => FlyPipStatusText;

    public string FlyToolStripStatusText => FlyToolStripRuntimeText;

    public string FlyTelemetryStatusText => FlyTelemetryPanelStatusText;

    public string FlyPayloadStatusText => FlyOverlayPanelStatusText;

    public string FlyGuidedStatusText => FlyGuidedRuntimeText;

    public string FlyInsetRuntimeText => FlyInsetStatusText;

    public string FlyMapHolderHintText => FlyMapOverlayRuntimeText;

    public string FlyWidgetHintText => FlyWidgetOverlayRuntimeText;

    public string FlyPayloadHintText => FlyPayloadRuntimeText;

    public string FlyTelemetryHintText => FlyTelemetryRuntimeText;

    public string FlyToolStripHintText => FlyToolStripRuntimeText;

    public string FlyPipHintText => FlyPipRuntimeText;

    public string FlyLayerHintText => FlyLayerStatusText;

    public string FlyStatusHintText => FlyStatusRuntimeText;

    public string FlyVideoHintText => FlyVideoPlaceholderText;

    public string FlyMapHintText => FlyMapRuntimeText;

    public string FlyIndicatorHintText => FlyIndicatorRuntimeText;

    public string FlyDrawerHintText => FlyInsetStatusText;

    public string FlyTopPanelHintText => FlyTopRightRuntimeText;

    public string FlyBottomPanelHintText => FlyBottomRightRuntimeText;

    public string FlyHudHintText => FlyHudRuntimeText;

    public string FlyWidgetLayerHintText => FlyWidgetLayerStatusText;

    public string FlyPayloadLayerHintText => FlyPayloadStatusText;

    public string FlyTelemetryLayerHintText => FlyTelemetryStatusText;

    public string FlyToolStripLayerHintText => FlyToolStripStatusText;

    public string FlyPipLayerHintStatusText => FlyPipLayerStatusText;

    public string FlyMapLayerHintText => FlyMapHolderStatusText;

    public string FlyGuidedLayerHintText => FlyGuidedStatusText;

    public string FlyInsetLayerHintText => FlyInsetRuntimeText;

    public string FlyDynamicLayoutHintText => FlyInsetStatusText;

    public string FlyTopRightLayoutHintText => FlyTopRightRuntimeText;

    public string FlyBottomRightLayoutHintText => FlyBottomRightRuntimeText;

    public string FlyToolLayoutHintText => FlyToolStripRuntimeText;

    public string FlyPipLayoutHintText => FlyPipRuntimeText;

    public string FlyHudLayoutHintText => FlyHudRuntimeText;

    public string FlyPayloadLayoutHintText => FlyPayloadRuntimeText;

    public string FlyWidgetLayoutHintText => FlyWidgetOverlayRuntimeText;

    public string FlyTelemetryLayoutHintText => FlyTelemetryRuntimeText;

    public string FlyStatusLayoutHintText => FlyStatusRuntimeText;

    public string FlyLayoutSummaryText => $"{FlyInsetStatusText} | {FlyLayerStatusText}";

    public string FlyLayoutDetailText => $"{FlyMapRuntimeText}\n{FlyProviderRuntimeText}\n{FlyPipRuntimeText}";

    public string FlyLayoutOverlayText => $"{FlyPayloadRuntimeText}\n{FlyTelemetryRuntimeText}";

    public string FlyLayoutWidgetText => FlyWidgetOverlayRuntimeText;

    public string FlyLayoutGuidedText => FlyGuidedRuntimeText;

    public string FlyLayoutToolText => FlyToolStripRuntimeText;

    public string FlyLayoutStatusText => FlyStatusRuntimeText;

    public string FlyLayoutPipText => FlyPipRuntimeText;

    public string FlyLayoutMapText => FlyMapRuntimeText;

    public string FlyLayoutVideoText => FlyVideoPlaceholderText;

    public string FlyLayoutTopRightText => FlyTopRightRuntimeText;

    public string FlyLayoutBottomRightText => FlyBottomRightRuntimeText;

    public string FlyLayoutCenterText => FlyHudRuntimeText;

    public string FlyLayoutInsetText => FlyInsetStatusText;

    public string FlyLayoutProviderText => FlyMapProviderRuntimeText;

    public string FlyLayoutOverlaySummaryText => FlyLayerStatusText;

    public string FlyLayoutTelemetrySummaryText => FlyTelemetryRuntimeText;

    public string FlyLayoutPayloadSummaryText => FlyPayloadRuntimeText;

    public string FlyLayoutWidgetSummaryText => FlyWidgetOverlayRuntimeText;

    public string FlyLayoutToolSummaryText => FlyToolStripRuntimeText;

    public string FlyLayoutIndicatorSummaryText => FlyIndicatorRuntimeText;

    public string FlyLayoutStatusSummaryText => FlyStatusRuntimeText;

    public string FlyLayoutMissionSummaryText => MissionCompleteText;

    public string FlyLayoutPreflightSummaryText => Preflight.Summary;

    public string FlyLayoutJoystickSummaryText => FlyJoystickRuntimeText;

    public string FlyLayoutWarningSummaryText => FlyWarningRuntimeText;

    public string FlyLayoutBatterySummaryText => FlyBatteryRuntimeText;

    public string FlyLayoutGpsSummaryText => FlyGpsRuntimeText;

    public string FlyLayoutArmSummaryText => FlyArmRuntimeText;

    public string FlyLayoutModeSummaryText => FlyModeRuntimeText;

    public string FlyLayoutVehicleSummaryText => FlyVehicleRuntimeText;

    public string FlyLayoutLinkSummaryText => FlyLinkRuntimeText;

    public string FlyLayoutMapSummaryText => FlyMapOverlayRuntimeText;

    public string FlyLayoutEstimatorSummaryText => FlyEstimatorRuntimeText;

    public string FlyLayoutPositionSummaryText => FlyPositionRuntimeText;

    public string FlyLayoutSpeedSummaryText => FlySpeedRuntimeText;

    public string FlyLayoutCameraSummaryText => FlyCameraRuntimeText;

    public string FlyLayoutGimbalSummaryText => FlyGimbalRuntimeText;
    public string MapAvailableProvidersText => $"Providers {MapProviderHostState.AvailableProviders.Count}";

    public string MapCenterText => $"{MapDisplayFrame.Viewport.Center.Latitude:F6}, {MapDisplayFrame.Viewport.Center.Longitude:F6}";

    public string MapZoomText => $"Zoom {MapDisplayFrame.Viewport.ZoomLevel:F0}";

    public bool HasMapVehicle => MapDisplayFrame.HasActiveVehicle;

    public bool HasMapHome => MapDisplayFrame.Home is not null;

    public bool HasMapTrajectory => MapDisplayFrame.Trajectory?.Points.Count > 1;

    public bool IsMapFollowingVehicle => _mapInteractionState.IsFollowingVehicle;

    public string MapFollowText => IsMapFollowingVehicle
        ? "Follow vehicle"
        : "Manual map";

    public string MapVehicleMarkerText => MapDisplayFrame.ActiveVehicle is { } marker
        ? $"{marker.Label} | {marker.Mode}"
        : "No vehicle position";

    public string MapHomeMarkerText => MapDisplayFrame.Home is { } home
        ? $"{home.Label} | {(_homeCoordinate?.Latitude ?? 0):F6}, {(_homeCoordinate?.Longitude ?? 0):F6}"
        : "No home position";

    public string MapTrajectoryText => MapDisplayFrame.Trajectory is { } trajectory
        ? $"Track {trajectory.Points.Count} points"
        : "Track 0 points";

    public MapOverlayFrame OverlayFrame => _overlayProjector.Build(ActiveVehicle, _homeCoordinate, _trajectory.ToArray());

    public MapDisplayFrame MapDisplayFrame => _mapProviderHost.RenderDisplayFrame(OverlayFrame, ResolveMapViewport());

    public VideoStreamRuntimeState VideoRuntimeState => _videoRuntime.State;

    public CameraRuntimeState CameraRuntimeState => _cameraRuntime.State;

    public GimbalRuntimeState GimbalRuntimeState => _gimbalRuntime.State;

    public string PayloadVideoText => VideoRuntimeState.StatusText;

    public string PayloadVideoStreamText => VideoRuntimeState.SelectedStream is { } stream
        ? $"{stream.Name} | {stream.Protocol}"
        : "No video stream";

    public string PayloadCameraReadyText => CameraRuntimeState.ReadyText;

    public string PayloadCameraModeText => CameraRuntimeState.ModeText;

    public string PayloadCameraCaptureText => CameraRuntimeState.CaptureText;

    public string PayloadCameraRecordingText => CameraRuntimeState.RecordingText;

    public string PayloadCameraStorageText => CameraRuntimeState.Storage.StatusText;

    public string PayloadGimbalAttitudeText => GimbalRuntimeState.AttitudeText;

    public string PayloadGimbalLockText => GimbalRuntimeState.LockText;

    public string PayloadGimbalTargetText => GimbalRuntimeState.TargetText;

    public string PayloadGimbalCommandText => GimbalRuntimeState.CommandText;

    public string AttitudeText
    {
        get
        {
            var v = ActiveVehicle;
            return v?.PitchDegrees is { } p && v.RollDegrees is { } r
                ? $"Pitch {p:F1}° Roll {r:F1}°"
                : v?.HeadingDegrees is { } h
                    ? $"Heading {h:F1}°"
                    : "No attitude";
        }
    }

    public string SpeedText
    {
        get
        {
            var v = ActiveVehicle;
            return v?.GroundSpeedMs is { } gs
                ? $"Gnd {gs:F1} m/s" + (v.AirspeedMs is { } air ? $" | Air {air:F1} m/s" : "")
                : "No speed";
        }
    }

    public string EstimatorText => ActiveVehicle is null
        ? "EKF —"
        : ActiveVehicle.EstimatorOk
            ? "EKF OK"
            : "EKF warning";

    public PreflightChecklist Preflight => _preflight;

    // Flight instrument data for graphical HUD
    public double ActivePitchDeg => ActiveVehicle?.PitchDegrees ?? 0;
    public double ActiveRollDeg => ActiveVehicle?.RollDegrees ?? 0;
    public double ActiveHeadingDeg => ActiveVehicle?.HeadingDegrees ?? 0;
    public double ActiveAltitudeM => ActiveVehicle?.RelativeAltitudeMeters ?? 0;
    public double ActiveSpeedMs => ActiveVehicle?.GroundSpeedMs ?? 0;

    public bool HasWarning => ActiveVehicle?.StatusMessages.Any(static message => message.Severity <= MavlinkSeverity.Warning) == true;

    public bool HasPayloadActivity =>
        VideoRuntimeState.Status != VideoStreamRuntimeStatus.Unavailable ||
        CameraRuntimeState.Status is not null ||
        GimbalRuntimeState.HasAttitude ||
        GimbalRuntimeState.Target is not null;

    public bool ShowMissionCompleteDialog
    {
        get => _showMissionCompleteDialog;
        private set => this.RaiseAndSetIfChanged(ref _showMissionCompleteDialog, value);
    }

    public string MissionCompleteText
    {
        get => _missionCompleteText;
        private set => this.RaiseAndSetIfChanged(ref _missionCompleteText, value);
    }

    public void DismissMissionComplete()
    {
        ShowMissionCompleteDialog = false;
        MissionCompleteText = string.Empty;
    }

    public IReadOnlyList<GuidedActionStatus> GuidedActions => _guidedActions.Capture(ActiveVehicle, _linkManager.ActiveLink);

    public string GuidedActionSummary
    {
        get
        {
            var active = GuidedActions.FirstOrDefault(static action => action.State is GuidedActionState.ConfirmationRequired or GuidedActionState.Pending);
            if (active is not null)
            {
                return active.StatusText;
            }

            var last = GuidedActions.LastOrDefault(static action => action.State is GuidedActionState.Accepted or GuidedActionState.Rejected or GuidedActionState.Timeout or GuidedActionState.Failed);
            return last?.StatusText ?? "Guided actions ready";
        }
    }

    public bool HasPendingGuidedAction => _guidedActions.PendingConfirmation is not null;

    public string PendingGuidedActionText => _guidedActions.PendingConfirmation is { } kind
        ? $"Confirm {kind}"
        : "No guided action pending confirmation";

    public ReactiveCommand<Unit, Unit> RecenterMapCommand { get; }

    public ReactiveCommand<Unit, Unit> CaptureImageCommand { get; }

    public ReactiveCommand<Unit, Unit> RecordVideoCommand { get; }

    public ReactiveCommand<Unit, Unit> TiltGimbalDownCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestArmActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestDisarmActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestTakeoffActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestLandActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestReturnActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> RequestPauseActionCommand { get; }

    public ReactiveCommand<Unit, GuidedActionStatus> ConfirmGuidedActionCommand { get; }

    // Virtual Joystick
    public VirtualJoystickRuntime VirtualJoystick => _virtualJoystick;

    public bool VirtualJoystickVisible
    {
        get => _virtualJoystickVisible;
        set
        {
            this.RaiseAndSetIfChanged(ref _virtualJoystickVisible, value);
            _virtualJoystick.Enabled = value;
            if (!value) _virtualJoystick.Reset();
            this.RaisePropertyChanged(nameof(VirtualJoystickStatusText));
        }
    }

    public string VirtualJoystickStatusText => _virtualJoystick.Enabled
        ? _virtualJoystick.ProjectManualControl().StatusText
        : "Virtual joystick disabled";

    public void UpdateVirtualJoystickLeft(double x, double y)
    {
        _virtualJoystick.UpdateLeftStick(x, y);
        this.RaisePropertyChanged(nameof(VirtualJoystickStatusText));
    }

    public void UpdateVirtualJoystickRight(double x, double y)
    {
        _virtualJoystick.UpdateRightStick(x, y);
        this.RaisePropertyChanged(nameof(VirtualJoystickStatusText));
    }

    public void ReleaseVirtualJoystickLeft()
    {
        _virtualJoystick.ReleaseLeftStick();
        this.RaisePropertyChanged(nameof(VirtualJoystickStatusText));
    }

    public void ReleaseVirtualJoystickRight()
    {
        _virtualJoystick.ReleaseRightStick();
        this.RaisePropertyChanged(nameof(VirtualJoystickStatusText));
    }

    public void ToggleVirtualJoystick()
    {
        VirtualJoystickVisible = !VirtualJoystickVisible;
    }

    // Proximity Radar
    public ProximityRadarRuntime ProximityRadar => _proximityRadar;

    public ProximityRadarSnapshot? ProximityRadarSnapshot
    {
        get => _proximityRadarSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _proximityRadarSnapshot, value);
    }

    // Custom Actions
    public CustomActionRuntime CustomActions => _customActions;

    public CustomActionSnapshot? CustomActionSnapshot
    {
        get => _customActionSnapshot;
        private set => this.RaiseAndSetIfChanged(ref _customActionSnapshot, value);
    }

    // Gimbal Touch Control
    public GimbalTouchControl GimbalTouchControl => _gimbalTouchControl;

    public GimbalTouchState? GimbalTouchState
    {
        get => _gimbalTouchState;
        private set => this.RaiseAndSetIfChanged(ref _gimbalTouchState, value);
    }

    public void BeginGimbalTouch(double x, double y)
    {
        GimbalTouchState = _gimbalTouchControl.BeginTouch(x, y);
    }

    public void MoveGimbalTouch(double x, double y)
    {
        GimbalTouchState = _gimbalTouchControl.MoveTouch(x, y);
    }

    public void EndGimbalTouch()
    {
        GimbalTouchState = _gimbalTouchControl.EndTouch();
    }

    public void MarkMapManuallyMoved(MapViewport viewport)
    {
        _mapInteractionState.MarkManualViewport(viewport);
        Refresh();
    }

    public void RecenterMap()
    {
        _mapInteractionState.RecenterOnVehicle();
        Refresh();
    }

    public GuidedActionStatus RequestGuidedAction(GuidedActionKind kind)
    {
        var status = _guidedActions.RequestConfirmation(kind, ActiveVehicle, _linkManager.ActiveLink);
        RefreshGuidedActions();
        return status;
    }

    public async Task<GuidedActionStatus> ConfirmGuidedActionAsync(CancellationToken cancellationToken = default)
    {
        var status = await _guidedActions.ConfirmAsync(ActiveVehicle, _linkManager.ActiveLink, cancellationToken).ConfigureAwait(false);
        RefreshGuidedActions();
        return status;
    }

    public IReadOnlyList<GuidedActionStatus> MarkGuidedActionTimeouts(TimeSpan? timeout = null, DateTimeOffset? now = null)
    {
        var statuses = _guidedActions.MarkTimeouts(timeout, now);
        RefreshGuidedActions();
        return statuses;
    }

    private Vehicle? ActiveVehicle => _multiVehicleManager.ActiveVehicle;

    private void Refresh()
    {
        DetectMissionComplete();
        CaptureMapHistory();
        this.RaisePropertyChanged(nameof(VehicleText));
        this.RaisePropertyChanged(nameof(ModeText));
        this.RaisePropertyChanged(nameof(ArmText));
        this.RaisePropertyChanged(nameof(IsCommunicationLost));
        this.RaisePropertyChanged(nameof(MainStatusText));
        this.RaisePropertyChanged(nameof(MainStatusColor));
        this.RaisePropertyChanged(nameof(MainStatusDetailText));
        this.RaisePropertyChanged(nameof(ToolbarIndicators));
        this.RaisePropertyChanged(nameof(GpsText));
        this.RaisePropertyChanged(nameof(BatteryText));
        this.RaisePropertyChanged(nameof(LinkText));
        this.RaisePropertyChanged(nameof(PositionText));
        this.RaisePropertyChanged(nameof(TelemetryText));
        this.RaisePropertyChanged(nameof(LinkDiagnosticText));
        this.RaisePropertyChanged(nameof(HasActiveVehicle));
        this.RaisePropertyChanged(nameof(OperatorLayout));
        this.RaisePropertyChanged(nameof(OverlayFrame));
        this.RaisePropertyChanged(nameof(MapDisplayFrame));
        this.RaisePropertyChanged(nameof(MapRuntimeText));
        this.RaisePropertyChanged(nameof(MapPlaceholderText));
        this.RaisePropertyChanged(nameof(MapProviderText));
        this.RaisePropertyChanged(nameof(MapProviderHostState));
        this.RaisePropertyChanged(nameof(MapProviderHostText));
        this.RaisePropertyChanged(nameof(MapAvailableProvidersText));
        this.RaisePropertyChanged(nameof(MapCenterText));
        this.RaisePropertyChanged(nameof(MapZoomText));
        this.RaisePropertyChanged(nameof(HasMapVehicle));
        this.RaisePropertyChanged(nameof(HasMapHome));
        this.RaisePropertyChanged(nameof(HasMapTrajectory));
        this.RaisePropertyChanged(nameof(IsMapFollowingVehicle));
        this.RaisePropertyChanged(nameof(MapFollowText));
        this.RaisePropertyChanged(nameof(MapVehicleMarkerText));
        this.RaisePropertyChanged(nameof(MapHomeMarkerText));
        this.RaisePropertyChanged(nameof(MapTrajectoryText));
        this.RaisePropertyChanged(nameof(ActivePitchDeg));
        this.RaisePropertyChanged(nameof(ActiveRollDeg));
        this.RaisePropertyChanged(nameof(ActiveHeadingDeg));
        this.RaisePropertyChanged(nameof(ActiveAltitudeM));
        this.RaisePropertyChanged(nameof(ActiveSpeedMs));
        RefreshPayload();
        RefreshGuidedActions();
        RefreshPreflight();
        ProximityRadarSnapshot = _proximityRadar.Snapshot;
        CustomActionSnapshot = _customActions.Snapshot;
        GimbalTouchState = _gimbalTouchControl.State;
    }

    private void RefreshGuidedActions()
    {
        this.RaisePropertyChanged(nameof(GuidedActions));
        this.RaisePropertyChanged(nameof(GuidedActionSummary));
        this.RaisePropertyChanged(nameof(HasPendingGuidedAction));
        this.RaisePropertyChanged(nameof(PendingGuidedActionText));
    }

    private void RefreshPreflight()
    {
        _preflight = ActiveVehicle is { } vehicle
            ? _preflightService.Evaluate(vehicle)
            : new PreflightChecklist("No vehicle", [], true, false, "Preflight: no vehicle");
        this.RaisePropertyChanged(nameof(Preflight));
    }

    private void RefreshPayload()
    {
        this.RaisePropertyChanged(nameof(VideoRuntimeState));
        this.RaisePropertyChanged(nameof(CameraRuntimeState));
        this.RaisePropertyChanged(nameof(GimbalRuntimeState));
        this.RaisePropertyChanged(nameof(PayloadVideoText));
        this.RaisePropertyChanged(nameof(PayloadVideoStreamText));
        this.RaisePropertyChanged(nameof(PayloadCameraReadyText));
        this.RaisePropertyChanged(nameof(PayloadCameraModeText));
        this.RaisePropertyChanged(nameof(PayloadCameraCaptureText));
        this.RaisePropertyChanged(nameof(PayloadCameraRecordingText));
        this.RaisePropertyChanged(nameof(PayloadCameraStorageText));
        this.RaisePropertyChanged(nameof(PayloadGimbalAttitudeText));
        this.RaisePropertyChanged(nameof(PayloadGimbalLockText));
        this.RaisePropertyChanged(nameof(PayloadGimbalTargetText));
        this.RaisePropertyChanged(nameof(PayloadGimbalCommandText));
        this.RaisePropertyChanged(nameof(AttitudeText));
        this.RaisePropertyChanged(nameof(SpeedText));
        this.RaisePropertyChanged(nameof(EstimatorText));
        this.RaisePropertyChanged(nameof(HasWarning));
        this.RaisePropertyChanged(nameof(HasPayloadActivity));
        this.RaisePropertyChanged(nameof(OperatorLayout));
    }

    private void DetectMissionComplete()
    {
        var vehicle = ActiveVehicle;
        if (vehicle is null)
        {
            _wasMissionActive = false;
            return;
        }

        var isArmed = (vehicle.BaseMode & SafetyArmed) == SafetyArmed;
        var modeName = vehicle.FlightModeName;
        var isMissionMode = modeName is "Auto" or "Mission";

        if (isArmed && isMissionMode)
        {
            _wasMissionActive = true;
        }
        else if (_wasMissionActive && isArmed && modeName is "Loiter" or "Land" or "RTL")
        {
            _wasMissionActive = false;
            MissionCompleteText = $"Mission completed. Vehicle switched to {modeName}.";
            ShowMissionCompleteDialog = true;
        }
        else if (!isArmed)
        {
            _wasMissionActive = false;
        }
    }

    private void CaptureMapHistory()
    {
        if (ActiveVehicle?.Coordinate is not { } coordinate)
        {
            return;
        }

        _homeCoordinate ??= coordinate;

        if (_trajectory.Count == 0 || _trajectory[^1] != coordinate)
        {
            _trajectory.Add(coordinate);
        }
    }

    private MapViewport ResolveMapViewport()
    {
        return _mapInteractionState.ResolveViewport(
            ActiveVehicle?.Coordinate is { } coordinate
                ? MapCoordinate.FromVehicleCoordinate(coordinate)
                : null);
    }

    private string BuildWarningSummary()
    {
        var vehicle = ActiveVehicle;
        if (vehicle is null)
        {
            return "No vehicle";
        }

        if (vehicle.IsCommunicationLost)
        {
            return "⚠ COMM LOST";
        }

        var warnings = vehicle.StatusMessages
            .Where(static message => message.Severity <= MavlinkSeverity.Warning)
            .ToArray();
        if (warnings.Length == 0)
        {
            return "No warnings";
        }

        var latest = warnings[^1];
        return warnings.Length == 1
            ? $"{latest.Severity}: {latest.Text}"
            : $"{warnings.Length} warnings | Latest {latest.Severity}: {latest.Text}";
    }
}
