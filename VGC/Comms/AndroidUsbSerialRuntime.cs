namespace VGC.Comms;

public enum AndroidUsbSerialState
{
    Idle,
    Discovering,
    PermissionRequired,
    Connecting,
    Connected,
    Disconnected,
    Failed
}

public sealed record AndroidUsbSerialDevice(
    string DeviceId,
    string DisplayName,
    int VendorId,
    int ProductId,
    bool HasPermission);

public sealed record AndroidUsbSerialSnapshot(
    AndroidUsbSerialState State,
    IReadOnlyList<AndroidUsbSerialDevice> Devices,
    AndroidUsbSerialDevice? SelectedDevice,
    string? LastError);

public interface IAndroidUsbSerialPlatform
{
    Task<IReadOnlyList<AndroidUsbSerialDevice>> DiscoverDevicesAsync(CancellationToken cancellationToken = default);

    Task<bool> RequestPermissionAsync(AndroidUsbSerialDevice device, CancellationToken cancellationToken = default);

    Task ConnectAsync(AndroidUsbSerialDevice device, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

public sealed class AndroidUsbSerialRuntime
{
    private readonly IAndroidUsbSerialPlatform _platform;
    private IReadOnlyList<AndroidUsbSerialDevice> _devices = [];

    public AndroidUsbSerialRuntime(IAndroidUsbSerialPlatform platform)
    {
        _platform = platform;
    }

    public AndroidUsbSerialState State { get; private set; } = AndroidUsbSerialState.Idle;

    public AndroidUsbSerialDevice? SelectedDevice { get; private set; }

    public string? LastError { get; private set; }

    public IReadOnlyList<AndroidUsbSerialDevice> Devices => _devices;

    public async Task<IReadOnlyList<AndroidUsbSerialDevice>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        State = AndroidUsbSerialState.Discovering;
        LastError = null;
        _devices = await _platform.DiscoverDevicesAsync(cancellationToken).ConfigureAwait(false);
        SelectedDevice = _devices.FirstOrDefault();
        State = SelectedDevice is null
            ? AndroidUsbSerialState.Disconnected
            : SelectedDevice.HasPermission
                ? AndroidUsbSerialState.Disconnected
                : AndroidUsbSerialState.PermissionRequired;
        return _devices;
    }

    public async Task<bool> RequestPermissionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedDevice is null)
        {
            Fail("No USB serial device selected.");
            return false;
        }

        State = AndroidUsbSerialState.PermissionRequired;
        var granted = await _platform.RequestPermissionAsync(SelectedDevice, cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            Fail("USB serial permission denied.");
            return false;
        }

        SelectedDevice = SelectedDevice with { HasPermission = true };
        _devices = _devices.Select(device => device.DeviceId == SelectedDevice.DeviceId ? SelectedDevice : device).ToArray();
        State = AndroidUsbSerialState.Disconnected;
        LastError = null;
        return true;
    }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedDevice is null)
        {
            Fail("No USB serial device selected.");
            return false;
        }

        if (!SelectedDevice.HasPermission)
        {
            State = AndroidUsbSerialState.PermissionRequired;
            LastError = "USB serial permission required.";
            return false;
        }

        try
        {
            State = AndroidUsbSerialState.Connecting;
            await _platform.ConnectAsync(SelectedDevice, cancellationToken).ConfigureAwait(false);
            State = AndroidUsbSerialState.Connected;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
            return false;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _platform.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        State = AndroidUsbSerialState.Disconnected;
    }

    public AndroidUsbSerialSnapshot Capture()
    {
        return new AndroidUsbSerialSnapshot(State, _devices, SelectedDevice, LastError);
    }

    private void Fail(string error)
    {
        State = AndroidUsbSerialState.Failed;
        LastError = error;
    }
}
