using VGC.Facts;

namespace VGC.Setup;

public enum FailsafeAction
{
    Disabled,
    Land,
    ReturnToLaunch,
    SmartRtl,
    SmartRtlOrLand,
    Terminate,
    Loiter,
    Continue
}

public sealed record SafetyParameterDefinition(
    string Name,
    string Label,
    string Description,
    string Group,
    SafetyParameterKind Kind,
    IReadOnlyList<SafetyEnumOption>? EnumOptions = null,
    double? Min = null,
    double? Max = null,
    string? Units = null);

public sealed record SafetyEnumOption(int Value, string Label);

public enum SafetyParameterKind
{
    Action,
    Threshold,
    Timeout,
    Altitude,
    Speed,
    Toggle
}

public sealed record SafetyParameterState(
    SafetyParameterDefinition Definition,
    bool IsPresent,
    string CurrentValue,
    string DisplayValue,
    string StatusText)
{
    public bool HasEnumOptions => Definition.EnumOptions is { Count: > 0 };
}

public sealed record SafetyConfigGroup(
    string Name,
    string Description,
    IReadOnlyList<SafetyParameterState> Parameters);

public sealed record SafetyConfigProjection(
    IReadOnlyList<SafetyConfigGroup> Groups,
    int TotalParameters,
    int PresentParameters,
    bool IsComplete,
    string StatusText);

public sealed class SafetyConfigRuntime
{
    private static readonly SafetyParameterDefinition[] ArduPilotCopterParameters =
    [
        // Battery Failsafe
        new("FS_BATT_ENABLE", "Battery Failsafe", "Action when battery is low", "Battery Failsafe", SafetyParameterKind.Action,
            [new(0, "Disabled"), new(1, "Land"), new(2, "RTL")]),
        new("FS_BATT_VOLTAGE", "Low Battery Voltage", "Voltage threshold for battery failsafe", "Battery Failsafe", SafetyParameterKind.Threshold,
            Min: 0, Max: 99, Units: "V"),
        new("FS_BATT_MAH", "Low Battery mAh", "Remaining capacity threshold for battery failsafe", "Battery Failsafe", SafetyParameterKind.Threshold,
            Min: 0, Max: 32767, Units: "mAh"),

        // GCS Failsafe
        new("FS_GCS_ENABLE", "GCS Failsafe", "Action on GCS heartbeat loss", "GCS Failsafe", SafetyParameterKind.Action,
            [new(0, "Disabled"), new(1, "RTL"), new(2, "Continue with mission in Auto")]),

        // Throttle Failsafe (RC Loss)
        new("FS_THR_ENABLE", "Throttle Failsafe", "Action on RC loss detected via throttle", "RC Failsafe", SafetyParameterKind.Action,
            [new(0, "Disabled"), new(1, "RTL"), new(2, "Continue with mission in Auto"), new(3, "Land")]),
        new("FS_THR_VALUE", "Throttle Failsafe Value", "PWM value below which RC is considered lost", "RC Failsafe", SafetyParameterKind.Threshold,
            Min: 900, Max: 1100, Units: "PWM"),

        // EKF Failsafe
        new("FS_EKF_ACTION", "EKF Failsafe Action", "Action on EKF variance failure", "EKF Failsafe", SafetyParameterKind.Action,
            [new(1, "Land"), new(2, "AltHold"), new(3, "Land even in stabilize")]),
        new("FS_EKF_THRESH", "EKF Failsafe Threshold", "EKF variance threshold", "EKF Failsafe", SafetyParameterKind.Threshold,
            Min: 0.6, Max: 1.0),

        // Fence
        new("FENCE_ENABLE", "Fence Enable", "Enable geofence breach detection", "Geofence", SafetyParameterKind.Toggle,
            [new(0, "Disabled"), new(1, "Enabled")]),
        new("FENCE_ACTION", "Fence Breach Action", "Action on geofence breach", "Geofence", SafetyParameterKind.Action,
            [new(0, "Report Only"), new(1, "RTL or Land"), new(2, "Land")]),
        new("FENCE_ALT_MAX", "Fence Max Altitude", "Maximum altitude geofence", "Geofence", SafetyParameterKind.Altitude,
            Min: 0, Max: 1000, Units: "m"),

        // Return to Launch
        new("RTL_ALT", "RTL Altitude", "Altitude to climb to before returning", "Return to Launch", SafetyParameterKind.Altitude,
            Min: 0, Max: 80000, Units: "cm"),
        new("RTL_ALT_FINAL", "RTL Final Altitude", "Altitude to descend to after RTL", "Return to Launch", SafetyParameterKind.Altitude,
            Min: -1, Max: 10000, Units: "cm"),
        new("RTL_LOIT_TIME", "RTL Loiter Time", "Time to loiter above home before landing", "Return to Launch", SafetyParameterKind.Timeout,
            Min: 0, Max: 60000, Units: "ms"),

        // Landing
        new("LAND_SPEED", "Landing Speed", "Final descent speed during landing", "Landing", SafetyParameterKind.Speed,
            Min: 0, Max: 500, Units: "cm/s"),
        new("LAND_SPEED_HIGH", "Landing Approach Speed", "Descent speed from RTL altitude to LAND_ALT_LOW", "Landing", SafetyParameterKind.Speed,
            Min: 0, Max: 2000, Units: "cm/s"),

        // Arming
        new("ARMING_CHECK", "Arming Checks", "Bitmask of pre-arm checks to perform", "Arming", SafetyParameterKind.Toggle,
            [new(0, "Disabled"), new(1, "All Checks")])
    ];

