using VGC.Vehicles;

namespace VGC.Firmware;

public sealed class FirmwarePluginManager
{
    private readonly IFirmwarePlugin _generic = new GenericFirmwarePlugin();
    private readonly IFirmwarePlugin _px4 = new Px4FirmwarePlugin();
    private readonly IFirmwarePlugin _arduPilot = new ArduPilotFirmwarePlugin();

    public IFirmwarePlugin GetPlugin(MavAutopilot autopilot)
    {
        return autopilot switch
        {
            MavAutopilot.Px4 => _px4,
            MavAutopilot.ArduPilotMega => _arduPilot,
            _ => _generic
        };
    }
}
