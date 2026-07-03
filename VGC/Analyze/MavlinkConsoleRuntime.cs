namespace VGC.Analyze;

public enum SerialConsoleState
{
    Disconnected,
    Connected,
    Failed
}

public sealed record SerialConsoleLine(
    string Text,
    bool IsCommand,
    DateTimeOffset Timestamp);

public sealed class SerialConsoleRuntime
{
    private readonly List<SerialConsoleLine> _lines = [];
    private readonly System.Text.StringBuilder _receiveBuffer = new();

    public SerialConsoleState State { get; private set; } = SerialConsoleState.Disconnected;

    public IReadOnlyList<SerialConsoleLine> Lines => _lines.ToArray();

    public void SendCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        State = SerialConsoleState.Connected;
        _lines.Add(new SerialConsoleLine(command.Trim(), IsCommand: true, DateTimeOffset.Now));
    }

    public void HandleSerialControlMessage(byte[] data)
    {
        if (data.Length == 0)
        {
            return;
        }

        State = SerialConsoleState.Connected;

        // SERIAL_CONTROL payload: flags(1) + timeout(2) + baudrate(4) + port(1) + count(1) + data(70)
        // For NSH console, the response data starts at offset 9 with length in count field.
        // However, callers may pass just the text data bytes directly.
        var text = System.Text.Encoding.UTF8.GetString(data);

        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                var line = _receiveBuffer.ToString();
                _receiveBuffer.Clear();
                if (!string.IsNullOrEmpty(line))
                {
                    _lines.Add(new SerialConsoleLine(line, IsCommand: false, DateTimeOffset.Now));
                }
            }
            else if (ch != '\r')
            {
                _receiveBuffer.Append(ch);
            }
        }

        // Flush any remaining partial line
        if (_receiveBuffer.Length > 0)
        {
            _lines.Add(new SerialConsoleLine(_receiveBuffer.ToString(), IsCommand: false, DateTimeOffset.Now));
            _receiveBuffer.Clear();
        }
    }

    public void Clear()
    {
        _lines.Clear();
        _receiveBuffer.Clear();
    }

    public void SetFailed()
    {
        State = SerialConsoleState.Failed;
    }
}
