using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed record VehicleStatusMessage(
    byte ComponentId,
    MavlinkSeverity Severity,
    string Text,
    DateTimeOffset ReceivedAt);
