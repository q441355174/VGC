using VGC.Vehicles;

namespace VGC.Mavlink;

public sealed record MavlinkHeartbeat(
    byte SystemId,
    byte ComponentId,
    MavAutopilot Autopilot,
    MavType VehicleType,
    byte BaseMode,
    uint CustomMode,
    byte SystemStatus);
