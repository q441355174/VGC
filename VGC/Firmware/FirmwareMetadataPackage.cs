using VGC.Facts;
using VGC.Vehicles;

namespace VGC.Firmware;

public sealed record FirmwareMetadataPackage(
    string FirmwareId,
    MavAutopilot Autopilot,
    MavType? VehicleType,
    string Version,
    string Source,
    string License,
    IReadOnlyList<ParameterMetadata> Parameters)
{
    public IParameterMetadataCatalog CreateCatalog()
    {
        return new InMemoryParameterMetadataCatalog(Parameters);
    }

    public bool Matches(MavAutopilot autopilot, MavType vehicleType)
    {
        return Autopilot == autopilot && (VehicleType is null || VehicleType == vehicleType);
    }
}

public sealed class FirmwareMetadataPackageRegistry
{
    private readonly List<FirmwareMetadataPackage> _packages = [];

    public FirmwareMetadataPackageRegistry(IEnumerable<FirmwareMetadataPackage>? packages = null)
    {
        if (packages is not null)
        {
            _packages.AddRange(packages);
        }
    }

    public IReadOnlyList<FirmwareMetadataPackage> Packages => _packages;

    public void Register(FirmwareMetadataPackage package)
    {
        _packages.RemoveAll(existing =>
            string.Equals(existing.FirmwareId, package.FirmwareId, StringComparison.Ordinal)
            && existing.Autopilot == package.Autopilot
            && existing.VehicleType == package.VehicleType);
        _packages.Add(package);
    }

    public FirmwareMetadataPackage? Resolve(MavAutopilot autopilot, MavType vehicleType)
    {
        return _packages
            .Where(package => package.Matches(autopilot, vehicleType))
            .OrderByDescending(package => package.VehicleType is not null)
            .ThenByDescending(package => package.Version, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static FirmwareMetadataPackageRegistry CreateDefault()
    {
        return new FirmwareMetadataPackageRegistry([
            new FirmwareMetadataPackage(
                "px4",
                MavAutopilot.Px4,
                null,
                "builtin-v1",
                "VGC builtin PX4 metadata subset",
                "VGC-owned metadata seed",
                [
                    new ParameterMetadata("COM_RC_LOSS_T", Group: "Safety", Label: "RC Loss Timeout", Units: "s", Min: 0, Max: 60, RebootRequired: false),
                    new ParameterMetadata("BAT_LOW_THR", Group: "Power", Label: "Low Battery Threshold", Units: "%", Min: 0, Max: 100),
                    new ParameterMetadata("CAL_ACC0_ID", Group: "Sensors", Label: "Accelerometer ID", RebootRequired: true)
                ]),
            new FirmwareMetadataPackage(
                "ardupilot",
                MavAutopilot.ArduPilotMega,
                null,
                "builtin-v1",
                "VGC builtin ArduPilot metadata subset",
                "VGC-owned metadata seed",
                [
                    new ParameterMetadata("FS_THR_ENABLE", Group: "Safety", Label: "Throttle Failsafe", Min: 0, Max: 2, RebootRequired: true),
                    new ParameterMetadata("BATT_LOW_VOLT", Group: "Power", Label: "Low Battery Voltage", Units: "V", Min: 0, Max: 60),
                    new ParameterMetadata("COMPASS_DEV_ID", Group: "Sensors", Label: "Compass ID", RebootRequired: true)
                ])
        ]);
    }
}
