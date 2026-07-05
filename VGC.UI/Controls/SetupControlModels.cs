namespace VGC.Views.Controls;

/// <summary>
/// RC channel value for RCChannelMonitor. PWM range typically 1000-2000.
/// </summary>
public sealed record RCChannelValue(string Name, int Value, int Min = 1000, int Max = 2000);

/// <summary>
/// PID slider parameter descriptor for PidTuningPanel.
/// </summary>
public sealed record PidSliderParam(string Name, string Label, double Value, double Min, double Max, double Step);

/// <summary>
/// Airframe type entry for AirframeSelectionGrid.
/// </summary>
public sealed record AirframeEntry(int Id, string Name, AirframeType Type, string? IconKey = null);

/// <summary>
/// Airframe type categories matching QGC APMAirframeComponent.
/// </summary>
public enum AirframeType
{
    QuadX,
    QuadPlus,
    QuadH,
    HexX,
    HexPlus,
    OctoX,
    OctoPlus,
    FixedWing,
    FlyingWing,
    VTail,
    Tricopter,
    Rover,
    Boat,
    Submarine,
    Helicopter,
    Custom
}

/// <summary>
/// Axis mapping entry for JoystickConfigPanel.
/// </summary>
public sealed record AxisMapping(string Function, int AxisIndex, bool Reversed, double Deadband);

/// <summary>
/// Button mapping entry for JoystickConfigPanel.
/// </summary>
public sealed record ButtonMapping(int ButtonIndex, string ActionName);

// ────────────────────────────────────────────────────────────────
// Pre-flight checklist data models
// QGC equivalents: DefaultChecklist.qml, MultiRotorChecklist.qml,
//                  FixedWingChecklist.qml, RoverChecklist.qml, VTOLChecklist.qml
// ────────────────────────────────────────────────────────────────

/// <summary>Status of a single pre-flight check item.</summary>
public enum CheckItemStatus { Pending, Passed, Failed, Warning }

/// <summary>A single pre-flight check step with name, description, and current status.</summary>
public sealed record PreFlightCheckItem(
    string Name,
    string Description = "",
    CheckItemStatus Status = CheckItemStatus.Pending);

/// <summary>A named group of related pre-flight check items.</summary>
public sealed record PreFlightCheckGroup(
    string Name,
    IReadOnlyList<PreFlightCheckItem> Items);

/// <summary>
/// Factory for DefaultChecklist — generic vehicle pre-flight model.
/// QGC equivalent: DefaultChecklist.qml.
/// </summary>
public static class DefaultChecklist
{
    public static IReadOnlyList<PreFlightCheckGroup> CreateGroups() =>
    [
        new PreFlightCheckGroup("Airframe",
        [
            new PreFlightCheckItem("Propellers / control surfaces secured"),
            new PreFlightCheckItem("No visible damage to airframe or payload"),
            new PreFlightCheckItem("Battery charged and secured"),
            new PreFlightCheckItem("All payload connectors seated"),
        ]),
        new PreFlightCheckGroup("RC / Comms",
        [
            new PreFlightCheckItem("RC transmitter on and bound"),
            new PreFlightCheckItem("Telemetry link established"),
            new PreFlightCheckItem("RC range / signal check passed"),
        ]),
        new PreFlightCheckGroup("Flight Controller",
        [
            new PreFlightCheckItem("Sensors calibrated (Accel / Compass / Baro)"),
            new PreFlightCheckItem("EKF status OK — no red flags"),
            new PreFlightCheckItem("GPS 3D fix acquired"),
            new PreFlightCheckItem("No active warnings or critical messages"),
        ]),
        new PreFlightCheckGroup("Mission",
        [
            new PreFlightCheckItem("Mission plan uploaded and verified"),
            new PreFlightCheckItem("Geofence / rally points configured"),
            new PreFlightCheckItem("RTL altitude and behavior confirmed"),
        ]),
    ];
}

