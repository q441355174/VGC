using System.Collections.ObjectModel;
using ReactiveUI;
using VGC.Comms;
using VGC.Core.Logging;
using VGC.Mavlink;
using VGC.Vehicles;

namespace VGC.ViewModels;

public sealed class OverviewViewModel : ViewModelBase
{
    private readonly LinkManager _linkManager;
    private readonly MavlinkProtocol _mavlinkProtocol;
    private readonly GcsHeartbeatService _gcsHeartbeatService;
    private readonly MultiVehicleManager _multiVehicleManager;

    public OverviewViewModel(
        LinkManager linkManager,
        MavlinkProtocol mavlinkProtocol,
        GcsHeartbeatService gcsHeartbeatService,
        MultiVehicleManager multiVehicleManager,
        ReadOnlyObservableCollection<LogEntry> logs)
    {
        _linkManager = linkManager;
        _mavlinkProtocol = mavlinkProtocol;
        _gcsHeartbeatService = gcsHeartbeatService;
        _multiVehicleManager = multiVehicleManager;
        Logs = logs;

        _linkManager.LinksChanged += (_, _) => Refresh();
        _mavlinkProtocol.PacketReceived += (_, _) => Refresh();
        _gcsHeartbeatService.HeartbeatSent += (_, _) => Refresh();
        _multiVehicleManager.VehiclesChanged += (_, _) => Refresh();
        _multiVehicleManager.VehicleUpdated += (_, _) => Refresh();
    }

    public string Title { get; } = "Overview";

    public ReadOnlyObservableCollection<LogEntry> Logs { get; }

    public string LinkStatus => _linkManager.Links.Count == 0
        ? "No links"
        : $"{_linkManager.Links.Count} link(s), {_linkManager.Links.Count(link => link.IsConnected)} connected";

    public string ActiveVehicleText => _multiVehicleManager.ActiveVehicle is null
        ? "No active vehicle"
        : FormatVehicleStatus(_multiVehicleManager.ActiveVehicle);

    public uint PacketsReceived => _mavlinkProtocol.PacketsReceived;

    public uint HeartbeatsSent => _gcsHeartbeatService.HeartbeatsSent;

    public int VehicleCount => _multiVehicleManager.Vehicles.Count;

    private void Refresh()
    {
        this.RaisePropertyChanged(nameof(LinkStatus));
        this.RaisePropertyChanged(nameof(ActiveVehicleText));
        this.RaisePropertyChanged(nameof(PacketsReceived));
        this.RaisePropertyChanged(nameof(HeartbeatsSent));
        this.RaisePropertyChanged(nameof(VehicleCount));
    }

    private static string FormatVehicleStatus(Vehicle vehicle)
    {
        var parts = new List<string>
        {
            $"Vehicle {vehicle.Id}: {vehicle.Autopilot}/{vehicle.VehicleType}"
        };

        if (vehicle.Coordinate is { } coordinate)
        {
            parts.Add($"{coordinate.Latitude:F6}, {coordinate.Longitude:F6}");
        }

        if (vehicle.RelativeAltitudeMeters is { } relativeAltitude)
        {
            parts.Add($"RelAlt {relativeAltitude:F1} m");
        }

        if (vehicle.BatteryVoltage is { } voltage)
        {
            parts.Add($"Batt {voltage:F1} V");
        }

        if (vehicle.BatteryRemainingPercent is { } remaining)
        {
            parts.Add($"{remaining}%");
        }

        if (vehicle.GpsFixType is { } fixType)
        {
            parts.Add($"GPS fix {fixType}");
        }

        if (vehicle.SatelliteCount is { } satellites)
        {
            parts.Add($"{satellites} sats");
        }

        if (vehicle.ParameterManager.Count > 0)
        {
            parts.Add($"Params {vehicle.ParameterManager.Count}");
        }

        return string.Join(" | ", parts);
    }
}
