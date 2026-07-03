using VGC.Mavlink;

namespace VGC.Analyze;

public sealed record MavlinkInspectorRow(
    byte SystemId,
    byte ComponentId,
    uint MessageId,
    string MessageName,
    string FieldSummary,
    string? Severity,
    string? Text,
    uint Count,
    double RateHz,
    DateTimeOffset LastSeenAt);

public sealed record MavlinkInspectorFilter(
    uint? MessageId = null,
    string? MessageName = null,
    byte? SystemId = null,
    byte? ComponentId = null,
    string? Text = null,
    string? Severity = null)
{
    public bool IsEmpty =>
        MessageId is null &&
        string.IsNullOrWhiteSpace(MessageName) &&
        SystemId is null &&
        ComponentId is null &&
        string.IsNullOrWhiteSpace(Text) &&
        string.IsNullOrWhiteSpace(Severity);
}

public sealed record MavlinkInspectorDecode(
    string MessageName,
    string FieldSummary,
    string? Severity = null,
    string? Text = null);

public sealed class MavlinkInspector
{
    private readonly Dictionary<(byte SystemId, byte ComponentId, uint MessageId), MavlinkInspectorAccumulator> _rows = [];
    private readonly Func<DateTimeOffset> _clock;

    public MavlinkInspector(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public IReadOnlyList<MavlinkInspectorRow> Rows => GetRows();

    public IReadOnlyList<MavlinkInspectorRow> GetRows(MavlinkInspectorFilter? filter = null)
    {
        var rows = _rows
        .Values
        .Select(static row => row.ToRow())
        .OrderBy(static row => row.SystemId)
        .ThenBy(static row => row.ComponentId)
        .ThenBy(static row => row.MessageId)
        .ToList();

        return filter is null || filter.IsEmpty
            ? rows
            : rows.Where(row => Matches(row, filter)).ToList();
    }

    public void Attach(MavlinkProtocol protocol)
    {
        protocol.PacketReceived += (_, packet) => Observe(packet);
    }

    public void Observe(MavlinkPacket packet)
    {
        var key = (packet.SystemId, packet.ComponentId, packet.MessageId);
        var now = _clock();
        if (!_rows.TryGetValue(key, out var row))
        {
            _rows.Add(key, new MavlinkInspectorAccumulator(packet.SystemId, packet.ComponentId, packet.MessageId, now, MavlinkInspectorDecoder.Decode(packet)));
            return;
        }

        row.MarkSeen(now, MavlinkInspectorDecoder.Decode(packet));
    }

    private static bool Matches(MavlinkInspectorRow row, MavlinkInspectorFilter filter)
    {
        if (filter.MessageId is not null && row.MessageId != filter.MessageId.Value)
        {
            return false;
        }

        if (filter.SystemId is not null && row.SystemId != filter.SystemId.Value)
        {
            return false;
        }

        if (filter.ComponentId is not null && row.ComponentId != filter.ComponentId.Value)
        {
            return false;
        }

        if (!TextContains(row.MessageName, filter.MessageName))
        {
            return false;
        }

        if (!TextContains(row.Severity, filter.Severity))
        {
            return false;
        }

        return RowContains(row.MessageName) || RowContains(row.FieldSummary) || RowContains(row.Severity) || RowContains(row.Text);

        bool RowContains(string? value)
        {
            return TextContains(value, filter.Text);
        }
    }

    private static bool TextContains(string? value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
            (!string.IsNullOrWhiteSpace(value) && value.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class MavlinkInspectorAccumulator
    {
        private readonly DateTimeOffset _firstSeenAt;

        public MavlinkInspectorAccumulator(byte systemId, byte componentId, uint messageId, DateTimeOffset firstSeenAt, MavlinkInspectorDecode decode)
        {
            SystemId = systemId;
            ComponentId = componentId;
            MessageId = messageId;
            _firstSeenAt = firstSeenAt;
            LastSeenAt = firstSeenAt;
            Count = 1;
            Decode = decode;
        }

        public byte SystemId { get; }

        public byte ComponentId { get; }

        public uint MessageId { get; }

        public uint Count { get; private set; }

        public DateTimeOffset LastSeenAt { get; private set; }

        public MavlinkInspectorDecode Decode { get; private set; }

        public void MarkSeen(DateTimeOffset seenAt, MavlinkInspectorDecode decode)
        {
            Count++;
            LastSeenAt = seenAt;
            Decode = decode;
        }

        public MavlinkInspectorRow ToRow()
        {
            var elapsedSeconds = Math.Max((LastSeenAt - _firstSeenAt).TotalSeconds, 0);
            var rate = elapsedSeconds <= 0 ? 0 : Count / elapsedSeconds;
            return new MavlinkInspectorRow(
                SystemId,
                ComponentId,
                MessageId,
                Decode.MessageName,
                Decode.FieldSummary,
                Decode.Severity,
                Decode.Text,
                Count,
                rate,
                LastSeenAt);
        }
    }
}

public static class MavlinkInspectorDecoder
{
    public static MavlinkInspectorDecode Decode(MavlinkPacket packet)
    {
        return packet.MessageId switch
        {
            0 => DecodeHeartbeat(packet),
            22 => DecodeParamValue(packet),
            43 => DecodeMissionRequestList(packet),
            44 => DecodeMissionCount(packet),
            47 => DecodeMissionAck(packet),
            51 => DecodeMissionRequestInt(packet),
            73 => DecodeMissionItemInt(packet),
            77 => DecodeCommandAck(packet),
            253 => DecodeStatusText(packet),
            _ => new MavlinkInspectorDecode($"MSG_{packet.MessageId}", $"payload={packet.Payload.Length} bytes")
        };
    }

    private static MavlinkInspectorDecode DecodeHeartbeat(MavlinkPacket packet)
    {
        if (packet.Payload.Length < 9)
        {
            return new MavlinkInspectorDecode("HEARTBEAT", $"payload={packet.Payload.Length} bytes");
        }

        var customMode = BitConverter.ToUInt32(packet.Payload, 0);
        var vehicleType = (Vehicles.MavType)packet.Payload[4];
        var autopilot = (Vehicles.MavAutopilot)packet.Payload[5];
        var baseMode = packet.Payload[6];
        var status = packet.Payload[7];
        return new MavlinkInspectorDecode(
            "HEARTBEAT",
            $"type={vehicleType}; autopilot={autopilot}; baseMode={baseMode}; customMode={customMode}; status={status}");
    }

    private static MavlinkInspectorDecode DecodeParamValue(MavlinkPacket packet)
    {
        if (packet.Payload.Length < 25)
        {
            return new MavlinkInspectorDecode("PARAM_VALUE", $"payload={packet.Payload.Length} bytes");
        }

        var value = BitConverter.ToSingle(packet.Payload, 0);
        var count = BitConverter.ToUInt16(packet.Payload, 4);
        var index = BitConverter.ToUInt16(packet.Payload, 6);
        var nameBytes = packet.Payload.AsSpan(8, 16);
        var terminator = nameBytes.IndexOf((byte)0);
        var name = System.Text.Encoding.ASCII.GetString(terminator >= 0 ? nameBytes[..terminator] : nameBytes).TrimEnd();
        var type = (MavlinkParamType)packet.Payload[24];
        return new MavlinkInspectorDecode("PARAM_VALUE", $"{name}={value:G}; index={index}/{count}; type={type}", Text: name);
    }

    private static MavlinkInspectorDecode DecodeCommandAck(MavlinkPacket packet)
    {
        if (!MavlinkCommandService.TryReadCommandAck(packet, out var ack))
        {
            return new MavlinkInspectorDecode("COMMAND_ACK", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("COMMAND_ACK", $"command={ack.Command}; result={ack.Result}", Text: ack.Result.ToString());
    }

    private static MavlinkInspectorDecode DecodeStatusText(MavlinkPacket packet)
    {
        if (!MavlinkStatusTextParser.TryRead(packet, out var statusText))
        {
            return new MavlinkInspectorDecode("STATUSTEXT", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("STATUSTEXT", statusText.Text, statusText.Severity.ToString(), statusText.Text);
    }

    private static MavlinkInspectorDecode DecodeMissionRequestList(MavlinkPacket packet)
    {
        if (!MavlinkMissionService.TryReadMissionRequestList(packet, out var request))
        {
            return new MavlinkInspectorDecode("MISSION_REQUEST_LIST", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("MISSION_REQUEST_LIST", $"target={request.TargetSystemId}/{request.TargetComponentId}; type={request.MissionType}", Text: request.MissionType.ToString());
    }

    private static MavlinkInspectorDecode DecodeMissionCount(MavlinkPacket packet)
    {
        if (!MavlinkMissionService.TryReadMissionCount(packet, out var count))
        {
            return new MavlinkInspectorDecode("MISSION_COUNT", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("MISSION_COUNT", $"target={count.TargetSystemId}/{count.TargetComponentId}; count={count.Count}; type={count.MissionType}", Text: count.MissionType.ToString());
    }

    private static MavlinkInspectorDecode DecodeMissionAck(MavlinkPacket packet)
    {
        if (!MavlinkMissionService.TryReadMissionAck(packet, out var ack))
        {
            return new MavlinkInspectorDecode("MISSION_ACK", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("MISSION_ACK", $"target={ack.TargetSystemId}/{ack.TargetComponentId}; result={ack.Result}; type={ack.MissionType}", Text: $"{ack.Result} {ack.MissionType}");
    }

    private static MavlinkInspectorDecode DecodeMissionRequestInt(MavlinkPacket packet)
    {
        if (!MavlinkMissionService.TryReadMissionRequestInt(packet, out var request))
        {
            return new MavlinkInspectorDecode("MISSION_REQUEST_INT", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("MISSION_REQUEST_INT", $"target={request.TargetSystemId}/{request.TargetComponentId}; seq={request.Sequence}; type={request.MissionType}", Text: request.MissionType.ToString());
    }

    private static MavlinkInspectorDecode DecodeMissionItemInt(MavlinkPacket packet)
    {
        if (!MavlinkMissionService.TryReadMissionItemInt(packet, out var item))
        {
            return new MavlinkInspectorDecode("MISSION_ITEM_INT", $"payload={packet.Payload.Length} bytes");
        }

        return new MavlinkInspectorDecode("MISSION_ITEM_INT", $"seq={item.Sequence}; command={item.Command}; frame={item.Frame}; type={item.MissionType}", Text: item.MissionType.ToString());
    }
}
