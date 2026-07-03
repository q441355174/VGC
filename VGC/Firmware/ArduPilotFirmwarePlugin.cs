namespace VGC.Firmware;

public sealed class ArduPilotFirmwarePlugin : IFirmwarePlugin
{
    public string Name => "ArduPilot";

    public VehicleSupports Supports { get; } = new(
        GeoFenceTransfer: true,
        RallyPointTransfer: true);

    public FirmwareBehaviorProfile Behavior => FirmwareBehaviorProfile.ArduPilot;
}
