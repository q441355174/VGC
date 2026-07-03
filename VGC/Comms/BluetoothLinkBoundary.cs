namespace VGC.Comms;

public enum BluetoothPlatformSupport
{
    Supported,
    Unsupported,
    PermissionRequired,
    RadioDisabled
}

public sealed record BluetoothCapabilityMatrix(
    BluetoothPlatformSupport Desktop,
    BluetoothPlatformSupport Android,
    string? DesktopReason = null,
    string? AndroidReason = null)
{
    public static BluetoothCapabilityMatrix Unsupported(string reason)
    {
        return new BluetoothCapabilityMatrix(
            BluetoothPlatformSupport.Unsupported,
            BluetoothPlatformSupport.Unsupported,
            reason,
            reason);
    }
}

public sealed record BluetoothDeviceDescriptor(
    string Id,
    string Name,
    string? Address,
    bool IsPaired,
    bool CanConnect);

public interface IBluetoothLinkTransport : ILinkTransport
{
    BluetoothDeviceDescriptor Device { get; }
}

public interface IBluetoothLinkPlatform
{
    BluetoothCapabilityMatrix Capabilities { get; }

    Task<IReadOnlyList<BluetoothDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);
}

public sealed class UnavailableBluetoothLinkPlatform : IBluetoothLinkPlatform
{
    private readonly string _reason;

    public UnavailableBluetoothLinkPlatform(string reason)
    {
        _reason = reason;
        Capabilities = BluetoothCapabilityMatrix.Unsupported(reason);
    }

    public BluetoothCapabilityMatrix Capabilities { get; }

    public Task<IReadOnlyList<BluetoothDeviceDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<BluetoothDeviceDescriptor>>([]);
    }

    public string UnavailableReason => _reason;
}
