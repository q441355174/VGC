namespace VGC.Payload;

public enum GimbalControlMode
{
    Free,
    Follow,
    Lock,
    ROI
}

public sealed record GimbalTouchState(
    double PanDegrees,
    double TiltDegrees,
    bool IsActive,
    GimbalControlMode Mode)
{
    public static GimbalTouchState Idle { get; } = new(0, 0, false, GimbalControlMode.Free);

    public string StatusText => IsActive
        ? $"Gimbal touch active | Pan {PanDegrees:F1} Tilt {TiltDegrees:F1}"
        : $"Gimbal touch idle | {Mode}";
}

public sealed class GimbalTouchControl
{
    private double _panDegrees;
    private double _tiltDegrees;
    private bool _isActive;
    private GimbalControlMode _mode = GimbalControlMode.Free;

    private double _startX;
    private double _startY;
    private double _lastX;
    private double _lastY;

    private double _panSensitivity = 0.5;
    private double _tiltSensitivity = 0.5;

    public event EventHandler<GimbalCommand>? GimbalCommandRequested;

    public GimbalTouchState State => BuildSnapshot();

    public double PanSensitivity
    {
        get => _panSensitivity;
        set => _panSensitivity = Math.Clamp(value, 0.05, 5.0);
    }

    public double TiltSensitivity
    {
        get => _tiltSensitivity;
        set => _tiltSensitivity = Math.Clamp(value, 0.05, 5.0);
    }

    public GimbalControlMode Mode
    {
        get => _mode;
        set => _mode = value;
    }

    public GimbalTouchState BeginTouch(double x, double y)
    {
        _startX = x;
        _startY = y;
        _lastX = x;
        _lastY = y;
        _isActive = true;
        return BuildSnapshot();
    }

    public GimbalTouchState MoveTouch(double x, double y)
    {
        if (!_isActive)
        {
            return BuildSnapshot();
        }

        var deltaX = (x - _lastX) * _panSensitivity;
        var deltaY = (y - _lastY) * _tiltSensitivity;

        _panDegrees = ClampDegrees(_panDegrees + deltaX, -180, 180);
        _tiltDegrees = ClampDegrees(_tiltDegrees - deltaY, -90, 90);

        _lastX = x;
        _lastY = y;

        var command = new GimbalCommand(
            _tiltDegrees,
            _panDegrees,
            LockYaw: _mode == GimbalControlMode.Lock);

        GimbalCommandRequested?.Invoke(this, command);

        return BuildSnapshot();
    }

    public GimbalTouchState EndTouch()
    {
        _isActive = false;
        return BuildSnapshot();
    }

    public GimbalTouchState ResetPosition()
    {
        _panDegrees = 0;
        _tiltDegrees = 0;
        _isActive = false;

        var command = new GimbalCommand(0, 0, LockYaw: false);
        GimbalCommandRequested?.Invoke(this, command);

        return BuildSnapshot();
    }

    private GimbalTouchState BuildSnapshot()
    {
        return new GimbalTouchState(_panDegrees, _tiltDegrees, _isActive, _mode);
    }

    private static double ClampDegrees(double value, double min, double max)
    {
        return Math.Clamp(value, min, max);
    }
}
