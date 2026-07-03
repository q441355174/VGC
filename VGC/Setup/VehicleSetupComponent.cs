using VGC.Firmware;
using VGC.Vehicles;

namespace VGC.Setup;

public sealed record VehicleSetupComponent(
    string Id,
    string Title,
    string Summary,
    bool IsAvailable,
    string? UnavailableReason = null,
    bool IsRequired = true,
    IReadOnlyList<string>? RequiredParameters = null);

public enum VehicleSetupReadiness
{
    Ready,
    Warning,
    Blocked,
    Unavailable
}

public sealed record VehicleSetupComponentStatus(
    string Id,
    string Title,
    string Summary,
    bool IsAvailable,
    bool IsRequired,
    VehicleSetupReadiness Readiness,
    string StatusText,
    IReadOnlyList<string> MissingParameters);

public sealed class VehicleSetupStatusService
{
    public VehicleSetupComponentStatus Project(VehicleSetupComponent component, VGC.Facts.ParameterManager parameterManager)
    {
        if (!component.IsAvailable)
        {
            return new VehicleSetupComponentStatus(
                component.Id,
                component.Title,
                component.Summary,
                false,
                component.IsRequired,
                VehicleSetupReadiness.Unavailable,
                component.UnavailableReason ?? "Unavailable",
                []);
        }

        var missing = (component.RequiredParameters ?? [])
            .Where(parameterName => !parameterManager.Parameters.Any(fact => string.Equals(fact.Name, parameterName, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (missing.Length > 0 && component.IsRequired)
        {
            return new VehicleSetupComponentStatus(
                component.Id,
                component.Title,
                component.Summary,
                true,
                true,
                VehicleSetupReadiness.Blocked,
                $"Missing parameters: {string.Join(", ", missing)}",
                missing);
        }

        if (missing.Length > 0)
        {
            return new VehicleSetupComponentStatus(
                component.Id,
                component.Title,
                component.Summary,
                true,
                false,
                VehicleSetupReadiness.Warning,
                $"Optional parameters missing: {string.Join(", ", missing)}",
                missing);
        }

        return new VehicleSetupComponentStatus(
            component.Id,
            component.Title,
            component.Summary,
            true,
            component.IsRequired,
            VehicleSetupReadiness.Ready,
            "Ready",
            []);
    }
}

public sealed class VehicleSetupComponentCatalog
{
    public IReadOnlyList<VehicleSetupComponent> GetComponents(IFirmwarePlugin firmwarePlugin, MavType vehicleType)
    {
        var components = new List<VehicleSetupComponent>
        {
            new("summary", "Summary", $"Read-only {firmwarePlugin.Name} setup summary.", true),
            new("firmware", "Firmware", $"Firmware behavior profile: {firmwarePlugin.Name}.", true),
            new("sensors", "Sensors", "Sensor status and calibration entry point.", true, RequiredParameters: ["CAL_ACC0_ID", "CAL_GYRO0_ID", "CAL_MAG0_ID"]),
            new("radio", "Radio", "RC input setup entry point.", true, IsRequired: false, RequiredParameters: ["RC_MAP_ROLL", "RC_MAP_PITCH", "RC_MAP_THROTTLE", "RC_MAP_YAW"]),
            new("power", "Power", "Battery and power setup entry point.", true, RequiredParameters: ["BAT_LOW_THR"]),
            new("safety", "Safety", "Arming, failsafe, and safety setup entry point.", true, RequiredParameters: ["COM_RC_LOSS_T"]),
            new(
                "flight-modes",
                "Flight Modes",
                "Settable flight mode setup entry point.",
                firmwarePlugin.Behavior.FlightModes.Count > 0,
                firmwarePlugin.Behavior.FlightModes.Count == 0 ? "No firmware flight mode table is available." : null),
            new(
                "geofence",
                "GeoFence",
                "GeoFence setup entry point.",
                firmwarePlugin.Supports.GeoFenceTransfer,
                firmwarePlugin.Supports.GeoFenceTransfer ? null : "Firmware profile does not expose GeoFence support."),
            new(
                "rally",
                "Rally Points",
                "Rally point setup entry point.",
                firmwarePlugin.Supports.RallyPointTransfer,
                firmwarePlugin.Supports.RallyPointTransfer ? null : "Firmware profile does not expose Rally support.")
        };

        if (IsMultirotor(vehicleType))
        {
            components.Add(new VehicleSetupComponent("motors", "Motors", "Motor assignment and test entry point.", true, RequiredParameters: ["MOT_SPIN_MIN"]));
        }
        else if (vehicleType == MavType.FixedWing)
        {
            components.Add(new VehicleSetupComponent("airframe", "Airframe", "Fixed-wing airframe setup entry point.", true));
        }
        else
        {
            components.Add(new VehicleSetupComponent("airframe", "Airframe", "Vehicle-type-specific airframe setup is not mapped yet.", false, "Unsupported vehicle type for setup skeleton."));
        }

        return components;
    }

    private static bool IsMultirotor(MavType vehicleType)
    {
        return vehicleType is MavType.Quadrotor;
    }
}
