namespace VGC.Input;

public enum JoystickAxisFunction
{
    Roll,
    Pitch,
    Yaw,
    Throttle
}

public sealed record JoystickAxisMapping(
    int AxisIndex,
    JoystickAxisFunction Function,
    bool Reversed,
    double Deadband,
    double ExponentialCurve);

public sealed record JoystickButtonMapping(
    int ButtonIndex,
    string Action);

public sealed record JoystickConfigSnapshot(
    IReadOnlyList<JoystickAxisMapping> AxisMappings,
    IReadOnlyList<JoystickButtonMapping> ButtonMappings,
    JoystickDevice? SelectedDevice,
    string StatusText);

public sealed class JoystickConfigRuntime
{
    private static readonly string[] KnownActions =
    [
        "Arm", "Disarm", "RTL", "Land", "Loiter", "Auto", "Stabilize",
        "CameraCapture", "CameraToggle", "GimbalUp", "GimbalDown",
        "MountCenter", "TriggerServo", "ToggleFence"
    ];

    private readonly Dictionary<JoystickAxisFunction, JoystickAxisMapping> _axisMappings = [];
    private readonly Dictionary<int, JoystickButtonMapping> _buttonMappings = [];
    private JoystickDevice? _selectedDevice;

    public JoystickConfigSnapshot Snapshot => BuildSnapshot();

    public JoystickConfigSnapshot LoadDevice(JoystickDevice device)
    {
        _selectedDevice = device;
        _axisMappings.Clear();
        _buttonMappings.Clear();

        if (device.Axes >= 4)
        {
            _axisMappings[JoystickAxisFunction.Roll] = new JoystickAxisMapping(0, JoystickAxisFunction.Roll, false, 0.05, 0.0);
            _axisMappings[JoystickAxisFunction.Pitch] = new JoystickAxisMapping(1, JoystickAxisFunction.Pitch, false, 0.05, 0.0);
            _axisMappings[JoystickAxisFunction.Yaw] = new JoystickAxisMapping(2, JoystickAxisFunction.Yaw, false, 0.05, 0.0);
            _axisMappings[JoystickAxisFunction.Throttle] = new JoystickAxisMapping(3, JoystickAxisFunction.Throttle, false, 0.05, 0.0);
        }

        return BuildSnapshot();
    }

    public JoystickConfigSnapshot MapAxis(int axisIndex, JoystickAxisFunction function, bool reversed = false)
    {
        if (_selectedDevice is null)
        {
            return BuildSnapshot();
        }

        if (axisIndex < 0 || axisIndex >= _selectedDevice.Axes)
        {
            return BuildSnapshot();
        }

        var existing = _axisMappings.TryGetValue(function, out var current) ? current : null;
        var deadband = existing?.Deadband ?? 0.05;
        var expo = existing?.ExponentialCurve ?? 0.0;

        _axisMappings[function] = new JoystickAxisMapping(axisIndex, function, reversed, deadband, expo);
        return BuildSnapshot();
    }

    public JoystickConfigSnapshot MapButton(int buttonIndex, string action)
    {
        if (_selectedDevice is null)
        {
            return BuildSnapshot();
        }

        if (buttonIndex < 0 || buttonIndex >= _selectedDevice.Buttons)
        {
            return BuildSnapshot();
        }

        _buttonMappings[buttonIndex] = new JoystickButtonMapping(buttonIndex, action);
        return BuildSnapshot();
    }

    public JoystickConfigSnapshot SetDeadband(JoystickAxisFunction function, double deadband)
    {
        if (!_axisMappings.TryGetValue(function, out var mapping))
        {
            return BuildSnapshot();
        }

        _axisMappings[function] = mapping with { Deadband = Math.Clamp(deadband, 0.0, 0.5) };
        return BuildSnapshot();
    }

    public JoystickConfigSnapshot SetExponential(JoystickAxisFunction function, double exponentialCurve)
    {
        if (!_axisMappings.TryGetValue(function, out var mapping))
        {
            return BuildSnapshot();
        }

        _axisMappings[function] = mapping with { ExponentialCurve = Math.Clamp(exponentialCurve, 0.0, 1.0) };
        return BuildSnapshot();
    }

    private JoystickConfigSnapshot BuildSnapshot()
    {
        var axes = _axisMappings.Values
            .OrderBy(static m => m.Function)
            .ToArray();

        var buttons = _buttonMappings.Values
            .OrderBy(static m => m.ButtonIndex)
            .ToArray();

        var status = _selectedDevice is null
            ? "No joystick device selected"
            : $"{_selectedDevice.Name}: {axes.Length} axes mapped, {buttons.Length} buttons mapped";

        return new JoystickConfigSnapshot(axes, buttons, _selectedDevice, status);
    }
}
