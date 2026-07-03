using VGC.Maps;
using VGC.Input;

namespace VGC.Traffic;

public enum TrafficAlertSeverity
{
    Advisory,
    Warning
}

public sealed record TrafficAlert(
    int IcaoAddress,
    string Callsign,
    TrafficAlertSeverity Severity,
    double HorizontalDistanceMeters,
    double VerticalSeparationMeters,
    string Message);

public sealed record TrafficRuntimeSnapshot(
    IReadOnlyList<AdsbVehicle> Vehicles,
    IReadOnlyList<TrafficAlert> Alerts,
    string Summary);

public sealed class AdsbTrafficRuntime
{
    private readonly Dictionary<int, AdsbVehicle> _vehicles = [];

    public IReadOnlyList<AdsbVehicle> Vehicles => _vehicles.Values.OrderBy(static vehicle => vehicle.IcaoAddress).ToArray();

    public AdsbVehicle Upsert(AdsbVehicle vehicle)
    {
        _vehicles[vehicle.IcaoAddress] = vehicle;
        return vehicle;
    }

    public TrafficRuntimeSnapshot Project(double ownLatitude, double ownLongitude, double ownAltitudeMeters)
    {
        var alerts = Vehicles
            .Select(vehicle => CreateAlert(vehicle, ownLatitude, ownLongitude, ownAltitudeMeters))
            .Where(static alert => alert is not null)
            .Select(static alert => alert!)
            .ToArray();

        return new TrafficRuntimeSnapshot(
            Vehicles,
            alerts,
            $"{Vehicles.Count} ADSB traffic targets, {alerts.Length} alerts.");
    }

    public MapProviderOverlayCommandFrame BuildTrafficOverlay()
    {
        var commands = Vehicles
            .Select(vehicle => new MapProviderMarkerCommand(
                $"traffic:{vehicle.IcaoAddress:X}",
                MapProviderOverlayLayer.Traffic,
                string.IsNullOrWhiteSpace(vehicle.Callsign) ? $"ICAO {vehicle.IcaoAddress:X}" : vehicle.Callsign,
                45,
                new MapCoordinate(vehicle.Latitude, vehicle.Longitude, vehicle.AltitudeMeters),
                "traffic-aircraft"))
            .Cast<MapProviderOverlayCommand>()
            .ToArray();

        return new MapProviderOverlayCommandFrame(commands);
    }

    private static TrafficAlert? CreateAlert(AdsbVehicle vehicle, double ownLatitude, double ownLongitude, double ownAltitudeMeters)
    {
        var horizontal = HaversineMeters(ownLatitude, ownLongitude, vehicle.Latitude, vehicle.Longitude);
        var vertical = Math.Abs(vehicle.AltitudeMeters - ownAltitudeMeters);
        if (horizontal <= 500 && vertical <= 100)
        {
            return new TrafficAlert(
                vehicle.IcaoAddress,
                vehicle.Callsign,
                TrafficAlertSeverity.Warning,
                horizontal,
                vertical,
                $"{vehicle.Callsign} traffic warning: {horizontal:F0}m horizontal, {vertical:F0}m vertical.");
        }

        if (horizontal <= 1500 && vertical <= 300)
        {
            return new TrafficAlert(
                vehicle.IcaoAddress,
                vehicle.Callsign,
                TrafficAlertSeverity.Advisory,
                horizontal,
                vertical,
                $"{vehicle.Callsign} traffic advisory: {horizontal:F0}m horizontal, {vertical:F0}m vertical.");
        }

        return null;
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * radius * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}

public sealed record RemoteIdBroadcastSnapshot(
    string Id,
    bool IsBroadcasting,
    string? OperatorId,
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    string? Error,
    string StatusText);

public sealed class RemoteIdRuntime
{
    private RemoteIdBroadcastSnapshot _snapshot = new("unknown", false, null, null, null, null, null, "RemoteID not broadcasting.");

    public RemoteIdBroadcastSnapshot Snapshot => _snapshot;

    public RemoteIdBroadcastSnapshot Update(
        string id,
        bool isBroadcasting,
        string? operatorId,
        double? latitude,
        double? longitude,
        double? altitudeMeters,
        string? error = null)
    {
        _snapshot = new RemoteIdBroadcastSnapshot(
            string.IsNullOrWhiteSpace(id) ? "unknown" : id,
            isBroadcasting,
            operatorId,
            latitude,
            longitude,
            altitudeMeters,
            error,
            error is not null
                ? $"RemoteID error: {error}"
                : isBroadcasting ? $"RemoteID {id} broadcasting." : "RemoteID not broadcasting.");
        return _snapshot;
    }

    public MapProviderOverlayCommandFrame BuildOverlay()
    {
        if (!_snapshot.IsBroadcasting || _snapshot.Latitude is null || _snapshot.Longitude is null)
        {
            return new MapProviderOverlayCommandFrame([]);
        }

        return new MapProviderOverlayCommandFrame(
        [
            new MapProviderMarkerCommand(
                $"remoteid:{_snapshot.Id}",
                MapProviderOverlayLayer.RemoteId,
                _snapshot.Id,
                55,
                new MapCoordinate(_snapshot.Latitude.Value, _snapshot.Longitude.Value, _snapshot.AltitudeMeters),
                "remote-id")
        ]);
    }
}

public sealed record TrafficRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class TrafficRuntimeEvidenceCatalog
{
    public IReadOnlyList<TrafficRuntimeEvidenceItem> Build()
    {
        return
        [
            new("INPUTTRAFFIC-274", "L1/L5", "ADSB traffic runtime tracks targets, map markers, and proximity alerts.", true),
            new("INPUTTRAFFIC-275", "L1/L5", "RemoteID runtime projects broadcast status, operator id, position, errors, and map markers.", true),
            new("INPUTTRAFFIC-276", "L1/L5", "Traffic and RemoteID overlays use provider-neutral map overlay commands.", true),
            new("INPUTTRAFFIC-277", "L1", "Traffic alert projection separates advisory and warning thresholds.", true),
            new("INPUTTRAFFIC-278", "L0/L6", "Real ADSB receiver, RemoteID module, and field alert evidence remains deferred.", false)
        ];
    }
}

public sealed record InputTrafficRuntimeAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class InputTrafficRuntimeParityAudit
{
    public InputTrafficRuntimeAuditResult Audit(
        IReadOnlyList<JoystickRuntimeEvidenceItem> joystickEvidence,
        IReadOnlyList<TrafficRuntimeEvidenceItem> trafficEvidence)
    {
        var complete = joystickEvidence.Count(static item => item.Complete) + trafficEvidence.Count(static item => item.Complete);
        var deferred = joystickEvidence
            .Where(static item => !item.Complete)
            .Select(static item => item.Description)
            .Concat(trafficEvidence.Where(static item => !item.Complete).Select(static item => item.Description))
            .ToArray();

        return new InputTrafficRuntimeAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} joystick/traffic evidence items complete; {deferred.Length} hardware/field evidence gaps remain.");
    }
}
