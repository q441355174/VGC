namespace VGC.Firmware;

public interface IFirmwarePlugin
{
    string Name { get; }

    VehicleSupports Supports { get; }

    FirmwareBehaviorProfile Behavior { get; }
}
