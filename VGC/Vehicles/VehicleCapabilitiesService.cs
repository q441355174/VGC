using VGC.Firmware;
using VGC.Mission;

namespace VGC.Vehicles;

public sealed record VehicleCapabilities(
    bool GeoFenceTransfer,
    bool RallyPointTransfer,
    int FlightModeCount,
    int SupportedCommandCount,
    IReadOnlyList<string> FlightModeNames,
    IReadOnlyList<ushort> SupportedCommandIds);

public sealed class VehicleCapabilitiesService
{
    private readonly FirmwarePluginManager _pluginManager;

    public VehicleCapabilitiesService(FirmwarePluginManager? pluginManager = null)
    {
        _pluginManager = pluginManager ?? new FirmwarePluginManager();
    }

    public VehicleCapabilities GetCapabilities(Vehicle vehicle)
    {
        var plugin = _pluginManager.GetPlugin(vehicle.Autopilot);
        return new VehicleCapabilities(
            GeoFenceTransfer: plugin.Supports.GeoFenceTransfer,
            RallyPointTransfer: plugin.Supports.RallyPointTransfer,
            FlightModeCount: plugin.Behavior.FlightModes.Count,
            SupportedCommandCount: plugin.Behavior.Commands.Count,
            FlightModeNames: plugin.Behavior.FlightModes
                .Select(static mode => mode.Name)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            SupportedCommandIds: plugin.Behavior.Commands
                .Select(static cmd => cmd.CommandId)
                .Order()
                .ToArray());
    }

    public bool CanExecuteCommand(Vehicle vehicle, ushort commandId)
    {
        var plugin = _pluginManager.GetPlugin(vehicle.Autopilot);
        return plugin.Behavior.FindCommand(commandId)?.IsSupported == true;
    }

    public bool HasFlightMode(Vehicle vehicle, uint customMode)
    {
        var plugin = _pluginManager.GetPlugin(vehicle.Autopilot);
        return plugin.Behavior.FindFlightMode(customMode) is not null;
    }
}
