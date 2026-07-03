using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Analyze;

public sealed record ReplayPacketIndexRow(
    int Sequence,
    TimeSpan Timestamp,
    TimeSpan Delta,
    byte? SystemId,
    byte? ComponentId,
    uint? MessageId,
    string MessageName,
    string FieldSummary,
    int ByteCount,
    bool IsValidMavlink);

public sealed record ReplayMessageRateRow(
    uint MessageId,
    string MessageName,
    int Count,
    double RateHz);

public sealed record ReplayGapRow(
    int PreviousSequence,
    int NextSequence,
    TimeSpan Start,
    TimeSpan End,
    TimeSpan Duration);

public sealed record ReplayTimelineProjection(
    TimeSpan Duration,
    int PacketCount,
    double AverageRateHz,
    IReadOnlyList<ReplayPacketIndexRow> Packets,
    IReadOnlyList<ReplayMessageRateRow> MessageRates,
    IReadOnlyList<ReplayGapRow> Gaps)
{
    public static ReplayTimelineProjection Empty { get; } = new(
        TimeSpan.Zero,
        0,
        0,
        [],
        [],
        []);
}

public sealed class ReplayTimelineBuilder
{
    private readonly TimeSpan _gapThreshold;

    public ReplayTimelineBuilder(TimeSpan? gapThreshold = null)
    {
        _gapThreshold = gapThreshold ?? TimeSpan.FromSeconds(2);
    }

    public ReplayTimelineProjection Build(IEnumerable<LogReplayPacket> packets)
    {
        var ordered = packets
            .OrderBy(static packet => packet.Timestamp)
            .ToList();

        if (ordered.Count == 0)
        {
            return ReplayTimelineProjection.Empty;
        }

        var indexRows = new List<ReplayPacketIndexRow>(ordered.Count);
        var gaps = new List<ReplayGapRow>();
        var parser = new MavlinkFrameParser();
        var previousTimestamp = TimeSpan.Zero;

        for (var index = 0; index < ordered.Count; index++)
        {
            var packet = ordered[index];
            var delta = index == 0 ? TimeSpan.Zero : packet.Timestamp - previousTimestamp;
            if (index > 0 && delta > _gapThreshold)
            {
                gaps.Add(new ReplayGapRow(
                    PreviousSequence: index - 1,
                    NextSequence: index,
                    Start: previousTimestamp,
                    End: packet.Timestamp,
                    Duration: delta));
            }

            indexRows.Add(BuildIndexRow(index, packet, delta, parser));
            previousTimestamp = packet.Timestamp;
        }

        var duration = ordered[^1].Timestamp;
        var averageRate = duration <= TimeSpan.Zero ? ordered.Count : ordered.Count / duration.TotalSeconds;
        var rates = indexRows
            .Where(static row => row.MessageId is not null)
            .GroupBy(static row => new { MessageId = row.MessageId!.Value, row.MessageName })
            .OrderBy(static group => group.Key.MessageId)
            .Select(group => new ReplayMessageRateRow(
                group.Key.MessageId,
                group.Key.MessageName,
                group.Count(),
                duration <= TimeSpan.Zero ? group.Count() : group.Count() / duration.TotalSeconds))
            .ToList();

        return new ReplayTimelineProjection(duration, ordered.Count, averageRate, indexRows, rates, gaps);
    }

    private static ReplayPacketIndexRow BuildIndexRow(int sequence, LogReplayPacket replayPacket, TimeSpan delta, MavlinkFrameParser parser)
    {
        var frames = parser.Parse(replayPacket.Bytes);
        if (frames.Count == 0)
        {
            return new ReplayPacketIndexRow(
                sequence,
                replayPacket.Timestamp,
                delta,
                null,
                null,
                null,
                "INVALID",
                "Not a complete MAVLink frame",
                replayPacket.Bytes.Length,
                false);
        }

        var frame = frames[0];
        var packet = new MavlinkPacket(new ReplayTimelineLinkTransport(), frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload);
        var decode = MavlinkInspectorDecoder.Decode(packet);
        return new ReplayPacketIndexRow(
            sequence,
            replayPacket.Timestamp,
            delta,
            frame.SystemId,
            frame.ComponentId,
            frame.MessageId,
            decode.MessageName,
            decode.FieldSummary,
            replayPacket.Bytes.Length,
            true);
    }

    private sealed class ReplayTimelineLinkTransport : ILinkTransport
    {
#pragma warning disable CS0067
        public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

        public event EventHandler<BytesReceivedEventArgs>? BytesSent;

        public event EventHandler<string>? CommunicationError;
#pragma warning restore CS0067

        public LinkConfiguration Configuration { get; } = new LogReplayLinkConfiguration("Timeline", "timeline.tlog");

        public bool IsConnected => false;

        public bool CanSend => false;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
