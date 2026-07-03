using System.IO.Ports;

namespace VGC.Comms;

public sealed class DesktopSerialPortEnumerator : ISerialPortEnumerator
{
    public Task<IReadOnlyList<SerialPortInfo>> EnumerateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<SerialPortInfo> ports = SerialPort.GetPortNames()
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(static name => new SerialPortInfo(name, name, 0, 0))
            .ToArray();
        return Task.FromResult(ports);
    }
}