/// <summary>
/// Factory for MultiRotorChecklist.
/// QGC equivalent: MultiRotorChecklist.qml.
/// </summary>
public static class MultiRotorChecklist
{
    public static IReadOnlyList<PreFlightCheckGroup> CreateGroups() =>
    [
        new PreFlightCheckGroup("Motors",
        [
            new PreFlightCheckItem("All motors spin in correct direction (CW/CCW)"),
            new PreFlightCheckItem("Motor test passed — no unusual vibration or heat"),
            new PreFlightCheckItem("ESC calibration current"),
        ]),
        new PreFlightCheckGroup("Propellers",
        [
            new PreFlightCheckItem("Props installed on correct motors"),
            new PreFlightCheckItem("Props tightened and locking nuts secured"),
            new PreFlightCheckItem("No cracks, chips, or warping on any prop"),
        ]),
        new PreFlightCheckGroup("Battery & Power",
        [
            new PreFlightCheckItem("LiPo fully charged (≥ 4.15 V/cell)"),
            new PreFlightCheckItem("Battery connector secure; no hot joints"),
            new PreFlightCheckItem("Voltage sag < 0.3 V under hover load"),
        ]),
        new PreFlightCheckGroup("Flight Controller",
        [
            new PreFlightCheckItem("Accelerometer calibrated"),
            new PreFlightCheckItem("Compass calibrated and healthy"),
            new PreFlightCheckItem("Hover throttle mid-point configured"),
            new PreFlightCheckItem("No GPS/EKF errors at arming"),
        ]),
    ];
}

/// <summary>
/// Factory for FixedWingChecklist.
/// QGC equivalent: FixedWingChecklist.qml.
/// </summary>
public static class FixedWingChecklist
{
    public static IReadOnlyList<PreFlightCheckGroup> CreateGroups() =>
    [
        new PreFlightCheckGroup("Control Surfaces",
        [
            new PreFlightCheckItem("Ailerons move in correct direction for roll"),
            new PreFlightCheckItem("Elevator moves in correct direction for pitch"),
            new PreFlightCheckItem("Rudder moves in correct direction for yaw"),
            new PreFlightCheckItem("Flaps travel to correct positions"),
        ]),
        new PreFlightCheckGroup("Engine / Motor",
        [
            new PreFlightCheckItem("Propeller secured and undamaged"),
            new PreFlightCheckItem("Engine / ESC passed self-test at full throttle"),
            new PreFlightCheckItem("Throttle cut switch operational"),
        ]),
        new PreFlightCheckGroup("Navigation",
        [
            new PreFlightCheckItem("Airspeed sensor calibrated and reading correctly"),
            new PreFlightCheckItem("GPS 3D fix acquired"),
            new PreFlightCheckItem("Takeoff / landing runway clear"),
            new PreFlightCheckItem("Wind speed within operating envelope"),
        ]),
    ];
}

/// <summary>
/// Factory for RoverChecklist.
/// QGC equivalent: RoverChecklist.qml.
/// </summary>
public static class RoverChecklist
{
    public static IReadOnlyList<PreFlightCheckGroup> CreateGroups() =>
    [
        new PreFlightCheckGroup("Drive System",
        [
            new PreFlightCheckItem("Wheels / tracks secured and inflated correctly"),
            new PreFlightCheckItem("Steering servo calibrated and centred"),
            new PreFlightCheckItem("Drive motor direction correct for forward"),
        ]),
        new PreFlightCheckGroup("Navigation",
        [
            new PreFlightCheckItem("GPS 3D fix acquired"),
            new PreFlightCheckItem("Compass calibrated — no magnetic interference nearby"),
            new PreFlightCheckItem("Geofence boundary configured"),
        ]),
        new PreFlightCheckGroup("Failsafe",
        [
            new PreFlightCheckItem("RC failsafe triggers Stop mode"),
            new PreFlightCheckItem("Low battery action configured"),
        ]),
    ];
}

/// <summary>
/// Factory for VTOLChecklist — combines multirotor hover and fixed-wing cruise checks.
/// QGC equivalent: VTOLChecklist.qml.
/// </summary>
public static class VTOLChecklist
{
    public static IReadOnlyList<PreFlightCheckGroup> CreateGroups() =>
    [
        new PreFlightCheckGroup("VTOL Hover Motors",
        [
            new PreFlightCheckItem("Hover motors spin correct direction (CW/CCW)"),
            new PreFlightCheckItem("Hover ESCs calibrated and responsive"),
            new PreFlightCheckItem("No unusual vibration under hover throttle"),
        ]),
        new PreFlightCheckGroup("Fixed-Wing Propulsion",
        [
            new PreFlightCheckItem("Pusher/puller prop secured and balanced"),
            new PreFlightCheckItem("Control surfaces move correctly for fixed-wing mode"),
        ]),
        new PreFlightCheckGroup("Transition",
        [
            new PreFlightCheckItem("Transition airspeed configured correctly"),
            new PreFlightCheckItem("Tilt mechanism (if any) moves full travel smoothly"),
            new PreFlightCheckItem("Back-transition altitude / speed set"),
        ]),
        new PreFlightCheckGroup("Navigation",
        [
            new PreFlightCheckItem("GPS 3D fix acquired"),
            new PreFlightCheckItem("EKF healthy in both VTOL and FW modes"),
            new PreFlightCheckItem("VTOL-specific arming checks passed"),
        ]),
    ];
}