    private static readonly SafetyParameterDefinition[] Px4Parameters =
    [
        // RC Loss
        new("COM_RC_LOSS_T", "RC Loss Timeout", "Time without RC before failsafe triggers", "RC Failsafe", SafetyParameterKind.Timeout,
            Min: 0, Max: 35, Units: "s"),
        new("NAV_RCL_ACT", "RC Loss Action", "Action on RC signal loss", "RC Failsafe", SafetyParameterKind.Action,
            [new(0, "Disabled"), new(1, "Loiter"), new(2, "RTL"), new(3, "Land"), new(5, "Terminate"), new(6, "Disarm")]),

        // Data Link Loss
        new("COM_DL_LOSS_T", "Data Link Loss Timeout", "Time without GCS heartbeat before failsafe", "Data Link Failsafe", SafetyParameterKind.Timeout,
            Min: 5, Max: 300, Units: "s"),
        new("NAV_DLL_ACT", "Data Link Loss Action", "Action on GCS heartbeat loss", "Data Link Failsafe", SafetyParameterKind.Action,
            [new(0, "Disabled"), new(1, "Loiter"), new(2, "RTL"), new(3, "Land"), new(5, "Terminate"), new(6, "Disarm")]),

        // Low Battery
        new("COM_LOW_BAT_ACT", "Low Battery Action", "Action when battery reaches low threshold", "Battery Failsafe", SafetyParameterKind.Action,
            [new(0, "Warning"), new(2, "Land"), new(3, "Return at critical level, Land at emergency level")]),
        new("BAT_LOW_THR", "Low Battery Threshold", "Remaining capacity for low battery warning", "Battery Failsafe", SafetyParameterKind.Threshold,
            Min: 0.05, Max: 0.5, Units: "fraction"),
        new("BAT_CRIT_THR", "Critical Battery Threshold", "Remaining capacity for critical battery", "Battery Failsafe", SafetyParameterKind.Threshold,
            Min: 0.03, Max: 0.5, Units: "fraction"),
        new("BAT_EMERGEN_THR", "Emergency Battery Threshold", "Remaining capacity for emergency landing", "Battery Failsafe", SafetyParameterKind.Threshold,
            Min: 0.01, Max: 0.5, Units: "fraction"),

        // Geofence
        new("GF_ACTION", "Geofence Violation Action", "Action on geofence breach", "Geofence", SafetyParameterKind.Action,
            [new(0, "None"), new(1, "Warning"), new(2, "Loiter"), new(3, "RTL"), new(4, "Land"), new(5, "Terminate")]),
        new("GF_MAX_HOR_DIST", "Max Horizontal Distance", "Maximum horizontal distance from home", "Geofence", SafetyParameterKind.Altitude,
            Min: 0, Max: 10000, Units: "m"),
        new("GF_MAX_VER_DIST", "Max Vertical Distance", "Maximum vertical distance from home", "Geofence", SafetyParameterKind.Altitude,
            Min: 0, Max: 10000, Units: "m"),

        // Return to Launch
        new("RTL_RETURN_ALT", "RTL Return Altitude", "Altitude to climb to before returning", "Return to Launch", SafetyParameterKind.Altitude,
            Min: 0, Max: 150, Units: "m"),
        new("RTL_DESCEND_ALT", "RTL Descend Altitude", "Altitude to descend to after reaching home", "Return to Launch", SafetyParameterKind.Altitude,
            Min: 2, Max: 100, Units: "m"),
        new("RTL_LAND_DELAY", "RTL Land Delay", "Delay after descend before landing (-1 to not land)", "Return to Launch", SafetyParameterKind.Timeout,
            Min: -1, Max: 300, Units: "s"),

        // Landing
        new("MPC_LAND_SPEED", "Landing Speed", "Vertical speed for final landing phase", "Landing", SafetyParameterKind.Speed,
            Min: 0.6, Max: 12, Units: "m/s"),

        // Arming
        new("COM_ARM_WO_GPS", "Allow Arm without GPS", "Allow arming without GPS lock", "Arming", SafetyParameterKind.Toggle,
            [new(0, "Require GPS"), new(1, "Allow without GPS")]),
        new("COM_ARM_MIS_REQ", "Require Mission for Auto", "Require mission loaded before auto mode", "Arming", SafetyParameterKind.Toggle,
            [new(0, "No"), new(1, "Yes")])
    ];

