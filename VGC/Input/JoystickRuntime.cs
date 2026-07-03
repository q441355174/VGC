namespace VGC.Input;

public sealed record JoystickRawState(
    IReadOnlyList<double> Axes,
    IReadOnlyList<bool> Buttons);

public sealed record JoystickAxisCalibration(
    int AxisIndex,
    double Minimum,
    double Center,
    double Maximum,
    bool Reversed = false,
    double Deadband = 0.05);

public sealed record JoystickCalibrationProfile(
    string DeviceName,
    JoystickAxisCalibration Roll,
    JoystickAxisCalibration Pitch,
    JoystickAxisCalibration Yaw,
    JoystickAxisCalibration Throttle);

public sealed record JoystickInputProjection(
    double Roll,
    double Pitch,
    double Yaw,
    double Throttle,
    ushort ButtonMask,
    string StatusText);

public sealed record JoystickManualControlCommand(
    short X,
    short Y,
    short Z,
    short R,
    ushort Buttons,
    string StatusText);

public sealed class JoystickCalibrationService
{
    public JoystickCalibrationProfile CreateDefaultProfile(JoystickDevice device)
    {
        if (device.Axes < 4)
        {
            throw new ArgumentException("Joystick requires at least four axes for roll, pitch, yaw, and throttle.", nameof(device));
        }

        return new JoystickCalibrationProfile(
            device.Name,
            new JoystickAxisCalibration(0, -1, 0, 1),
            new JoystickAxisCalibration(1, -1, 0, 1, Reversed: true),
            new JoystickAxisCalibration(2, -1, 0, 1),
            new JoystickAxisCalibration(3, -1, 0, 1));
    }

    public JoystickInputProjection Project(JoystickRawState raw, JoystickCalibrationProfile profile)
    {
        var roll = NormalizeAxis(raw, profile.Roll);
        var pitch = NormalizeAxis(raw, profile.Pitch);
        var yaw = NormalizeAxis(raw, profile.Yaw);
        var throttle = (NormalizeAxis(raw, profile.Throttle) + 1) / 2.0;
        var buttons = BuildButtonMask(raw.Buttons);

        return new JoystickInputProjection(
            roll,
            pitch,
            yaw,
            Math.Clamp(throttle, 0, 1),
            buttons,
            $"Joystick {profile.DeviceName}: {raw.Axes.Count} axes, {raw.Buttons.Count} buttons.");
    }

    private static double NormalizeAxis(JoystickRawState raw, JoystickAxisCalibration calibration)
    {
        if (calibration.AxisIndex < 0 || calibration.AxisIndex >= raw.Axes.Count)
        {
            return 0;
        }

        var value = raw.Axes[calibration.AxisIndex];
        var normalized = value >= calibration.Center
            ? Divide(value - calibration.Center, calibration.Maximum - calibration.Center)
            : Divide(value - calibration.Center, calibration.Center - calibration.Minimum);
        normalized = Math.Clamp(normalized, -1, 1);
        if (Math.Abs(normalized) < calibration.Deadband)
        {
            normalized = 0;
        }

        return calibration.Reversed ? -normalized : normalized;
    }

    private static double Divide(double numerator, double denominator)
    {
        return Math.Abs(denominator) < 0.000001 ? 0 : numerator / denominator;
    }

    private static ushort BuildButtonMask(IReadOnlyList<bool> buttons)
    {
        var mask = 0;
        for (var index = 0; index < Math.Min(16, buttons.Count); index++)
        {
            if (buttons[index])
            {
                mask |= 1 << index;
            }
        }

        return (ushort)mask;
    }
}

public sealed class JoystickManualControlProjector
{
    public JoystickManualControlCommand Project(JoystickInputProjection projection)
    {
        return new JoystickManualControlCommand(
            ToManual(projection.Roll),
            ToManual(projection.Pitch),
            ToThrottle(projection.Throttle),
            ToManual(projection.Yaw),
            projection.ButtonMask,
            "MANUAL_CONTROL values projected from calibrated joystick input.");
    }

    private static short ToManual(double value)
    {
        return (short)Math.Clamp(Math.Round(value * 1000), -1000, 1000);
    }

    private static short ToThrottle(double value)
    {
        return (short)Math.Clamp(Math.Round(value * 1000), 0, 1000);
    }
}

public sealed class InMemoryJoystickService : IJoystickService
{
    private readonly Dictionary<string, JoystickRawState> _states = [];
    private IReadOnlyList<JoystickDevice> _devices = [];

    public void LoadDevices(IReadOnlyList<JoystickDevice> devices)
    {
        _devices = devices.ToArray();
    }

    public void SetRawState(JoystickDevice device, JoystickRawState state)
    {
        _states[device.Name] = state;
    }

    public Task<IReadOnlyList<JoystickDevice>> ScanAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_devices);
    }

    public Task<JoystickState?> ReadAsync(JoystickDevice device, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_states.TryGetValue(device.Name, out var state) || state.Axes.Count < 4)
        {
            return Task.FromResult<JoystickState?>(null);
        }

        return Task.FromResult<JoystickState?>(new JoystickState(
            state.Axes[0],
            state.Axes[1],
            state.Axes[2],
            state.Axes[3],
            state.Buttons.ToArray()));
    }
}

public sealed record JoystickRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class JoystickRuntimeEvidenceCatalog
{
    public IReadOnlyList<JoystickRuntimeEvidenceItem> Build()
    {
        return
        [
            new("INPUTTRAFFIC-271", "L1/L5", "Joystick scan/read boundary is testable through an in-memory service.", true),
            new("INPUTTRAFFIC-272", "L1/L5", "Joystick calibration maps raw axes to roll, pitch, yaw, throttle, and button masks.", true),
            new("INPUTTRAFFIC-273", "L1/L5", "MANUAL_CONTROL projection clamps calibrated values to MAVLink manual-control ranges.", true),
            new("INPUTTRAFFIC-278", "L0/L6", "Real HID joystick, Android controller, and operator tuning evidence remains deferred.", false)
        ];
    }
}
