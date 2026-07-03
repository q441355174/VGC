namespace VGC.Firmware;

public sealed class GenericFirmwarePlugin : IFirmwarePlugin
{
    public string Name => "Generic";

    public VehicleSupports Supports { get; } = VehicleSupports.None;

    public FirmwareBehaviorProfile Behavior => FirmwareBehaviorProfile.Generic;
}
