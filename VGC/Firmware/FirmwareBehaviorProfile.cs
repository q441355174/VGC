using VGC.Mission;
using VGC.Vehicles;

namespace VGC.Firmware;

public sealed record FirmwareFlightMode(uint CustomMode, string Name, bool CanSet = true);

public sealed record FirmwareCommandCapability(ushort CommandId, string Label, bool IsSupported, string? Reason = null);

public sealed class FirmwareBehaviorProfile
{
    private readonly Dictionary<uint, FirmwareFlightMode> _flightModes;
    private readonly Dictionary<ushort, FirmwareCommandCapability> _commands;

    public FirmwareBehaviorProfile(
        IEnumerable<FirmwareFlightMode> flightModes,
        IEnumerable<FirmwareCommandCapability> commands)
    {
        _flightModes = flightModes.ToDictionary(static mode => mode.CustomMode, static mode => mode);
        _commands = commands.ToDictionary(static command => command.CommandId, static command => command);
    }

    public IReadOnlyCollection<FirmwareFlightMode> FlightModes => _flightModes.Values;

    public IReadOnlyCollection<FirmwareCommandCapability> Commands => _commands.Values;

    public FirmwareFlightMode? FindFlightMode(uint customMode)
    {
        return _flightModes.GetValueOrDefault(customMode);
    }

    public FirmwareCommandCapability? FindCommand(ushort commandId)
    {
        return _commands.GetValueOrDefault(commandId);
    }

    public static FirmwareBehaviorProfile Generic { get; } = new(
        [],
        [
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavWaypoint, "Waypoint", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavFenceCircleInclusion, "Fence Circle Inclusion", false, "Generic firmware has no geofence behavior table."),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavRallyPoint, "Rally Point", false, "Generic firmware has no rally behavior table.")
        ]);

    public static FirmwareBehaviorProfile Px4 { get; } = new(
        [
            new FirmwareFlightMode(0x00010000, "Manual"),
            new FirmwareFlightMode(0x00020000, "Altitude"),
            new FirmwareFlightMode(0x00030000, "Position"),
            new FirmwareFlightMode(0x04040000, "Mission"),
            new FirmwareFlightMode(0x05040000, "Return"),
            new FirmwareFlightMode(0x06040000, "Land")
        ],
        [
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavWaypoint, "Waypoint", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavFenceCircleInclusion, "Fence Circle Inclusion", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavFencePolygonVertexInclusion, "Fence Polygon Inclusion", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavRallyPoint, "Rally Point", true)
        ]);

    public static FirmwareBehaviorProfile ArduPilot { get; } = new(
        [
            new FirmwareFlightMode(0, "Stabilize"),
            new FirmwareFlightMode(1, "Acro"),
            new FirmwareFlightMode(2, "AltHold"),
            new FirmwareFlightMode(3, "Auto"),
            new FirmwareFlightMode(4, "Guided"),
            new FirmwareFlightMode(5, "Loiter"),
            new FirmwareFlightMode(6, "RTL"),
            new FirmwareFlightMode(9, "Land")
        ],
        [
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavWaypoint, "Waypoint", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavFenceCircleInclusion, "Fence Circle Inclusion", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavFencePolygonVertexInclusion, "Fence Polygon Inclusion", true),
            new FirmwareCommandCapability(MavlinkMissionCommandIds.NavRallyPoint, "Rally Point", true)
        ]);
}

public sealed class FirmwareFlightModeResolver
{
    private const byte CustomModeEnabled = 0x01;

    public VehicleFlightModeState Resolve(IFirmwarePlugin plugin, byte baseMode, uint customMode)
    {
        if ((baseMode & CustomModeEnabled) == CustomModeEnabled
            && plugin.Behavior.FindFlightMode(customMode) is { } mode)
        {
            return new VehicleFlightModeState(baseMode, customMode, mode.Name);
        }

        return VehicleFlightModeState.FromHeartbeat(baseMode, customMode);
    }
}
