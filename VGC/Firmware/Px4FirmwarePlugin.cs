namespace VGC.Firmware;

public sealed class Px4FirmwarePlugin : IFirmwarePlugin
{
    public string Name => "PX4";

    public VehicleSupports Supports { get; } = new(
        GeoFenceTransfer: true,
        RallyPointTransfer: true);

    public FirmwareBehaviorProfile Behavior => FirmwareBehaviorProfile.Px4;
}
