using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using VGC.Comms;
using VGC.Maps;
using VGC.Mission;
using VGC.Vehicles;
using VGC.Views.Controls;

namespace VGC.ViewModels;

public enum PlanSection
{
    Mission,
    GeoFence,
    Rally
}

public enum PlanMapClickTool
{
    Waypoint,
    FencePolygon,
    FenceCircle,
    RallyPoint,
    MoveSelectedWaypoint
}

public enum PlanTransferVerb
{
    Upload,
    Download,
    Clear
}

public enum PlanTransferNotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}

public sealed record PlanTransferNotification(
    PlanSection Section,
    PlanTransferVerb Verb,
    PlanTransferNotificationSeverity Severity,
    string Message,
    DateTimeOffset CreatedAt);

public sealed record PlanWorkflowNode(
    string Id,
    string Label,
    PlanSection? Section,
    string Kind,
    int Count,
    bool IsActive,
    bool HasValidationIssue,
    bool IsComplexAuthoring,
    string Summary);

public sealed record PlanFileWorkflowState(
    bool CanCreateNew,
    bool CanImport,
    bool CanExport,
    bool CanSave,
    bool IsDirty,
    string StatusText,
    string ValidationSummary,
    string AndroidStorageRiskText);

public sealed record PlanMissionMapMarker(
    int Index,
    int DoJumpId,
    int Command,
    bool IsSelected,
    string MarkerText,
    string MarkerFill,
    string MarkerStroke,
    string Summary);

public sealed class PlanTransferToolbarCommand : ReactiveObject
{
    private bool _isEnabled;

    public PlanTransferToolbarCommand(
        PlanSection section,
        PlanTransferVerb verb,
        string label,
        string confirmationLabel,
        ReactiveCommand<Unit, Unit> command)
    {
        Section = section;
        Verb = verb;
        Label = label;
        ConfirmationLabel = confirmationLabel;
        Command = command;
    }

    public PlanSection Section { get; }

    public PlanTransferVerb Verb { get; }

    public string Label { get; }

    public string ConfirmationLabel { get; }

    public ReactiveCommand<Unit, Unit> Command { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }
}

public sealed class PlanViewModel : ViewModelBase
{
    private const int MaxTransferNotificationCount = 20;

    private enum PendingTransferKind
    {
        None,
        MissionUpload,
        MissionDownload,
        MissionClear,
        GeoFenceUpload,
        GeoFenceDownload,
        GeoFenceClear,
        RallyUpload,
        RallyDownload,
        RallyClear
    }

    private readonly MultiVehicleManager _multiVehicleManager;
    private readonly LinkManager? _linkManager;
    private readonly PlanTransferSupportPolicy _transferSupportPolicy = new();
    private readonly PlanImportExportService _planImportExportService = new();
    private readonly PlanMapOverlayBuilder _planMapOverlayBuilder = new();
    private readonly PlanMapDisplayProjector _planMapDisplayProjector = new();
    private readonly ObservableCollection<PlanTransferToolbarCommand> _planTransferCommands = new();
    private readonly ObservableCollection<PlanTransferNotification> _transferNotifications = new();
    private PlanDocument _document;
    private MissionPlanItem? _selectedItem;
    private PlanSection _activeSection = PlanSection.Mission;
    private PendingTransferKind _pendingTransferKind = PendingTransferKind.None;
    private Func<Task>? _pendingTransferAction;
    private string? _pendingTransferText;
    private string? _missionCommandError;
    private string? _geoFenceCommandError;
    private string? _rallyCommandError;
    private string _planImportExportStatusText = "Plan import/export ready";
    private bool _isPlanDirty;
    private PlanMapClickTool _activeMapClickTool = PlanMapClickTool.Waypoint;
    private MapViewport? _manualMapViewport;
    private bool _isRightPanelCollapsed;

    public PlanViewModel(MultiVehicleManager multiVehicleManager, LinkManager? linkManager = null)
    {
        _multiVehicleManager = multiVehicleManager;
        _linkManager = linkManager;
        _document = CreateDocumentSnapshot();
        _selectedItem = _document.Mission.Items.FirstOrDefault();
        _multiVehicleManager.VehiclesChanged += (_, _) =>
        {
            RefreshDocument();
            RaiseTransferAvailabilityProperties();
        };
        _multiVehicleManager.VehicleUpdated += (_, _) =>
        {
            RefreshDocument();
            RaiseTransferAvailabilityProperties();
        };
        if (_linkManager is not null)
        {
            _linkManager.LinksChanged += (_, _) => RaiseTransferAvailabilityProperties();
        }
        AddWaypointCommand = ReactiveCommand.Create(() => AddWithTool(PlanMapClickTool.Waypoint, AddWaypoint));
        RemoveSelectedItemCommand = ReactiveCommand.Create(() =>
        {
            RemoveSelectedItem();
            return Unit.Default;
        });
        MoveSelectedItemUpCommand = ReactiveCommand.Create(() =>
        {
            MoveSelectedItemUp();
            return Unit.Default;
        });
        MoveSelectedItemDownCommand = ReactiveCommand.Create(() =>
        {
            MoveSelectedItemDown();
            return Unit.Default;
        });
        ShowMissionSectionCommand = ReactiveCommand.Create(() => ShowPlanSection(PlanSection.Mission));
        ShowGeoFenceSectionCommand = ReactiveCommand.Create(() => ShowPlanSection(PlanSection.GeoFence));
        ShowRallySectionCommand = ReactiveCommand.Create(() => ShowPlanSection(PlanSection.Rally));
        AddGeoFencePolygonCommand = ReactiveCommand.Create(() => AddWithTool(PlanMapClickTool.FencePolygon, AddGeoFencePolygon));
        AddGeoFenceCircleCommand = ReactiveCommand.Create(() => AddWithTool(PlanMapClickTool.FenceCircle, AddGeoFenceCircle));
        RemoveLastGeoFencePolygonCommand = ReactiveCommand.Create(() =>
        {
            RemoveLastGeoFencePolygon();
            return Unit.Default;
        });
        RemoveLastGeoFenceCircleCommand = ReactiveCommand.Create(() =>
        {
            RemoveLastGeoFenceCircle();
            return Unit.Default;
        });
        AddRallyPointCommand = ReactiveCommand.Create(() => AddWithTool(PlanMapClickTool.RallyPoint, AddRallyPoint));
        RemoveLastRallyPointCommand = ReactiveCommand.Create(() =>
        {
            RemoveLastRallyPoint();
            return Unit.Default;
        });
        UploadMissionCommand = ReactiveCommand.Create(RequestMissionUpload, this.WhenAnyValue(static x => x.CanRequestMissionTransfer));
        DownloadMissionCommand = ReactiveCommand.Create(RequestMissionDownload, this.WhenAnyValue(static x => x.CanRequestMissionTransfer));
        ClearMissionCommand = ReactiveCommand.Create(RequestMissionClear, this.WhenAnyValue(static x => x.CanRequestMissionTransfer));
        UploadGeoFenceCommand = ReactiveCommand.Create(RequestGeoFenceUpload, this.WhenAnyValue(static x => x.CanRequestGeoFenceTransfer));
        DownloadGeoFenceCommand = ReactiveCommand.Create(RequestGeoFenceDownload, this.WhenAnyValue(static x => x.CanRequestGeoFenceTransfer));
        ClearGeoFenceCommand = ReactiveCommand.Create(RequestGeoFenceClear, this.WhenAnyValue(static x => x.CanRequestGeoFenceTransfer));
        UploadRallyCommand = ReactiveCommand.Create(RequestRallyUpload, this.WhenAnyValue(static x => x.CanRequestRallyTransfer));
        DownloadRallyCommand = ReactiveCommand.Create(RequestRallyDownload, this.WhenAnyValue(static x => x.CanRequestRallyTransfer));
        ClearRallyCommand = ReactiveCommand.Create(RequestRallyClear, this.WhenAnyValue(static x => x.CanRequestRallyTransfer));
        ConfirmPendingTransferCommand = ReactiveCommand.CreateFromTask(ConfirmPendingTransferAsync, this.WhenAnyValue(static x => x.HasPendingTransfer));
        CancelPendingTransferCommand = ReactiveCommand.Create(CancelPendingTransfer, this.WhenAnyValue(static x => x.HasPendingTransfer));
        SelectWaypointMapToolCommand = ReactiveCommand.Create(() => SelectMapClickTool(PlanMapClickTool.Waypoint));
        SelectFencePolygonMapToolCommand = ReactiveCommand.Create(() => SelectMapClickTool(PlanMapClickTool.FencePolygon));
        SelectFenceCircleMapToolCommand = ReactiveCommand.Create(() => SelectMapClickTool(PlanMapClickTool.FenceCircle));
        SelectRallyMapToolCommand = ReactiveCommand.Create(() => SelectMapClickTool(PlanMapClickTool.RallyPoint));
        SelectMoveWaypointMapToolCommand = ReactiveCommand.Create(() => SelectMapClickTool(PlanMapClickTool.MoveSelectedWaypoint));
        ToggleRightPanelCommand = ReactiveCommand.Create(() =>
        {
            ToggleRightPanel();
            return Unit.Default;
        });
        InitializePlanTransferCommands();
        UpdatePlanTransferCommandState();
    }

