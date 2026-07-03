namespace VGC.Input;

public sealed record VirtualJoystickState(
    double LeftX,
    double LeftY,
    double RightX,
    double RightY,
    bool IsActive);

public sealed record VirtualJoystickManualControl(
    short Roll,
    short Pitch,
    short Throttle,
    short Yaw,
    ushort Buttons,
    string StatusText);

public sealed class VirtualJoystickRuntime
{
    private double _leftX;
    private double _leftY;
    private double _rightX;
    private double _rightY;
    private bool _leftActive;
    private bool _rightActive;
    private bool _enabled;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public bool IsActive => _leftActive || _rightActive;

    public VirtualJoystickState Snapshot => new(
        _leftX, _leftY, _rightX, _rightY, IsActive);

    public void UpdateLeftStick(double x, double y)
    {
        _leftX = Clamp(x);
        _leftY = Clamp(y);
        _leftActive = true;
    }

    public void UpdateRightStick(double x, double y)
    {
        _rightX = Clamp(x);
        _rightY = Clamp(y);
        _rightActive = true;
    }

    public void ReleaseLeftStick(bool springBack = true)
    {
        _leftActive = false;
        if (springBack)
        {
            _leftX = 0;
            // Throttle (leftY) does NOT spring back to center - stays at last position
        }
    }

    public void ReleaseRightStick()
    {
        _rightActive = false;
        _rightX = 0;
        _rightY = 0;
    }

    public VirtualJoystickManualControl ProjectManualControl()
    {
        if (!_enabled)
        {
            return new VirtualJoystickManualControl(0, 0, 0, 0, 0, "Virtual joystick disabled");
        }

        // Left stick: Throttle (Y up = more throttle) and Yaw (X = yaw)
        // Right stick: Pitch (Y up = pitch forward) and Roll (X = roll right)
        var throttle = (short)Math.Clamp(Math.Round((_leftY + 1) / 2.0 * 1000), 0, 1000);
        var yaw = (short)Math.Clamp(Math.Round(_leftX * 1000), -1000, 1000);
        var pitch = (short)Math.Clamp(Math.Round(_rightY * 1000), -1000, 1000);
        var roll = (short)Math.Clamp(Math.Round(_rightX * 1000), -1000, 1000);

        return new VirtualJoystickManualControl(
            roll, pitch, throttle, yaw, 0,
            $"Virtual joystick: T={throttle} Y={yaw} P={pitch} R={roll}");
    }

    public void Reset()
    {
        _leftX = 0;
        _leftY = -1; // Throttle at minimum
        _rightX = 0;
        _rightY = 0;
        _leftActive = false;
        _rightActive = false;
    }

    private static double Clamp(double value) => Math.Clamp(value, -1, 1);
}
