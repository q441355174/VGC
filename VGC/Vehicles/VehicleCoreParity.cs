namespace VGC.Vehicles;

public enum VehicleCoreParityDisposition
{
    Complete,
    Partial,
    Deferred,
    Missing,
    OwnedByOtherPhase
}

public enum VehicleCoreParityPriority
{
    P0,
    P1,
    P2
}

public sealed record VehicleCoreParityItem(
    string Id,
    string QgcArea,
    VehicleCoreParityPriority Priority,
    VehicleCoreParityDisposition Disposition,
    string VgcOwner,
    string Notes);

public sealed class VehicleCoreParityCatalog
{
    public IReadOnlyList<VehicleCoreParityItem> Build()
    {
        return
        [
            new("VEH-311-LINK", "VehicleLinkManager", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.VehicleLinkManager", "Active link diagnostics, bytes, errors, and communication loss projection exist."),
            new("VEH-311-INITIAL-CONNECT", "InitialConnectStateMachine", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.InitialConnectService", "Heartbeat, parameter, mission, home, and component-information request phases are modeled."),
            new("VEH-311-REQUEST-MESSAGE", "RequestMessageCoordinator", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.RequestMessageCoordinator", "MAV_CMD_REQUEST_MESSAGE request and ACK state is covered."),
            new("VEH-311-MESSAGE-INTERVAL", "MessageIntervalManager", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.MessageIntervalManager", "Default/high/low stream request profiles, retries, and ACK handling are modeled."),
            new("VEH-311-COMMAND-QUEUE", "MavCommandQueue", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.VehicleCommandQueue", "Duplicate detection, retries, ACK clearing, and failure states are covered."),
            new("VEH-311-SUPPORTS", "VehicleSupports", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Partial, "VGC.Vehicles.VehicleCapabilitiesService", "Firmware capability queries exist; full QGC support matrix remains broader."),
            new("VEH-311-FACT-GROUPS", "FactGroups", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Partial, "VGC.Vehicles.*FactGroup", "Battery, GPS, radio, attitude, EKF, vibration, and wind exist; many QGC specialty fact groups remain missing."),
            new("VEH-311-STANDARD-MODES", "StandardModes", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.VehicleStandardModeCatalog", "Phase 311 adds firmware-aware shared mode metadata."),
            new("VEH-311-COMPONENT-INFO", "ComponentInformation", VehicleCoreParityPriority.P0, VehicleCoreParityDisposition.Partial, "VGC.Vehicles.ComponentInformationRuntime", "Phase 311 adds request/cache/failure/unsupported state boundaries; metadata download parsing remains later work."),
            new("VEH-311-TRAJECTORY", "TrajectoryPoints", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.Complete, "VGC.Vehicles.VehicleTrajectoryStore", "Phase 311 adds bounded timestamped trajectory storage independent of map SDKs."),
            new("VEH-311-SIGNING", "VehicleSigningController", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.Deferred, "Phase 312", "Raw MAVLink signing boundary exists; vehicle-level signing UX/key management remains deferred."),
            new("VEH-311-TERRAIN-PROTOCOL", "TerrainProtocolHandler/TerrainQueryCoordinator", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.OwnedByOtherPhase, "Phase 315", "Terrain planning exists; vehicle-driven terrain protocol coordination belongs with map/terrain production runtime."),
            new("VEH-311-OBJECT-AVOIDANCE", "VehicleObjectAvoidance", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.Missing, "Unassigned", "No obstacle/object avoidance state projection exists yet."),
            new("VEH-311-ACTUATORS", "Actuators", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.OwnedByOtherPhase, "Phase 313", "Actuator setup/testing belongs with firmware setup and calibration parity."),
            new("VEH-311-MAVLINK-LOG-FTP", "MAVLinkLogManager/FTPManager", VehicleCoreParityPriority.P1, VehicleCoreParityDisposition.OwnedByOtherPhase, "Phase 317", "Protocol FTP and analyze log boundaries exist; QGC-style vehicle-owned orchestration belongs with Analyze/log parity."),
            new("VEH-311-AUTOTUNE", "Autotune", VehicleCoreParityPriority.P2, VehicleCoreParityDisposition.Deferred, "Future phase", "Autotune command workflow is not yet modeled.")
        ];
    }
}

public sealed record VehicleStandardMode(
    string Id,
    string DisplayName,
    MavAutopilot Autopilot,
    uint CustomMode,
    bool IsCommandable,
    bool IsDisplayOnly,
    bool IsFirmwareSpecific);

public sealed class VehicleStandardModeCatalog
{
    private readonly IReadOnlyList<VehicleStandardMode> _modes;

    public VehicleStandardModeCatalog()
    {
        _modes =
        [
            new("GENERIC-MANUAL", "Manual", MavAutopilot.Generic, 0, true, false, false),
            new("GENERIC-HOLD", "Hold", MavAutopilot.Generic, 1, true, false, false),
            new("PX4-MANUAL", "Manual", MavAutopilot.Px4, 1, true, false, true),
            new("PX4-POSITION", "Position", MavAutopilot.Px4, 3, true, false, true),
            new("PX4-MISSION", "Mission", MavAutopilot.Px4, 4, true, false, true),
            new("PX4-HOLD", "Hold", MavAutopilot.Px4, 5, true, false, true),
            new("PX4-RETURN", "Return", MavAutopilot.Px4, 6, true, false, true),
            new("PX4-LAND", "Land", MavAutopilot.Px4, 9, true, false, true),
            new("PX4-UNKNOWN", "PX4 Custom", MavAutopilot.Px4, uint.MaxValue, false, true, true),
            new("APM-STABILIZE", "Stabilize", MavAutopilot.ArduPilotMega, 0, true, false, true),
            new("APM-ALT-HOLD", "AltHold", MavAutopilot.ArduPilotMega, 2, true, false, true),
            new("APM-AUTO", "Auto", MavAutopilot.ArduPilotMega, 3, true, false, true),
            new("APM-GUIDED", "Guided", MavAutopilot.ArduPilotMega, 4, true, false, true),
            new("APM-LOITER", "Loiter", MavAutopilot.ArduPilotMega, 5, true, false, true),
            new("APM-RTL", "RTL", MavAutopilot.ArduPilotMega, 6, true, false, true),
            new("APM-LAND", "Land", MavAutopilot.ArduPilotMega, 9, true, false, true),
            new("APM-UNKNOWN", "ArduPilot Custom", MavAutopilot.ArduPilotMega, uint.MaxValue, false, true, true)
        ];
    }

    public IReadOnlyList<VehicleStandardMode> Modes => _modes;

    public IReadOnlyList<VehicleStandardMode> GetModes(MavAutopilot autopilot)
    {
        var modes = _modes.Where(mode => mode.Autopilot == autopilot).ToArray();
        return modes.Length > 0 ? modes : _modes.Where(static mode => mode.Autopilot == MavAutopilot.Generic).ToArray();
    }

    public bool TryFind(MavAutopilot autopilot, uint customMode, out VehicleStandardMode mode)
    {
        mode = GetModes(autopilot).FirstOrDefault(candidate => candidate.CustomMode == customMode)
            ?? GetModes(autopilot).FirstOrDefault(static candidate => candidate.CustomMode == uint.MaxValue)
            ?? new VehicleStandardMode("GENERIC-UNKNOWN", "Unknown", MavAutopilot.Generic, customMode, false, true, false);
        return !mode.Id.EndsWith("UNKNOWN", StringComparison.Ordinal);
    }
}

public enum ComponentInformationKind
{
    General,
    Parameters,
    Actuators,
    Events
}

public enum ComponentInformationState
{
    Unavailable,
    Requested,
    Cached,
    Failed,
    Unsupported
}

public sealed record ComponentInformationEntry(
    ComponentInformationKind Kind,
    ComponentInformationState State,
    DateTimeOffset UpdatedAt,
    string? Uri,
    string? Version,
    string? Message);

public sealed class ComponentInformationRuntime
{
    private readonly Dictionary<ComponentInformationKind, ComponentInformationEntry> _entries = new();

    public ComponentInformationRuntime()
    {
        foreach (var kind in Enum.GetValues<ComponentInformationKind>())
        {
            _entries[kind] = Create(kind, ComponentInformationState.Unavailable, null, null, null);
        }
    }

    public IReadOnlyList<ComponentInformationEntry> Entries => _entries.Values
        .OrderBy(static entry => entry.Kind)
        .ToArray();

    public ComponentInformationEntry Get(ComponentInformationKind kind)
    {
        return _entries[kind];
    }

    public ComponentInformationEntry Request(ComponentInformationKind kind)
    {
        return Set(kind, ComponentInformationState.Requested, null, null, "Metadata requested.");
    }

    public ComponentInformationEntry MarkCached(ComponentInformationKind kind, string uri, string version)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            throw new ArgumentException("Component information URI is required.", nameof(uri));
        }

        return Set(kind, ComponentInformationState.Cached, uri, version, "Metadata cached.");
    }

    public ComponentInformationEntry MarkFailed(ComponentInformationKind kind, string message)
    {
        return Set(kind, ComponentInformationState.Failed, null, null, NormalizeMessage(message, "Metadata request failed."));
    }

    public ComponentInformationEntry MarkUnsupported(ComponentInformationKind kind, string message)
    {
        return Set(kind, ComponentInformationState.Unsupported, null, null, NormalizeMessage(message, "Metadata type unsupported."));
    }

    private ComponentInformationEntry Set(ComponentInformationKind kind, ComponentInformationState state, string? uri, string? version, string? message)
    {
        var entry = Create(kind, state, uri, version, message);
        _entries[kind] = entry;
        return entry;
    }

    private static ComponentInformationEntry Create(ComponentInformationKind kind, ComponentInformationState state, string? uri, string? version, string? message)
    {
        return new ComponentInformationEntry(kind, state, DateTimeOffset.UtcNow, uri, version, message);
    }

    private static string NormalizeMessage(string message, string fallback)
    {
        return string.IsNullOrWhiteSpace(message) ? fallback : message;
    }
}

public sealed record VehicleTrajectoryPoint(
    VehicleCoordinate Coordinate,
    DateTimeOffset Timestamp,
    double? HeadingDegrees = null,
    double? GroundSpeedMs = null);

public sealed class VehicleTrajectoryStore
{
    private readonly Queue<VehicleTrajectoryPoint> _points = new();

    public VehicleTrajectoryStore(int maxPoints = 500)
    {
        if (maxPoints <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPoints), "Trajectory history must retain at least one point.");
        }

        MaxPoints = maxPoints;
    }

    public int MaxPoints { get; }

    public IReadOnlyList<VehicleTrajectoryPoint> Points => _points.ToArray();

    public VehicleTrajectoryPoint? LatestPoint => _points.Count == 0 ? null : _points.Last();

    public bool Add(VehicleCoordinate coordinate, DateTimeOffset timestamp, double? headingDegrees = null, double? groundSpeedMs = null)
    {
        if (!IsValid(coordinate))
        {
            return false;
        }

        _points.Enqueue(new VehicleTrajectoryPoint(coordinate, timestamp, headingDegrees, groundSpeedMs));
        while (_points.Count > MaxPoints)
        {
            _points.Dequeue();
        }

        return true;
    }

    public void Clear()
    {
        _points.Clear();
    }

    private static bool IsValid(VehicleCoordinate coordinate)
    {
        return double.IsFinite(coordinate.Latitude)
            && double.IsFinite(coordinate.Longitude)
            && coordinate.Latitude is >= -90 and <= 90
            && coordinate.Longitude is >= -180 and <= 180
            && (!coordinate.AltitudeMeters.HasValue || double.IsFinite(coordinate.AltitudeMeters.Value));
    }
}

public sealed record VehicleCoreParitySummary(
    int TotalAreas,
    int CompleteAreas,
    int PartialAreas,
    int DeferredAreas,
    int MissingAreas,
    bool CanClaimQgcVehicleParity,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class VehicleCoreParityAudit
{
    public VehicleCoreParitySummary Audit(IReadOnlyList<VehicleCoreParityItem> items)
    {
        var blockers = items
            .Where(static item => item.Priority is VehicleCoreParityPriority.P0 or VehicleCoreParityPriority.P1
                && item.Disposition is not VehicleCoreParityDisposition.Complete)
            .Select(static item => $"{item.Id}: {item.QgcArea} remains {item.Disposition} ({item.VgcOwner}).")
            .ToArray();
        var complete = items.Count(static item => item.Disposition == VehicleCoreParityDisposition.Complete);
        var partial = items.Count(static item => item.Disposition == VehicleCoreParityDisposition.Partial);
        var deferred = items.Count(static item => item.Disposition is VehicleCoreParityDisposition.Deferred or VehicleCoreParityDisposition.OwnedByOtherPhase);
        var missing = items.Count(static item => item.Disposition == VehicleCoreParityDisposition.Missing);
        var canClaim = blockers.Length == 0 && items.All(static item => item.Disposition == VehicleCoreParityDisposition.Complete);

        return new VehicleCoreParitySummary(
            items.Count,
            complete,
            partial,
            deferred,
            missing,
            canClaim,
            blockers,
            $"{complete}/{items.Count} QGC Vehicle areas complete; {partial} partial, {deferred} deferred/owned by later phases, {missing} missing.");
    }
}