    public string Title { get; } = "Plan";

    public string EmptyStateText => "No mission loaded. Plan View will host waypoint editing and mission transfer status.";

    public PlanDocument Document
    {
        get => _document;
        private set => this.RaiseAndSetIfChanged(ref _document, value);
    }

    public string MissionSummary => BuildMissionSummary(Document);

    public string MissionStats => BuildMissionStats(Document);

    public PlanMapDisplayFrame PlanMapDisplayFrame => ProjectPlanMapDisplayFrame();

    public MapGeoPoint PlanMapCenter => new(PlanMapDisplayFrame.Viewport.Center.Latitude, PlanMapDisplayFrame.Viewport.Center.Longitude, PlanMapDisplayFrame.Viewport.Center.AltitudeMeters ?? 0);

    public double PlanMapZoomLevel => PlanMapDisplayFrame.Viewport.ZoomLevel;

    public double PlanMapScaleLatitude => PlanMapDisplayFrame.Viewport.Center.Latitude;

    public IReadOnlyList<MissionMarker> PlanMapWaypoints => Document.Mission.Items
        .Where(static item => item.Params.Length >= 7)
        .Select((item, index) => new MissionMarker(item.DoJumpId, new MapGeoPoint(item.Params[4], item.Params[5], item.Params[6]), item.Command.ToString(), index == SelectedIndex))
        .ToArray();

    public IReadOnlyList<MapGeoPoint> PlanMapMissionPath => PlanMapWaypoints.Select(static marker => marker.Position).ToArray();

    public IReadOnlyList<MapPolygonOverlay> PlanMapPolygons => Document.GeoFence.Polygons
        .Where(static polygon => polygon.Polygon.Count >= 3)
        .Select(static polygon => new MapPolygonOverlay(
            polygon.Polygon.Select(static point => new MapGeoPoint(point.Latitude, point.Longitude, point.Altitude ?? 0)).ToArray(),
            true,
            Avalonia.Media.Color.Parse("#48d6ff"),
            Avalonia.Media.Color.FromArgb(45, 72, 214, 255)))
        .ToArray();

    public IReadOnlyList<MapCircleOverlay> PlanMapCircles => Document.GeoFence.Circles
        .Where(static circle => circle.Circle?.Center is not null)
        .Select(static circle => new MapCircleOverlay(
            new MapGeoPoint(circle.Circle!.Center.Latitude, circle.Circle.Center.Longitude, circle.Circle.Center.Altitude ?? 0),
            circle.Circle.Radius,
            true,
            Avalonia.Media.Color.Parse("#48d6ff"),
            Avalonia.Media.Color.FromArgb(35, 72, 214, 255)))
        .ToArray();

    public IReadOnlyList<RallyPointMarker> PlanMapRallyPoints => Document.RallyPoints.Points
        .Select((point, index) => new RallyPointMarker(new MapGeoPoint(point.Latitude, point.Longitude, point.Altitude ?? 0), $"R{index + 1}"))
        .ToArray();

    public string PlanMapPreviewSummary => BuildPlanMapPreviewSummary(PlanMapDisplayFrame);

    public PlanSection ActiveSection
    {
        get => _activeSection;
        private set
        {
            if (_activeSection == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _activeSection, value);
            this.RaisePropertyChanged(nameof(IsMissionSectionActive));
            this.RaisePropertyChanged(nameof(IsGeoFenceSectionActive));
            this.RaisePropertyChanged(nameof(IsRallySectionActive));
            RaisePlanWorkflowProperties();
        }
    }

    public bool IsMissionSectionActive => ActiveSection == PlanSection.Mission;

    public bool IsGeoFenceSectionActive => ActiveSection == PlanSection.GeoFence;

    public bool IsRallySectionActive => ActiveSection == PlanSection.Rally;

    public string GeoFenceSummary => BuildGeoFenceSummary(Document.GeoFence);

    public string RallySummary => BuildRallySummary(Document.RallyPoints);

    public string GeoFenceValidationText => BuildGeoFenceValidationText(Document.GeoFence);

    public string RallyValidationText => BuildRallyValidationText(Document.RallyPoints);

    public string PlanImportExportStatusText => _planImportExportStatusText;

    public IReadOnlyList<PlanWorkflowNode> PlanWorkflowNodes => BuildPlanWorkflowNodes();

    public PlanFileWorkflowState FileWorkflowState => BuildFileWorkflowState();

    public bool IsPlanDirty => _isPlanDirty;

    public PlanMapClickTool ActiveMapClickTool
    {
        get => _activeMapClickTool;
        private set
        {
            if (_activeMapClickTool == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _activeMapClickTool, value);
            this.RaisePropertyChanged(nameof(PlanMapClickModeText));
            this.RaisePropertyChanged(nameof(PlanMapDisplayFrame));
        }
    }

    public string PlanMapClickModeText => ActiveMapClickTool switch
    {
        PlanMapClickTool.Waypoint => "Map click: add waypoint",
        PlanMapClickTool.FencePolygon => "Map click: append fence polygon vertex",
        PlanMapClickTool.FenceCircle => "Map click: add fence circle",
        PlanMapClickTool.RallyPoint => "Map click: add rally point",
        PlanMapClickTool.MoveSelectedWaypoint => "Map click: move selected waypoint",
        _ => "Map click ready"
    };

    public string SelectedItemSummary => BuildSelectedItemSummary(SelectedItem, SelectedIndex);

    public string SelectedItemCoordinateText => BuildSelectedItemCoordinateText(SelectedItem);

    public string VehicleMissionState => BuildVehicleMissionState(_multiVehicleManager.ActiveVehicle);

    public string MissionTransferSummary => BuildMissionTransferSummary(_multiVehicleManager.ActiveVehicle);

    public double MissionTransferProgressPercent => GetMissionTransferProgressPercent(_multiVehicleManager.ActiveVehicle);

    public string MissionTransferErrorText => BuildMissionTransferErrorText(_multiVehicleManager.ActiveVehicle, _missionCommandError);

    public string GeoFenceTransferSummary => BuildGeoFenceTransferSummary(_multiVehicleManager.ActiveVehicle);

    public double GeoFenceTransferProgressPercent => GetGeoFenceTransferProgressPercent(_multiVehicleManager.ActiveVehicle);

    public string GeoFenceTransferErrorText => BuildGeoFenceTransferErrorText(_multiVehicleManager.ActiveVehicle, _geoFenceCommandError);

    public string RallyTransferSummary => BuildRallyTransferSummary(_multiVehicleManager.ActiveVehicle);

    public double RallyTransferProgressPercent => GetRallyTransferProgressPercent(_multiVehicleManager.ActiveVehicle);

    public string RallyTransferErrorText => BuildRallyTransferErrorText(_multiVehicleManager.ActiveVehicle, _rallyCommandError);

    public string PlanTransferLinkText => BuildPlanTransferLinkText(GetActiveTransferLink());

    public bool CanRequestMissionTransfer => IsMissionTransferAvailable();

    public bool CanRequestGeoFenceTransfer => IsGeoFenceTransferAvailable();

    public bool CanRequestRallyTransfer => IsRallyTransferAvailable();

    public bool HasPendingTransfer => _pendingTransferKind != PendingTransferKind.None;

    public string PendingTransferText => _pendingTransferText ?? "No pending transfer";

    public IReadOnlyList<MissionPlanItem> MissionItems => Document.Mission.Items;

