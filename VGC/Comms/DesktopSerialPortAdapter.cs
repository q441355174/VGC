using System.IO.Ports;

namespace VGC.Comms;

public sealed class DesktopSerialPortAdapter : ISerialPortAdapter
{
    private SerialPort? _port;
    private readonly object _syncRoot = new();

    public event EventHandler<byte[]>? BytesReceived;

    public bool IsOpen => _port?.IsOpen == true;

    public Task OpenAsync(SerialLinkConfiguration configuration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsOpen)
        {
            return Task.CompletedTask;
        }

        var port = new SerialPort(configuration.PortName, configuration.BaudRate)
        {
            DataBits = configuration.DataBits,
            Parity = Enum.Parse<Parity>(configuration.Parity),
            StopBits = Enum.Parse<StopBits>(configuration.StopBits),
            ReadTimeout = 500,
            WriteTimeout = 500
        };
        port.DataReceived += OnDataReceived;
        port.Open();
        _port = port;
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        var port = _port;
        if (port is null)
        {
            return Task.CompletedTask;
        }

        port.DataReceived -= OnDataReceived;
        if (port.IsOpen)
        {
            port.Close();
        }

        port.Dispose();
        _port = null;
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var port = _port ?? throw new InvalidOperationException("Serial port is not open.");
        var array = bytes.ToArray();
        lock (_syncRoot)
        {
            port.Write(array, 0, array.Length);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs args)
    {
        var port = _port;
        if (port is null || !port.IsOpen)
        {
            return;
        }

        var count = port.BytesToRead;
        if (count <= 0)
        {
            return;
        }

        var buffer = new byte[count];
        var read = port.Read(buffer, 0, buffer.Length);
        if (read > 0)
        {
            BytesReceived?.Invoke(this, buffer[..read]);
        }
    }
}
