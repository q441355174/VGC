namespace VGC.Input;

public sealed record JoystickDevice(string Name, int Axes, int Buttons);

public sealed record JoystickState(double Roll, double Pitch, double Yaw, double Throttle, bool[] Buttons);

public interface IJoystickService
{
    Task<IReadOnlyList<JoystickDevice>> ScanAsync(CancellationToken cancellationToken = default);
    Task<JoystickState?> ReadAsync(JoystickDevice device, CancellationToken cancellationToken = default);
}
