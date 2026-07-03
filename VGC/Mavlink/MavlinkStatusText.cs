using System.Text;

namespace VGC.Mavlink;

public enum MavlinkSeverity : byte
{
    Emergency = 0,
    Alert = 1,
    Critical = 2,
    Error = 3,
    Warning = 4,
    Notice = 5,
    Info = 6,
    Debug = 7
}

public sealed record MavlinkStatusText(
    byte SystemId,
    byte ComponentId,
    MavlinkSeverity Severity,
    string Text);

public static class MavlinkStatusTextParser
{
    public static bool TryRead(MavlinkPacket packet, out MavlinkStatusText statusText)
    {
        statusText = default!;
        if (packet.MessageId != 253 || packet.Payload.Length < 51)
        {
            return false;
        }

        var textBytes = packet.Payload.AsSpan(1, 50);
        var terminatorIndex = textBytes.IndexOf((byte)0);
        var text = Encoding.ASCII.GetString(terminatorIndex >= 0 ? textBytes[..terminatorIndex] : textBytes).TrimEnd();
        statusText = new MavlinkStatusText(packet.SystemId, packet.ComponentId, (MavlinkSeverity)packet.Payload[0], text);
        return true;
    }
}