    public IReadOnlyList<PlanMissionMapMarker> MissionMapMarkers => BuildMissionMapMarkers();

    public IReadOnlyList<GeoFencePolygon> GeoFencePolygons => Document.GeoFence.Polygons;

    public IReadOnlyList<GeoFenceCircle> GeoFenceCircles => Document.GeoFence.Circles;

    public IReadOnlyList<PlanCoordinate> RallyPoints => Document.RallyPoints.Points;

    public IReadOnlyList<PlanTransferToolbarCommand> PlanTransferCommands => _planTransferCommands;

    public IReadOnlyList<PlanTransferNotification> TransferNotifications => _transferNotifications;

    public string LatestTransferNotificationText => _transferNotifications.Count == 0
        ? "No transfer notifications"
        : _transferNotifications[0].Message;

    public MissionPlanItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            _selectedItem = value;
            this.RaisePropertyChanged();
            RaiseSelectedItemProperties();
        }
    }

    public int SelectedCommand
    {
        get => SelectedItem?.Command ?? 16;
        set
        {
            if (SelectedItem is null || SelectedItem.Command == value)
            {
                return;
            }

            SelectedItem.Command = value;
            MarkPlanDirty();
            RaiseSelectedItemProperties();
            RaiseMissionProperties();
        }
    }

    public int SelectedFrame
    {
        get => SelectedItem?.Frame ?? 3;
        set
        {
            if (SelectedItem is null || SelectedItem.Frame == value)
            {
                return;
            }

            SelectedItem.Frame = value;
            MarkPlanDirty();
            RaiseSelectedItemProperties();
            RaiseMissionProperties();
        }
    }

    public double SelectedLatitude
    {
        get => GetSelectedParam(4);
        set => SetSelectedParam(4, value);
    }

    public double SelectedLongitude
    {
        get => GetSelectedParam(5);
        set => SetSelectedParam(5, value);
    }

    public double SelectedAltitude
    {
        get => GetSelectedParam(6);
        set => SetSelectedParam(6, value);
    }

    public int SelectedIndex => SelectedItem is null ? -1 : Document.Mission.Items.IndexOf(SelectedItem);

    public bool HasSelectedItem => SelectedItem is not null;

    public ReadOnlyObservableCollection<Vehicle> Vehicles => _multiVehicleManager.Vehicles;

    public ReactiveCommand<Unit, Unit> AddWaypointCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveSelectedItemCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveSelectedItemUpCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveSelectedItemDownCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowMissionSectionCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowGeoFenceSectionCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowRallySectionCommand { get; }

    public ReactiveCommand<Unit, Unit> AddGeoFencePolygonCommand { get; }

    public ReactiveCommand<Unit, Unit> AddGeoFenceCircleCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveLastGeoFencePolygonCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveLastGeoFenceCircleCommand { get; }

    public ReactiveCommand<Unit, Unit> AddRallyPointCommand { get; }

    public ReactiveCommand<Unit, Unit> RemoveLastRallyPointCommand { get; }

    public ReactiveCommand<Unit, Unit> UploadMissionCommand { get; }

    public ReactiveCommand<Unit, Unit> DownloadMissionCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearMissionCommand { get; }

    public ReactiveCommand<Unit, Unit> UploadGeoFenceCommand { get; }

    public ReactiveCommand<Unit, Unit> DownloadGeoFenceCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearGeoFenceCommand { get; }

    public ReactiveCommand<Unit, Unit> UploadRallyCommand { get; }

    public ReactiveCommand<Unit, Unit> DownloadRallyCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearRallyCommand { get; }

    public ReactiveCommand<Unit, Unit> ConfirmPendingTransferCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelPendingTransferCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectWaypointMapToolCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectFencePolygonMapToolCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectFenceCircleMapToolCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectRallyMapToolCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectMoveWaypointMapToolCommand { get; }

    public ReactiveCommand<Unit, Unit> ToggleRightPanelCommand { get; }

    public bool IsRightPanelCollapsed
    {
        get => _isRightPanelCollapsed;
        set => this.RaiseAndSetIfChanged(ref _isRightPanelCollapsed, value);
    }

    public void ToggleRightPanel()
    {
        IsRightPanelCollapsed = !IsRightPanelCollapsed;
    }

    public MissionPlanItem AddWaypoint()
    {
        var item = CreateWaypointItem(Document.Mission.Items.Count + 1);
        Document.Mission.Items.Add(item);
        NormalizeMissionItemOrder();
        SelectedItem = item;
        MarkPlanDirty();
        RaiseMissionProperties();
        return item;
    }

    public MissionPlanItem AddWaypointAt(PlanCoordinate coordinate)
    {
        var item = AddWaypoint();
        UpdateSelectedWaypoint(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude ?? 50);
        item.Coordinate = [coordinate.Latitude, coordinate.Longitude, coordinate.Altitude ?? 50];
        RaiseSelectedItemProperties();
        RaiseMissionProperties();
        return item;
    }

    public PlanCoordinate ApplyMapClick(double normalizedX, double normalizedY)
    {
        return ApplyMapClick(ProjectMapClickCoordinate(normalizedX, normalizedY));
    }

    public PlanCoordinate ApplyMapClick(PlanCoordinate coordinate)
    {
        switch (ActiveMapClickTool)
        {
            case PlanMapClickTool.Waypoint:
                ActiveSection = PlanSection.Mission;
                AddWaypointAt(coordinate);
                break;
            case PlanMapClickTool.FencePolygon:
                ActiveSection = PlanSection.GeoFence;
                AddFencePolygonVertex(coordinate);
                break;
            case PlanMapClickTool.FenceCircle:
                ActiveSection = PlanSection.GeoFence;
                AddGeoFenceCircleAt(coordinate);
                break;
            case PlanMapClickTool.RallyPoint:
                ActiveSection = PlanSection.Rally;
                AddRallyPointAt(coordinate);
                break;
            case PlanMapClickTool.MoveSelectedWaypoint:
                ActiveSection = PlanSection.Mission;
                MoveSelectedWaypointTo(coordinate);
                break;
        }

        return coordinate;
    }

    public void MarkMapManuallyMoved(MapViewport viewport)
    {
        _manualMapViewport = viewport;
        RaisePlanSectionProperties();
    }

    public void RemoveSelectedItem()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var index = Document.Mission.Items.IndexOf(SelectedItem);
        if (index < 0)
        {
            return;
        }

        Document.Mission.Items.RemoveAt(index);
        NormalizeMissionItemOrder();
        SelectedItem = Document.Mission.Items.Count == 0
            ? null
            : Document.Mission.Items[Math.Min(index, Document.Mission.Items.Count - 1)];
        MarkPlanDirty();
        RaiseMissionProperties();
    }

    public void MoveSelectedItemUp()
    {
        MoveSelectedItem(-1);
    }

    public void MoveSelectedItemDown()
    {
        MoveSelectedItem(1);
    }

    public void UpdateSelectedWaypoint(double latitude, double longitude, double altitude)
    {
        if (SelectedItem is null)
        {
            return;
        }

        EnsureSelectedParams();
        SelectedItem.Params[4] = latitude;
        SelectedItem.Params[5] = longitude;
        SelectedItem.Params[6] = altitude;
        MarkPlanDirty();
        RaiseSelectedItemProperties();
        RaiseMissionProperties();
    }

    public void MoveSelectedWaypointTo(PlanCoordinate coordinate)
    {
        if (SelectedItem is null)
        {
            return;
        }

        var altitude = coordinate.Altitude ?? SelectedAltitude;
        UpdateSelectedWaypoint(coordinate.Latitude, coordinate.Longitude, altitude);
        SelectedItem.Coordinate = [coordinate.Latitude, coordinate.Longitude, altitude];
        RaiseSelectedItemProperties();
        RaiseMissionProperties();
    }

    public PlanDocument NewPlan()
    {
        Document = PlanDocument.CreateBlank();
        Document.Mission.Items.Add(CreateWaypointItem(1));
        SelectedItem = Document.Mission.Items.FirstOrDefault();
        SetPlanImportExportStatus("New plan ready");
        SetPlanDirty(false);
        RaiseMissionProperties();
        RaisePlanSectionProperties();
        return Document;
    }

    public GeoFencePolygon AddGeoFencePolygon()
    {
        var polygon = new GeoFencePolygon
        {
            Polygon =
            [
                new PlanCoordinate(47.397742, 8.545594),
                new PlanCoordinate(47.398742, 8.546594),
                new PlanCoordinate(47.396742, 8.546594)
            ]
        };
        Document.GeoFence.Polygons.Add(polygon);
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return polygon;
    }

    public GeoFencePolygon AddFencePolygonVertex(PlanCoordinate coordinate)
    {
        var polygon = Document.GeoFence.Polygons.LastOrDefault();
        if (polygon is null)
        {
            polygon = new GeoFencePolygon();
            Document.GeoFence.Polygons.Add(polygon);
        }

        polygon.Polygon.Add(new PlanCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude));
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return polygon;
    }

    public GeoFenceCircle AddGeoFenceCircle()
    {
        var circle = new GeoFenceCircle
        {
            Circle = new GeoFenceCircleShape
            {
                Center = new PlanCoordinate(47.397742, 8.545594),
                Radius = 100
            }
        };
        Document.GeoFence.Circles.Add(circle);
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return circle;
    }

    public GeoFenceCircle AddGeoFenceCircleAt(PlanCoordinate coordinate)
    {
        var circle = new GeoFenceCircle
        {
            Circle = new GeoFenceCircleShape
            {
                Center = new PlanCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude),
                Radius = 100
            }
        };
        Document.GeoFence.Circles.Add(circle);
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return circle;
    }

    public void RemoveLastGeoFencePolygon()
    {
        if (Document.GeoFence.Polygons.Count == 0)
        {
            return;
        }

        Document.GeoFence.Polygons.RemoveAt(Document.GeoFence.Polygons.Count - 1);
        MarkPlanDirty();
        RaisePlanSectionProperties();
    }

    public void RemoveLastGeoFenceCircle()
    {
        if (Document.GeoFence.Circles.Count == 0)
        {
            return;
        }

        Document.GeoFence.Circles.RemoveAt(Document.GeoFence.Circles.Count - 1);
        MarkPlanDirty();
        RaisePlanSectionProperties();
    }

    public PlanCoordinate AddRallyPoint()
    {
        var point = new PlanCoordinate(47.397742, 8.545594, 50);
        Document.RallyPoints.Points.Add(point);
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return point;
    }

    public PlanCoordinate AddRallyPointAt(PlanCoordinate coordinate)
    {
        var point = new PlanCoordinate(coordinate.Latitude, coordinate.Longitude, coordinate.Altitude ?? 50);
        Document.RallyPoints.Points.Add(point);
        MarkPlanDirty();
        RaisePlanSectionProperties();
        return point;
    }

    public void RemoveLastRallyPoint()
    {
        if (Document.RallyPoints.Points.Count == 0)
        {
            return;
        }

        Document.RallyPoints.Points.RemoveAt(Document.RallyPoints.Points.Count - 1);
        MarkPlanDirty();
        RaisePlanSectionProperties();
    }

    public void RequestMissionUpload()
    {
        SetPendingTransfer(PendingTransferKind.MissionUpload, "Mission upload", UploadMissionAsync);
    }

    public void RequestMissionDownload()
    {
        SetPendingTransfer(PendingTransferKind.MissionDownload, "Mission download", DownloadMissionAsync);
    }

    public void RequestMissionClear()
    {
        SetPendingTransfer(PendingTransferKind.MissionClear, "Mission clear", ClearMissionAsync);
    }

    public void RequestGeoFenceUpload()
    {
        SetPendingTransfer(PendingTransferKind.GeoFenceUpload, "GeoFence upload", UploadGeoFenceAsync);
    }

    public void RequestGeoFenceDownload()
    {
        SetPendingTransfer(PendingTransferKind.GeoFenceDownload, "GeoFence download", DownloadGeoFenceAsync);
    }

    public void RequestGeoFenceClear()
    {
        SetPendingTransfer(PendingTransferKind.GeoFenceClear, "GeoFence clear", ClearGeoFenceAsync);
    }

    public void RequestRallyUpload()
    {
        SetPendingTransfer(PendingTransferKind.RallyUpload, "Rally upload", UploadRallyAsync);
    }

    public void RequestRallyDownload()
    {
        SetPendingTransfer(PendingTransferKind.RallyDownload, "Rally download", DownloadRallyAsync);
    }

    public void RequestRallyClear()
    {
        SetPendingTransfer(PendingTransferKind.RallyClear, "Rally clear", ClearRallyAsync);
    }

    public async Task ConfirmPendingTransferAsync()
    {
        var action = _pendingTransferAction;
        if (action is null)
        {
            return;
        }

        var pendingKind = _pendingTransferKind;
        ClearPendingTransfer();
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var (section, verb, label) = DescribeTransfer(pendingKind);
            AddTransferNotification(section, verb, PlanTransferNotificationSeverity.Error, $"{label} failed: {ex.Message}");
            throw;
        }
    }

    public void CancelPendingTransfer()
    {
        if (HasPendingTransfer)
        {
            var (section, verb, label) = DescribeTransfer(_pendingTransferKind);
            AddTransferNotification(
                section,
                verb,
                PlanTransferNotificationSeverity.Warning,
                $"{label} canceled locally. No MAVLink transfer was sent.");
        }

        ClearPendingTransfer();
    }

    public async Task UploadMissionAsync()
    {
        if (!TryPrepareMissionTransfer(out var vehicle, out var error))
        {
            SetMissionCommandError(error);
            AddTransferNotification(PlanSection.Mission, PlanTransferVerb.Upload, PlanTransferNotificationSeverity.Error, $"Mission upload failed: {error}");
            return;
        }

        var items = Document.Mission.Items
            .Select((item, index) => MissionItemConverter.ToMavlinkMissionItemInt(
                item,
                vehicle.Id,
                vehicle.ComponentId,
                checked((ushort)index),
                index == 0 ? (byte)1 : (byte)0))
            .ToArray();

        _missionCommandError = null;
        var action = await vehicle.MissionTransferService!.BeginWriteAsync(items).ConfigureAwait(false);
        AddActionNotification(PlanSection.Mission, PlanTransferVerb.Upload, "Mission upload", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task DownloadMissionAsync()
    {
        if (!TryPrepareMissionTransfer(out var vehicle, out var error))
        {
            SetMissionCommandError(error);
            AddTransferNotification(PlanSection.Mission, PlanTransferVerb.Download, PlanTransferNotificationSeverity.Error, $"Mission download failed: {error}");
            return;
        }

        _missionCommandError = null;
        var action = await vehicle.MissionTransferService!.BeginReadAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.Mission, PlanTransferVerb.Download, "Mission download", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task ClearMissionAsync()
    {
        if (!TryPrepareMissionTransfer(out var vehicle, out var error))
        {
            SetMissionCommandError(error);
            AddTransferNotification(PlanSection.Mission, PlanTransferVerb.Clear, PlanTransferNotificationSeverity.Error, $"Mission clear failed: {error}");
            return;
        }

        _missionCommandError = null;
        var action = await vehicle.MissionTransferService!.BeginClearAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.Mission, PlanTransferVerb.Clear, "Mission clear", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task UploadGeoFenceAsync()
    {
        if (!TryPrepareGeoFenceTransfer(out var vehicle, out var error))
        {
            SetGeoFenceCommandError(error);
            AddTransferNotification(PlanSection.GeoFence, PlanTransferVerb.Upload, PlanTransferNotificationSeverity.Error, $"GeoFence upload failed: {error}");
            return;
        }

        _geoFenceCommandError = null;
        var action = await vehicle.GeoFenceTransferService!.BeginWriteAsync(Document.GeoFence).ConfigureAwait(false);
        AddActionNotification(PlanSection.GeoFence, PlanTransferVerb.Upload, "GeoFence upload", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task DownloadGeoFenceAsync()
    {
        if (!TryPrepareGeoFenceTransfer(out var vehicle, out var error))
        {
            SetGeoFenceCommandError(error);
            AddTransferNotification(PlanSection.GeoFence, PlanTransferVerb.Download, PlanTransferNotificationSeverity.Error, $"GeoFence download failed: {error}");
            return;
        }

        _geoFenceCommandError = null;
        var action = await vehicle.GeoFenceTransferService!.BeginReadAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.GeoFence, PlanTransferVerb.Download, "GeoFence download", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task ClearGeoFenceAsync()
    {
        if (!TryPrepareGeoFenceTransfer(out var vehicle, out var error))
        {
            SetGeoFenceCommandError(error);
            AddTransferNotification(PlanSection.GeoFence, PlanTransferVerb.Clear, PlanTransferNotificationSeverity.Error, $"GeoFence clear failed: {error}");
            return;
        }

        _geoFenceCommandError = null;
        var action = await vehicle.GeoFenceTransferService!.BeginClearAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.GeoFence, PlanTransferVerb.Clear, "GeoFence clear", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task UploadRallyAsync()
    {
        if (!TryPrepareRallyTransfer(out var vehicle, out var error))
        {
            SetRallyCommandError(error);
            AddTransferNotification(PlanSection.Rally, PlanTransferVerb.Upload, PlanTransferNotificationSeverity.Error, $"Rally upload failed: {error}");
            return;
        }

        _rallyCommandError = null;
        var action = await vehicle.RallyPointTransferService!.BeginWriteAsync(Document.RallyPoints).ConfigureAwait(false);
        AddActionNotification(PlanSection.Rally, PlanTransferVerb.Upload, "Rally upload", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task DownloadRallyAsync()
    {
        if (!TryPrepareRallyTransfer(out var vehicle, out var error))
        {
            SetRallyCommandError(error);
            AddTransferNotification(PlanSection.Rally, PlanTransferVerb.Download, PlanTransferNotificationSeverity.Error, $"Rally download failed: {error}");
            return;
        }

        _rallyCommandError = null;
        var action = await vehicle.RallyPointTransferService!.BeginReadAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.Rally, PlanTransferVerb.Download, "Rally download", action);
        RaisePlanSectionTransferProperties();
    }

    public async Task ClearRallyAsync()
    {
        if (!TryPrepareRallyTransfer(out var vehicle, out var error))
        {
            SetRallyCommandError(error);
            AddTransferNotification(PlanSection.Rally, PlanTransferVerb.Clear, PlanTransferNotificationSeverity.Error, $"Rally clear failed: {error}");
            return;
        }

        _rallyCommandError = null;
        var action = await vehicle.RallyPointTransferService!.BeginClearAsync().ConfigureAwait(false);
        AddActionNotification(PlanSection.Rally, PlanTransferVerb.Clear, "Rally clear", action);
        RaisePlanSectionTransferProperties();
    }

    public PlanImportExportResult ImportPlanJson(string json)
    {
        var result = _planImportExportService.Import(json);
        if (result.IsSuccess && result.Document is { } document)
        {
            Document = document;
            SelectedItem = Document.Mission.Items.FirstOrDefault();
            SetPlanImportExportStatus("Plan import complete");
            SetPlanDirty(false);
            RaiseMissionProperties();
            RaisePlanSectionProperties();
            return result;
        }

        SetPlanImportExportStatus($"Plan import failed: {FormatPlanIssues(result.Issues)}");
        return result;
    }

    public PlanImportExportResult ExportPlanJson()
    {
        var result = _planImportExportService.Export(Document);
        SetPlanImportExportStatus(result.IsSuccess
            ? "Plan export complete"
            : $"Plan export failed: {FormatPlanIssues(result.Issues)}");
        RaisePlanWorkflowProperties();
        return result;
    }

    public PlanImportExportResult SavePlanJson()
    {
        var result = _planImportExportService.Export(Document);
        if (result.IsSuccess)
        {
            SetPlanImportExportStatus("Plan save complete");
            SetPlanDirty(false);
        }
        else
        {
            SetPlanImportExportStatus($"Plan save failed: {FormatPlanIssues(result.Issues)}");
            RaisePlanWorkflowProperties();
        }

        return result;
    }

    public PlanMapDisplayFrame ProjectPlanMapDisplayFrame()
    {
        var coordinator = new PlanSectionCoordinator(Document);
        var overlay = _planMapOverlayBuilder.Build(coordinator);
        return _planMapDisplayProjector.Project(overlay, _manualMapViewport);
    }

    private void RefreshDocument()
    {
        Document = CreateDocumentSnapshot();
        SelectedItem = Document.Mission.Items.FirstOrDefault();
        RaiseMissionProperties();
        this.RaisePropertyChanged(nameof(PlanMapDisplayFrame));
    }

    private Unit ShowPlanSection(PlanSection section)
    {
        ActiveSection = section;
        return Unit.Default;
    }

    private Unit SelectMapClickTool(PlanMapClickTool tool)
    {
        ActiveMapClickTool = tool;
        return Unit.Default;
    }

    private Unit AddWithTool<T>(PlanMapClickTool tool, Func<T> add)
    {
        SelectMapClickTool(tool);
        add();
        return Unit.Default;
    }

    private Unit SetActiveMapClickTool(PlanMapClickTool tool)
    {
        return SelectMapClickTool(tool);
    }

    private static PlanCoordinate ProjectMapClickCoordinate(double normalizedX, double normalizedY)
    {
        var x = Math.Clamp(normalizedX, 0, 1);
        var y = Math.Clamp(normalizedY, 0, 1);
        const double centerLatitude = 47.397742;
        const double centerLongitude = 8.545594;
        const double spanDegrees = 0.02;
        return new PlanCoordinate(
            centerLatitude + (0.5 - y) * spanDegrees,
            centerLongitude + (x - 0.5) * spanDegrees,
            50);
    }

    private PlanDocument CreateDocumentSnapshot()
    {
        var activeVehicle = _multiVehicleManager.ActiveVehicle;
        var document = PlanDocument.CreateBlank();

        if (activeVehicle is null)
        {
            document.Mission.Items.Add(new MissionPlanItem { Command = 16, DoJumpId = 1 });
            return document;
        }

        document.Mission.VehicleType = (int)activeVehicle.VehicleType;
        document.Mission.FirmwareType = (int)activeVehicle.Autopilot;
        document.Mission.PlannedHomePosition = activeVehicle.Coordinate is { } coordinate
            ? [coordinate.Latitude, coordinate.Longitude, coordinate.AltitudeMeters ?? 0]
            : [0, 0, 0];

        if (activeVehicle.MissionTransferManager.MissionItems.Count > 0)
        {
            foreach (var item in activeVehicle.MissionTransferManager.MissionItems)
            {
                document.Mission.Items.Add(MissionItemConverter.ToMissionPlanItem(item));
            }
        }
        else
        {
            document.Mission.Items.Add(new MissionPlanItem { Command = 16, DoJumpId = 1 });
        }

        var geoFence = activeVehicle.LastGeoFencePlan;
        if (geoFence.Polygons.Count > 0 || geoFence.Circles.Count > 0 || geoFence.BreachReturn is not null)
        {
            document.GeoFence = geoFence;
        }

        var rallyPoints = activeVehicle.LastRallyPointsPlan;
        if (rallyPoints.Points.Count > 0)
        {
            document.RallyPoints = rallyPoints;
        }

        return document;
    }

    private static string BuildMissionSummary(PlanDocument document)
    {
        var mission = document.Mission;
        return $"Mission v{mission.Version} | Items {mission.Items.Count} | Firmware {mission.FirmwareType} | Vehicle {mission.VehicleType}";
    }

    private static string BuildMissionStats(PlanDocument document)
    {
        var mission = document.Mission;
        var altitude = mission.PlannedHomePosition[2];
        return $"Home {mission.PlannedHomePosition[0]:F6}, {mission.PlannedHomePosition[1]:F6}, {altitude:F1} m";
    }

    private IReadOnlyList<PlanMissionMapMarker> BuildMissionMapMarkers()
    {
        return Document.Mission.Items
            .Select((item, index) =>
            {
                var isSelected = ReferenceEquals(item, SelectedItem);
                return new PlanMissionMapMarker(
                    index,
                    item.DoJumpId,
                    item.Command,
                    isSelected,
                    isSelected ? $"{item.DoJumpId}*" : item.DoJumpId.ToString(),
                    isSelected ? "#ffffff" : "#f0c443",
                    isSelected ? "#2f7de1" : "#1b2430",
                    isSelected
                        ? $"Selected waypoint {item.DoJumpId}, command {item.Command}"
                        : $"Waypoint {item.DoJumpId}, command {item.Command}");
            })
            .ToArray();
    }

    private void MoveSelectedItem(int direction)
    {
        if (SelectedItem is null)
        {
            return;
        }

        var index = Document.Mission.Items.IndexOf(SelectedItem);
        var newIndex = index + direction;
        if (index < 0 || newIndex < 0 || newIndex >= Document.Mission.Items.Count)
        {
            return;
        }

        var item = SelectedItem;
        Document.Mission.Items.RemoveAt(index);
        Document.Mission.Items.Insert(newIndex, item);
        NormalizeMissionItemOrder();
        SelectedItem = item;
        MarkPlanDirty();
        RaiseMissionProperties();
    }

    private double GetSelectedParam(int index)
    {
        return SelectedItem is not null && SelectedItem.Params.Length > index
            ? SelectedItem.Params[index]
            : 0;
    }

    private void SetSelectedParam(int index, double value)
    {
        if (SelectedItem is null)
        {
            return;
        }

        EnsureSelectedParams();
        if (Math.Abs(SelectedItem.Params[index] - value) < 0.0000001)
        {
            return;
        }

        SelectedItem.Params[index] = value;
        MarkPlanDirty();
        RaiseSelectedItemProperties();
        RaiseMissionProperties();
    }

    private void EnsureSelectedParams()
    {
        if (SelectedItem is null || SelectedItem.Params.Length >= 7)
        {
            return;
        }

        var expanded = new double[7];
        Array.Copy(SelectedItem.Params, expanded, SelectedItem.Params.Length);
        SelectedItem.Params = expanded;
    }

    private void NormalizeMissionItemOrder()
    {
        for (var index = 0; index < Document.Mission.Items.Count; index++)
        {
            Document.Mission.Items[index].DoJumpId = index + 1;
        }
    }

    private void RaiseMissionProperties()
    {
        this.RaisePropertyChanged(nameof(Document));
        this.RaisePropertyChanged(nameof(MissionItems));
        this.RaisePropertyChanged(nameof(MissionMapMarkers));
        this.RaisePropertyChanged(nameof(MissionSummary));
        this.RaisePropertyChanged(nameof(MissionStats));
        this.RaisePropertyChanged(nameof(PlanMapPreviewSummary));
        this.RaisePropertyChanged(nameof(VehicleMissionState));
        this.RaisePropertyChanged(nameof(MissionTransferSummary));
        this.RaisePropertyChanged(nameof(MissionTransferProgressPercent));
        this.RaisePropertyChanged(nameof(MissionTransferErrorText));
        RaisePlanWorkflowProperties();
        RaisePlanSectionTransferProperties();
        this.RaisePropertyChanged(nameof(EmptyStateText));
        this.RaisePropertyChanged(nameof(SelectedIndex));
        RaisePlanSectionProperties();
    }

    private void RaiseSelectedItemProperties()
    {
        this.RaisePropertyChanged(nameof(SelectedCommand));
        this.RaisePropertyChanged(nameof(SelectedFrame));
        this.RaisePropertyChanged(nameof(SelectedLatitude));
        this.RaisePropertyChanged(nameof(SelectedLongitude));
        this.RaisePropertyChanged(nameof(SelectedAltitude));
        this.RaisePropertyChanged(nameof(SelectedIndex));
        this.RaisePropertyChanged(nameof(HasSelectedItem));
        this.RaisePropertyChanged(nameof(SelectedItemSummary));
        this.RaisePropertyChanged(nameof(SelectedItemCoordinateText));
        this.RaisePropertyChanged(nameof(MissionMapMarkers));
        this.RaisePropertyChanged(nameof(PlanMapWaypoints));
    }

    private static MissionPlanItem CreateWaypointItem(int doJumpId)
    {
        return new MissionPlanItem
        {
            Type = "SimpleItem",
            Command = 16,
            Frame = 3,
            Params = [0, 0, 0, 0, 0, 0, 30],
            AutoContinue = true,
            DoJumpId = doJumpId
        };
    }

    private void RaisePlanSectionProperties()
    {
        this.RaisePropertyChanged(nameof(Document));
        this.RaisePropertyChanged(nameof(GeoFencePolygons));
        this.RaisePropertyChanged(nameof(GeoFenceCircles));
        this.RaisePropertyChanged(nameof(RallyPoints));
        this.RaisePropertyChanged(nameof(GeoFenceSummary));
        this.RaisePropertyChanged(nameof(RallySummary));
        this.RaisePropertyChanged(nameof(GeoFenceValidationText));
        this.RaisePropertyChanged(nameof(RallyValidationText));
        this.RaisePropertyChanged(nameof(PlanImportExportStatusText));
        this.RaisePropertyChanged(nameof(PlanMapDisplayFrame));
        this.RaisePropertyChanged(nameof(PlanMapCenter));
        this.RaisePropertyChanged(nameof(PlanMapZoomLevel));
        this.RaisePropertyChanged(nameof(PlanMapScaleLatitude));
        this.RaisePropertyChanged(nameof(PlanMapWaypoints));
        this.RaisePropertyChanged(nameof(PlanMapMissionPath));
        this.RaisePropertyChanged(nameof(PlanMapPolygons));
        this.RaisePropertyChanged(nameof(PlanMapCircles));
        this.RaisePropertyChanged(nameof(PlanMapRallyPoints));
        this.RaisePropertyChanged(nameof(PlanMapPreviewSummary));
        RaisePlanWorkflowProperties();
        RaisePlanSectionTransferProperties();
    }

    private void RaisePlanWorkflowProperties()
    {
        this.RaisePropertyChanged(nameof(PlanWorkflowNodes));
        this.RaisePropertyChanged(nameof(FileWorkflowState));
        this.RaisePropertyChanged(nameof(IsPlanDirty));
    }

    private void RaisePlanSectionTransferProperties()
    {
        this.RaisePropertyChanged(nameof(PlanTransferLinkText));
        this.RaisePropertyChanged(nameof(GeoFenceTransferSummary));
        this.RaisePropertyChanged(nameof(GeoFenceTransferProgressPercent));
        this.RaisePropertyChanged(nameof(GeoFenceTransferErrorText));
        this.RaisePropertyChanged(nameof(RallyTransferSummary));
        this.RaisePropertyChanged(nameof(RallyTransferProgressPercent));
        this.RaisePropertyChanged(nameof(RallyTransferErrorText));
        RaiseTransferAvailabilityProperties();
    }

    private void RaiseTransferAvailabilityProperties()
    {
        this.RaisePropertyChanged(nameof(CanRequestMissionTransfer));
        this.RaisePropertyChanged(nameof(CanRequestGeoFenceTransfer));
        this.RaisePropertyChanged(nameof(CanRequestRallyTransfer));
        this.RaisePropertyChanged(nameof(HasPendingTransfer));
        this.RaisePropertyChanged(nameof(PendingTransferText));
        this.RaisePropertyChanged(nameof(PlanTransferCommands));
        UpdatePlanTransferCommandState();
    }

    private void RaiseTransferNotificationProperties()
    {
        this.RaisePropertyChanged(nameof(TransferNotifications));
        this.RaisePropertyChanged(nameof(LatestTransferNotificationText));
    }

    private void InitializePlanTransferCommands()
    {
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Mission,
            PlanTransferVerb.Upload,
            "Mission Upload",
            "Mission upload",
            UploadMissionCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Mission,
            PlanTransferVerb.Download,
            "Mission Download",
            "Mission download",
            DownloadMissionCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Mission,
            PlanTransferVerb.Clear,
            "Mission Clear",
            "Mission clear",
            ClearMissionCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.GeoFence,
            PlanTransferVerb.Upload,
            "GeoFence Upload",
            "GeoFence upload",
            UploadGeoFenceCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.GeoFence,
            PlanTransferVerb.Download,
            "GeoFence Download",
            "GeoFence download",
            DownloadGeoFenceCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.GeoFence,
            PlanTransferVerb.Clear,
            "GeoFence Clear",
            "GeoFence clear",
            ClearGeoFenceCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Rally,
            PlanTransferVerb.Upload,
            "Rally Upload",
            "Rally upload",
            UploadRallyCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Rally,
            PlanTransferVerb.Download,
            "Rally Download",
            "Rally download",
            DownloadRallyCommand));
        _planTransferCommands.Add(new PlanTransferToolbarCommand(
            PlanSection.Rally,
            PlanTransferVerb.Clear,
            "Rally Clear",
            "Rally clear",
            ClearRallyCommand));
    }

    private void UpdatePlanTransferCommandState()
    {
        foreach (var command in _planTransferCommands)
        {
            command.IsEnabled = command.Section switch
            {
                PlanSection.Mission => CanRequestMissionTransfer,
                PlanSection.GeoFence => CanRequestGeoFenceTransfer,
                PlanSection.Rally => CanRequestRallyTransfer,
                _ => false
            };
        }
    }

    private void SetPendingTransfer(PendingTransferKind kind, string label, Func<Task> action)
    {
        _pendingTransferKind = kind;
        _pendingTransferText = $"Pending {label}";
        _pendingTransferAction = action;
        RaiseTransferAvailabilityProperties();
    }

    private void ClearPendingTransfer()
    {
        _pendingTransferKind = PendingTransferKind.None;
        _pendingTransferText = null;
        _pendingTransferAction = null;
        RaiseTransferAvailabilityProperties();
    }

    private void AddActionNotification(
        PlanSection section,
        PlanTransferVerb verb,
        string label,
        MissionTransferAction action)
    {
        if (action.Type == MissionTransferActionType.None)
        {
            AddTransferNotification(section, verb, PlanTransferNotificationSeverity.Error, $"{label} failed to start.");
            return;
        }

        AddTransferNotification(section, verb, PlanTransferNotificationSeverity.Success, $"{label} started.");
    }

    private void AddTransferNotification(
        PlanSection section,
        PlanTransferVerb verb,
        PlanTransferNotificationSeverity severity,
        string message)
    {
        _transferNotifications.Insert(0, new PlanTransferNotification(section, verb, severity, message, DateTimeOffset.Now));
        while (_transferNotifications.Count > MaxTransferNotificationCount)
        {
            _transferNotifications.RemoveAt(_transferNotifications.Count - 1);
        }

        RaiseTransferNotificationProperties();
    }

    private void SetPlanImportExportStatus(string status)
    {
        _planImportExportStatusText = status;
        this.RaisePropertyChanged(nameof(PlanImportExportStatusText));
        this.RaisePropertyChanged(nameof(FileWorkflowState));
    }

    private void MarkPlanDirty()
    {
        SetPlanDirty(true);
    }

    private void SetPlanDirty(bool isDirty)
    {
        if (_isPlanDirty == isDirty)
        {
            RaisePlanWorkflowProperties();
            return;
        }

        _isPlanDirty = isDirty;
        RaisePlanWorkflowProperties();
    }

    private IReadOnlyList<PlanWorkflowNode> BuildPlanWorkflowNodes()
    {
        var geoFenceValidation = GeoFenceValidation.Validate(Document.GeoFence);
        var rallyValidation = RallyPointsValidation.Validate(Document.RallyPoints);
        return
        [
            new PlanWorkflowNode(
                "mission",
                "Mission",
                PlanSection.Mission,
                "Simple mission",
                Document.Mission.Items.Count,
                ActiveSection == PlanSection.Mission,
                _planImportExportService.Validate(Document).Any(static issue => issue.Path.StartsWith("$.mission", StringComparison.Ordinal)),
                false,
                MissionSummary),
            new PlanWorkflowNode(
                "complex",
                "Complex",
                null,
                "Survey/Corridor/Structure",
                0,
                false,
                false,
                true,
                "Complex item authoring entry pending"),
            new PlanWorkflowNode(
                "geofence",
                "GeoFence",
                PlanSection.GeoFence,
                "Boundary",
                Document.GeoFence.Polygons.Count + Document.GeoFence.Circles.Count + (Document.GeoFence.BreachReturn is null ? 0 : 1),
                ActiveSection == PlanSection.GeoFence,
                !geoFenceValidation.IsValid,
                false,
                GeoFenceSummary),
            new PlanWorkflowNode(
                "rally",
                "Rally",
                PlanSection.Rally,
                "Rally points",
                Document.RallyPoints.Points.Count,
                ActiveSection == PlanSection.Rally,
                !rallyValidation.IsValid,
                false,
                RallySummary)
        ];
    }

    private PlanFileWorkflowState BuildFileWorkflowState()
    {
        var issues = _planImportExportService.Validate(Document);
        return new PlanFileWorkflowState(
            CanCreateNew: true,
            CanImport: true,
            CanExport: issues.Count == 0,
            CanSave: issues.Count == 0,
            IsDirty: _isPlanDirty,
            StatusText: PlanImportExportStatusText,
            ValidationSummary: issues.Count == 0 ? "Plan validation clean" : FormatPlanIssues(issues),
            AndroidStorageRiskText: "Android scoped-storage file picker/runtime evidence pending");
    }

    private static string FormatPlanIssues(IReadOnlyList<PlanValidationIssue> issues)
    {
        return issues.Count == 0
            ? "Unknown error"
            : string.Join(" | ", issues.Select(static issue => $"{issue.Path}: {issue.Message}"));
    }

    private static (PlanSection Section, PlanTransferVerb Verb, string Label) DescribeTransfer(PendingTransferKind kind)
    {
        return kind switch
        {
            PendingTransferKind.MissionUpload => (PlanSection.Mission, PlanTransferVerb.Upload, "Mission upload"),
            PendingTransferKind.MissionDownload => (PlanSection.Mission, PlanTransferVerb.Download, "Mission download"),
            PendingTransferKind.MissionClear => (PlanSection.Mission, PlanTransferVerb.Clear, "Mission clear"),
            PendingTransferKind.GeoFenceUpload => (PlanSection.GeoFence, PlanTransferVerb.Upload, "GeoFence upload"),
            PendingTransferKind.GeoFenceDownload => (PlanSection.GeoFence, PlanTransferVerb.Download, "GeoFence download"),
            PendingTransferKind.GeoFenceClear => (PlanSection.GeoFence, PlanTransferVerb.Clear, "GeoFence clear"),
            PendingTransferKind.RallyUpload => (PlanSection.Rally, PlanTransferVerb.Upload, "Rally upload"),
            PendingTransferKind.RallyDownload => (PlanSection.Rally, PlanTransferVerb.Download, "Rally download"),
            PendingTransferKind.RallyClear => (PlanSection.Rally, PlanTransferVerb.Clear, "Rally clear"),
            _ => (PlanSection.Mission, PlanTransferVerb.Upload, "Transfer")
        };
    }

    private static string BuildGeoFenceSummary(GeoFencePlan geoFence)
    {
        return $"GeoFence v{geoFence.Version} | Polygons {geoFence.Polygons.Count} | Circles {geoFence.Circles.Count}";
    }

    private static string BuildRallySummary(RallyPointsPlan rallyPoints)
    {
        return $"Rally v{rallyPoints.Version} | Points {rallyPoints.Points.Count}";
    }

    private static string BuildGeoFenceValidationText(GeoFencePlan geoFence)
    {
        var result = GeoFenceValidation.Validate(geoFence);
        return result.IsValid ? "GeoFence valid" : string.Join(" | ", result.Errors);
    }

    private static string BuildRallyValidationText(RallyPointsPlan rallyPoints)
    {
        var result = RallyPointsValidation.Validate(rallyPoints);
        return result.IsValid ? "Rally valid" : string.Join(" | ", result.Errors);
    }

    private static string BuildVehicleMissionState(Vehicle? vehicle)
    {
        if (vehicle is null)
        {
            return "No active vehicle mission state";
        }

        var manager = vehicle.MissionTransferManager;
        return $"Vehicle {vehicle.Id} | Transaction {manager.TransactionType} | Expected {manager.ExpectedMessage} | Stored {manager.MissionItems.Count}";
    }

    private static string BuildPlanMapPreviewSummary(PlanMapDisplayFrame frame)
    {
        return frame.HasAnyOverlay
            ? $"Preview | Mission {frame.MissionWaypoints.Count} | Fence {frame.GeoFencePolygons.Count + frame.GeoFenceCircles.Count} | Rally {frame.RallyPoints.Count} | Center {frame.Viewport.Center.Latitude:F6}, {frame.Viewport.Center.Longitude:F6}"
            : "Preview | No plan overlays";
    }

    private static string BuildSelectedItemSummary(MissionPlanItem? item, int selectedIndex)
    {
        return item is null
            ? "No selected mission item"
            : $"Item {selectedIndex + 1} | Command {item.Command} | Frame {item.Frame} | {item.Type}";
    }

    private static string BuildSelectedItemCoordinateText(MissionPlanItem? item)
    {
        if (item is null || item.Params.Length < 7)
        {
            return "No selected coordinate";
        }

        return $"Lat {item.Params[4]:F6} | Lon {item.Params[5]:F6} | Alt {item.Params[6]:F1} m";
    }

    private static string BuildMissionTransferSummary(Vehicle? vehicle)
    {
        if (vehicle is null)
        {
            return "No mission transfer active";
        }

        var manager = vehicle.MissionTransferManager;
        return manager.TransactionType switch
        {
            MissionTransactionType.Read => $"Reading mission {manager.ReceivedItemCount}/{manager.ExpectedItemCount}",
            MissionTransactionType.Write => $"Writing mission pending {manager.PendingWriteRequestCount}/{manager.ExpectedItemCount}",
            MissionTransactionType.Clear => "Clearing vehicle mission",
            _ when manager.MissionItems.Count > 0 => $"Vehicle mission cached: {manager.MissionItems.Count} item(s)",
            _ => "No mission transfer active"
        };
    }

    private static double GetMissionTransferProgressPercent(Vehicle? vehicle)
    {
        return Math.Round((vehicle?.MissionTransferManager.Progress ?? 0) * 100, 1);
    }

    private static string BuildMissionTransferErrorText(Vehicle? vehicle, string? commandError)
    {
        if (!string.IsNullOrWhiteSpace(commandError))
        {
            return commandError;
        }

        if (vehicle is null)
        {
            return "No mission transfer error";
        }

        var manager = vehicle.MissionTransferManager;
        return manager.LastError == MissionTransferError.None
            ? "No mission transfer error"
            : $"{manager.LastError}: {manager.LastErrorMessage}";
    }

    private ILinkTransport? GetActiveTransferLink()
    {
        return _linkManager?.ActiveLink;
    }

    private static string BuildPlanTransferLinkText(ILinkTransport? link)
    {
        return link is null
            ? "No send-capable link"
            : $"Transfer link: {link.Configuration.Name}";
    }

    private bool IsMissionTransferAvailable()
    {
        return IsSectionTransferAvailable(PlanSection.Mission);
    }

    private bool IsGeoFenceTransferAvailable()
    {
        return IsSectionTransferAvailable(PlanSection.GeoFence);
    }

    private bool IsRallyTransferAvailable()
    {
        return IsSectionTransferAvailable(PlanSection.Rally);
    }

    private bool IsSectionTransferAvailable(PlanSection section)
    {
        var vehicle = _multiVehicleManager.ActiveVehicle;
        if (vehicle is null || HasPendingTransfer)
        {
            return false;
        }

        if (GetActiveTransferLink() is null)
        {
            return false;
        }

        if (vehicle.MissionTransferManager.InProgress
            || vehicle.GeoFenceTransferManager.InProgress
            || vehicle.RallyPointTransferManager.InProgress)
        {
            return false;
        }

        var support = _transferSupportPolicy.GetSupportForVehicle(vehicle);
        var gate = section switch
        {
            PlanSection.Mission => support.IsConnected
                ? PlanTransferGateResult.Allowed
                : PlanTransferGateResult.Blocked("Mission transfer requires a connected vehicle."),
            PlanSection.GeoFence => _transferSupportPolicy.CanTransferGeoFence(support),
            PlanSection.Rally => _transferSupportPolicy.CanTransferRally(support),
            _ => PlanTransferGateResult.Blocked("Unsupported Plan section.")
        };
        return gate.IsAllowed;
    }

    private bool TryPrepareMissionTransfer(out Vehicle vehicle, out string error)
    {
        return TryPrepareVehicleTransfer(out vehicle, out error);
    }

    private bool TryPrepareGeoFenceTransfer(out Vehicle vehicle, out string error)
    {
        if (!TryPrepareVehicleTransfer(out vehicle, out error))
        {
            return false;
        }

        var gate = _transferSupportPolicy.CanTransferGeoFence(_transferSupportPolicy.GetSupportForVehicle(vehicle));
        if (!gate.IsAllowed)
        {
            error = gate.Reason ?? "GeoFence transfer is not supported.";
            return false;
        }

        return true;
    }

    private bool TryPrepareRallyTransfer(out Vehicle vehicle, out string error)
    {
        if (!TryPrepareVehicleTransfer(out vehicle, out error))
        {
            return false;
        }

        var gate = _transferSupportPolicy.CanTransferRally(_transferSupportPolicy.GetSupportForVehicle(vehicle));
        if (!gate.IsAllowed)
        {
            error = gate.Reason ?? "Rally transfer is not supported.";
            return false;
        }

        return true;
    }

    private bool TryPrepareVehicleTransfer(out Vehicle vehicle, out string error)
    {
        vehicle = _multiVehicleManager.ActiveVehicle!;
        if (vehicle is null)
        {
            error = "Transfer requires an active vehicle.";
            return false;
        }

        var link = _linkManager?.ActiveLink;
        if (link is null)
        {
            error = "Transfer requires a connected link.";
            return false;
        }

        if (vehicle.MissionTransferService is null
            || vehicle.GeoFenceTransferService is null
            || vehicle.RallyPointTransferService is null)
        {
            vehicle.AttachPlanTransferLink(link);
        }

        error = string.Empty;
        return true;
    }

    private void SetMissionCommandError(string error)
    {
        _missionCommandError = error;
        RaisePlanSectionTransferProperties();
    }

    private void SetGeoFenceCommandError(string error)
    {
        _geoFenceCommandError = error;
        RaisePlanSectionTransferProperties();
    }

    private void SetRallyCommandError(string error)
    {
        _rallyCommandError = error;
        RaisePlanSectionTransferProperties();
    }

    private static string BuildGeoFenceTransferSummary(Vehicle? vehicle)
    {
        if (vehicle is null)
        {
            return "No GeoFence transfer active";
        }

        var manager = vehicle.GeoFenceTransferManager;
        return manager.TransferType switch
        {
            GeoFenceTransferType.Read => "Reading GeoFence",
            GeoFenceTransferType.Write => $"Writing GeoFence polygons {manager.PolygonCount}, circles {manager.CircleCount}",
            GeoFenceTransferType.Clear => "Clearing GeoFence",
            _ => "No GeoFence transfer active"
        };
    }

    private static double GetGeoFenceTransferProgressPercent(Vehicle? vehicle)
    {
        return Math.Round((vehicle?.GeoFenceTransferManager.Progress ?? 0) * 100, 1);
    }

    private static string BuildGeoFenceTransferErrorText(Vehicle? vehicle, string? commandError)
    {
        if (!string.IsNullOrWhiteSpace(commandError))
        {
            return commandError;
        }

        if (vehicle is null)
        {
            return "No GeoFence transfer error";
        }

        var manager = vehicle.GeoFenceTransferManager;
        return manager.LastError == GeoFenceTransferError.None
            ? "No GeoFence transfer error"
            : $"{manager.LastError}: {manager.LastErrorMessage}";
    }

    private static string BuildRallyTransferSummary(Vehicle? vehicle)
    {
        if (vehicle is null)
        {
            return "No Rally transfer active";
        }

        var manager = vehicle.RallyPointTransferManager;
        return manager.TransferType switch
        {
            RallyPointTransferType.Read => "Reading Rally points",
            RallyPointTransferType.Write => $"Writing Rally points {manager.PointCount}",
            RallyPointTransferType.Clear => "Clearing Rally points",
            _ => "No Rally transfer active"
        };
    }

    private static double GetRallyTransferProgressPercent(Vehicle? vehicle)
    {
        return Math.Round((vehicle?.RallyPointTransferManager.Progress ?? 0) * 100, 1);
    }

    private static string BuildRallyTransferErrorText(Vehicle? vehicle, string? commandError)
    {
        if (!string.IsNullOrWhiteSpace(commandError))
        {
            return commandError;
        }

        if (vehicle is null)
        {
            return "No Rally transfer error";
        }

        var manager = vehicle.RallyPointTransferManager;
        return manager.LastError == RallyPointTransferError.None
            ? "No Rally transfer error"
            : $"{manager.LastError}: {manager.LastErrorMessage}";
    }
}
