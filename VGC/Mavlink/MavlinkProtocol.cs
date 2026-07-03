using VGC.Comms;

namespace VGC.Mavlink;

public sealed class MavlinkProtocol
{
    private readonly Dictionary<ILinkTransport, MavlinkFrameParser> _parsers = new();
    private readonly MavlinkStatisticsTracker _statistics = new();

    public event EventHandler<MavlinkPacket>? PacketReceived;

    public event EventHandler<MavlinkHeartbeat>? HeartbeatReceived;

    public event EventHandler<MavlinkCommandAck>? CommandAckReceived;

    public event EventHandler<MavlinkStatusText>? StatusTextReceived;

    public uint PacketsReceived { get; private set; }

    public MavlinkStatisticsTracker Statistics => _statistics;

    public void Attach(LinkManager linkManager)
    {
        linkManager.BytesReceived += OnBytesReceived;
    }

    private void OnBytesReceived(object? sender, BytesReceivedEventArgs args)
    {
        if (!_parsers.TryGetValue(args.Link, out var parser))
        {
            parser = new MavlinkFrameParser();
            _parsers.Add(args.Link, parser);
        }

        foreach (var parsed in parser.Parse(args.Bytes))
        {
            var packet = new MavlinkPacket(args.Link, parsed.Version, parsed.SystemId, parsed.ComponentId, parsed.MessageId, parsed.Payload);
            PacketsReceived++;
            _statistics.RecordPacket(parsed.MessageId, parsed.Sequence, args.Link.Configuration.Name);
            PacketReceived?.Invoke(this, packet);

            if (TryReadHeartbeat(packet, out var heartbeat))
            {
                HeartbeatReceived?.Invoke(this, heartbeat);
            }

            if (MavlinkCommandService.TryReadCommandAck(packet, out var ack))
            {
                CommandAckReceived?.Invoke(this, ack);
            }

            if (MavlinkStatusTextParser.TryRead(packet, out var statusText))
            {
                StatusTextReceived?.Invoke(this, statusText);
            }
        }
    }

    private static bool TryReadHeartbeat(MavlinkPacket packet, out MavlinkHeartbeat heartbeat)
    {
        heartbeat = default!;
        if (packet.MessageId != 0 || packet.Payload.Length < 9)
        {
            return false;
        }

        var customMode = BitConverter.ToUInt32(packet.Payload, 0);
        var vehicleType = (Vehicles.MavType)packet.Payload[4];
        var autopilot = (Vehicles.MavAutopilot)packet.Payload[5];
        var baseMode = packet.Payload[6];
        var systemStatus = packet.Payload[7];
        heartbeat = new MavlinkHeartbeat(packet.SystemId, packet.ComponentId, autopilot, vehicleType, baseMode, customMode, systemStatus);
        return true;
    }
}
