namespace VGC.Vehicles;

public enum VehicleSubManagerParityStatus
{
    Complete,
    Partial,
    Blocked
}

public enum VehicleSubManagerPriority
{
    P0,
    P1,
    P2
}

public sealed record VehicleSubManagerParityItem(
    string Id,
    string QgcArea,
    VehicleSubManagerPriority Priority,
    VehicleSubManagerParityStatus Status,
    string VgcOwner,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> MissingCapabilities,
    string RequiredEvidence);

public sealed class VehicleSubManagerParityCatalog
{
    public IReadOnlyList<VehicleSubManagerParityItem> BuildPhase323()
    {
        return
        [
            new("VEH323-LINK", "Link lifecycle and active link diagnostics", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Complete, "VGC.Vehicles.VehicleLinkManager", ["active link", "bytes/errors", "communication loss"], [], "VGC.Tests link manager coverage"),
            new("VEH323-COMMAND", "Command queue and guided actions", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Complete, "VGC.Vehicles.VehicleCommandQueue", ["retry", "ACK clearing", "duplicate rejection", "guided actions"], [], "VGC.Tests command queue/guided action coverage"),
            new("VEH323-INITIAL-CONNECT", "Initial connect request sequence", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Complete, "VGC.Vehicles.InitialConnectService", ["heartbeat", "parameters", "mission", "home", "component information"], [], "VGC.Tests initial connect coverage"),
            new("VEH323-COMPONENT-INFO", "Component information", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Partial, "VGC.Vehicles.ComponentInformationRuntime", ["request", "cached", "failed", "unsupported states"], ["metadata URI download parser", "component metadata merge"], "component metadata fixture and SITL transcript"),
            new("VEH323-FACT-BATTERY", "Battery fact groups", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Partial, "VGC.Vehicles.BatteryFactGroup", ["battery voltage/current/remaining"], ["multi-battery list parity", "charge state/time remaining string parity"], "multi-battery MAVLink fixture"),
            new("VEH323-FACT-ESC", "ESC status fact groups", VehicleSubManagerPriority.P1, VehicleSubManagerParityStatus.Blocked, "Unassigned", [], ["ESC_STATUS", "ESC_INFO", "per-ESC fault projection"], "ESC MAVLink fixture or real ESC transcript"),
            new("VEH323-FACT-HEALTH", "Health and preflight fact groups", VehicleSubManagerPriority.P0, VehicleSubManagerParityStatus.Partial, "VGC.Vehicles.VehicleStatusMessage", ["status text", "preflight checklist"], ["QGC health report parity", "sensor-specific health aggregation"], "PX4/APM health transcript"),
            new("VEH323-SIGNING", "Vehicle signing UX and key management", VehicleSubManagerPriority.P1, VehicleSubManagerParityStatus.Blocked, "VGC.Mavlink.SigningController", ["raw MAVLink v2 signing boundary"], ["vehicle-level signing policy", "key rotation UX"], "signed vehicle session transcript"),
            new("VEH323-OBJECT-AVOIDANCE", "Object avoidance state", VehicleSubManagerPriority.P1, VehicleSubManagerParityStatus.Blocked, "Unassigned", [], ["obstacle distance", "avoidance status", "operator warning projection"], "obstacle/avoidance MAVLink fixture"),
            new("VEH323-AUTOTUNE", "Autotune workflow", VehicleSubManagerPriority.P2, VehicleSubManagerParityStatus.Blocked, "Unassigned", [], ["autotune command lifecycle", "progress/result projection"], "PX4/APM autotune transcript")
        ];
    }
}

public sealed record VehicleSubManagerParitySummary(
    int TotalItems,
    int CompleteItems,
    int PartialItems,
    int BlockedItems,
    bool CanClaimVehicleParity,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class VehicleSubManagerParityAudit
{
    public VehicleSubManagerParitySummary Audit(IReadOnlyList<VehicleSubManagerParityItem> items)
    {
        var blockers = items
            .Where(static item => item.Priority is VehicleSubManagerPriority.P0 or VehicleSubManagerPriority.P1
                && item.Status != VehicleSubManagerParityStatus.Complete)
            .Select(static item => $"{item.Id}: {item.QgcArea} remains {item.Status}; needs {item.RequiredEvidence}.")
            .ToArray();
        var complete = items.Count(static item => item.Status == VehicleSubManagerParityStatus.Complete);
        var partial = items.Count(static item => item.Status == VehicleSubManagerParityStatus.Partial);
        var blocked = items.Count(static item => item.Status == VehicleSubManagerParityStatus.Blocked);

        return new VehicleSubManagerParitySummary(
            items.Count,
            complete,
            partial,
            blocked,
            blockers.Length == 0,
            blockers,
            $"{complete}/{items.Count} vehicle sub-manager parity items complete; {partial} partial, {blocked} blocked.");
    }
}
