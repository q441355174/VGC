namespace VGC.Firmware;

public sealed record VehicleSupports(
    bool GeoFenceTransfer,
    bool RallyPointTransfer)
{
    public static VehicleSupports None { get; } = new(false, false);
}
