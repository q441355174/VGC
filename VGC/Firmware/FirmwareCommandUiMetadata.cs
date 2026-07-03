using VGC.Mission;
using VGC.Vehicles;

namespace VGC.Firmware;

public sealed record FirmwareCommandUiMetadata(
    ushort CommandId,
    string Label,
    string Category,
    bool IsAvailable,
    bool RequiresConfirmation,
    string? Reason);

public sealed class FirmwareCommandUiMetadataService
{
    private readonly FirmwareCommandAvailabilityService _availabilityService;

    public FirmwareCommandUiMetadataService(FirmwareCommandAvailabilityService? availabilityService = null)
    {
        _availabilityService = availabilityService ?? new FirmwareCommandAvailabilityService();
    }

    public IReadOnlyList<FirmwareCommandUiMetadata> Project(MavAutopilot autopilot, MavType vehicleType)
    {
        return _availabilityService.GetAvailableCommands(autopilot, vehicleType)
            .Select(command => new FirmwareCommandUiMetadata(
                command.CommandId,
                command.Label,
                CategoryFor(command.CommandId),
                command.IsSupported,
                RequiresConfirmation(command.CommandId),
                command.FirmwareReason ?? command.VehicleTypeReason))
            .ToArray();
    }

    private static string CategoryFor(ushort commandId)
    {
        return commandId switch
        {
            MavlinkMissionCommandIds.NavWaypoint => "Mission",
            MavlinkMissionCommandIds.NavFenceCircleInclusion or MavlinkMissionCommandIds.NavFencePolygonVertexInclusion => "GeoFence",
            MavlinkMissionCommandIds.NavRallyPoint => "Rally",
            _ => "Other"
        };
    }

    private static bool RequiresConfirmation(ushort commandId)
    {
        return commandId is MavlinkMissionCommandIds.NavFenceCircleInclusion
            or MavlinkMissionCommandIds.NavFencePolygonVertexInclusion
            or MavlinkMissionCommandIds.NavRallyPoint;
    }
}