    public SafetyConfigProjection Project(ParameterManager parameterManager, bool isArduPilot)
    {
        var definitions = isArduPilot ? ArduPilotCopterParameters : Px4Parameters;
        var states = definitions.Select(def => ProjectParameter(def, parameterManager)).ToArray();

        var groups = states
            .GroupBy(s => s.Definition.Group)
            .Select(g => new SafetyConfigGroup(
                g.Key,
                $"{g.Count(s => s.IsPresent)}/{g.Count()} parameters available",
                g.ToArray()))
            .ToArray();

        var total = states.Length;
        var present = states.Count(s => s.IsPresent);
        return new SafetyConfigProjection(
            groups,
            total,
            present,
            present == total,
            present == total
                ? "All safety parameters are configured"
                : $"{present}/{total} safety parameters available, {total - present} not loaded from vehicle");
    }

    private static SafetyParameterState ProjectParameter(SafetyParameterDefinition definition, ParameterManager parameterManager)
    {
        var fact = parameterManager.Parameters
            .FirstOrDefault(f => string.Equals(f.Name, definition.Name, StringComparison.Ordinal));

        if (fact is null)
        {
            return new SafetyParameterState(definition, false, "", "Not loaded", "Parameter not received from vehicle");
        }

        var rawValue = fact.RawValue?.ToString() ?? "";
        var displayValue = FormatDisplayValue(definition, rawValue);
        return new SafetyParameterState(definition, true, rawValue, displayValue, "Loaded");
    }

    private static string FormatDisplayValue(SafetyParameterDefinition definition, string rawValue)
    {
        if (definition.EnumOptions is { Count: > 0 } && int.TryParse(rawValue, out var intVal))
        {
            var match = definition.EnumOptions.FirstOrDefault(o => o.Value == intVal);
            if (match is not null)
            {
                return $"{match.Label} ({rawValue})";
            }
        }

        if (definition.Units is not null)
        {
            return $"{rawValue} {definition.Units}";
        }

        return rawValue;
    }
}
