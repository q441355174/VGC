using VGC.Facts;
using VGC.Mission;

namespace VGC.Firmware;

public sealed record FirmwareCommandAvailability(
    ushort CommandId,
    string Label,
    bool IsSupported,
    string? FirmwareReason = null,
    string? VehicleTypeReason = null);

public sealed class FirmwareCommandAvailabilityService
{
    private readonly FirmwarePluginManager _pluginManager;
    private readonly InMemoryMissionCommandMetadataCatalog _commandCatalog;

    public FirmwareCommandAvailabilityService(
        FirmwarePluginManager? pluginManager = null,
        InMemoryMissionCommandMetadataCatalog? catalog = null)
    {
        _pluginManager = pluginManager ?? new FirmwarePluginManager();
        _commandCatalog = catalog ?? InMemoryMissionCommandMetadataCatalog.CreateDefault();
    }

    public IReadOnlyList<FirmwareCommandAvailability> GetAvailableCommands(
        Vehicles.MavAutopilot autopilot,
        Vehicles.MavType vehicleType)
    {
        var plugin = _pluginManager.GetPlugin(autopilot);
        var results = new List<FirmwareCommandAvailability>();

        foreach (var metadata in _commandCatalog.GetAll())
        {
            var pluginSupport = plugin.Behavior.FindCommand(metadata.CommandId);
            var isSupported = pluginSupport?.IsSupported == true;
            var reason = isSupported ? null : pluginSupport?.Reason ?? $"Command {metadata.CommandId} not in firmware profile.";

            results.Add(new FirmwareCommandAvailability(
                metadata.CommandId,
                metadata.Label,
                isSupported,
                reason));
        }

        return results;
    }
}
