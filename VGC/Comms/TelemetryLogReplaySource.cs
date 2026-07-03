using System.Buffers.Binary;
using VGC.Mavlink;

namespace VGC.Comms;

public interface ILogReplayFileParser
{
    IReadOnlyList<LogReplayPacket> Parse(ReadOnlySpan<byte> bytes);
}

public sealed class QgcTelemetryLogReplayParser : ILogReplayFileParser
{
    private const int TimestampByteCount = sizeof(ulong);

    public IReadOnlyList<LogReplayPacket> Parse(ReadOnlySpan<byte> bytes)
    {
        var parsed = new List<(ulong TimestampMicros, byte[] Frame)>();
        var offset = 0;

        while (offset < bytes.Length)
        {
            if (bytes.Length - offset < TimestampByteCount)
            {
                throw new FormatException("Telemetry log entry is missing timestamp bytes.");
            }

            var timestampMicros = ReadTimestampMicros(bytes.Slice(offset, TimestampByteCount));
            offset += TimestampByteCount;

            if (!TryGetMavlinkFrameLength(bytes[offset..], out var frameLength))
            {
                throw new FormatException("Telemetry log entry does not contain a complete MAVLink frame.");
            }

            var frame = bytes.Slice(offset, frameLength).ToArray();
            if (new MavlinkFrameParser().Parse(frame).Count == 0)
            {
                throw new FormatException("Telemetry log entry contains an invalid MAVLink frame.");
            }

            parsed.Add((timestampMicros, frame));
            offset += frameLength;
        }

        if (parsed.Count == 0)
        {
            return [];
        }

        var startMicros = parsed[0].TimestampMicros;
        return parsed
            .Select(packet => new LogReplayPacket(ToRelativeTimeSpan(startMicros, packet.TimestampMicros), packet.Frame))
            .ToList();
    }

    private static ulong ReadTimestampMicros(ReadOnlySpan<byte> bytes)
    {
        var bigEndian = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        var nowMicros = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000UL;
        if (bigEndian <= nowMicros)
        {
            return bigEndian;
        }

        var littleEndian = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        return littleEndian <= nowMicros ? littleEndian : bigEndian;
    }

    private static bool TryGetMavlinkFrameLength(ReadOnlySpan<byte> bytes, out int frameLength)
    {
        frameLength = 0;
        if (bytes.Length < 1)
        {
            return false;
        }

        return bytes[0] switch
        {
            0xFE => TryGetMavlinkV1FrameLength(bytes, out frameLength),
            0xFD => TryGetMavlinkV2FrameLength(bytes, out frameLength),
            _ => false
        };
    }

    private static bool TryGetMavlinkV1FrameLength(ReadOnlySpan<byte> bytes, out int frameLength)
    {
        frameLength = 0;
        if (bytes.Length < 2)
        {
            return false;
        }

        frameLength = 6 + bytes[1] + 2;
        return bytes.Length >= frameLength;
    }

    private static bool TryGetMavlinkV2FrameLength(ReadOnlySpan<byte> bytes, out int frameLength)
    {
        frameLength = 0;
        if (bytes.Length < 3)
        {
            return false;
        }

        var signatureLength = (bytes[2] & 0x01) == 0x01 ? 13 : 0;
        frameLength = 10 + bytes[1] + 2 + signatureLength;
        return bytes.Length >= frameLength;
    }

    private static TimeSpan ToRelativeTimeSpan(ulong startMicros, ulong timestampMicros)
    {
        if (timestampMicros <= startMicros)
        {
            return TimeSpan.Zero;
        }

        var relativeMicros = timestampMicros - startMicros;
        return relativeMicros > (ulong)(TimeSpan.MaxValue.Ticks / 10)
            ? TimeSpan.MaxValue
            : TimeSpan.FromTicks((long)relativeMicros * 10);
    }
}

public sealed class TelemetryLogReplaySource : ILogReplaySource
{
    private readonly string _filePath;
    private readonly ILogReplayFileParser _parser;

    public TelemetryLogReplaySource(string filePath, ILogReplayFileParser? parser = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Telemetry log file path is required.", nameof(filePath))
            : filePath;
        _parser = parser ?? new QgcTelemetryLogReplayParser();
    }

    public async Task<IReadOnlyList<LogReplayPacket>> ReadPacketsAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await File.ReadAllBytesAsync(_filePath, cancellationToken).ConfigureAwait(false);
        return _parser.Parse(bytes);
    }
}
