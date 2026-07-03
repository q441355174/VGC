namespace VGC.Mavlink;

public sealed record MavlinkStatisticsSnapshot(
    uint TotalPacketsReceived,
    uint TotalPacketsLost,
    double PacketLossPercent,
    IReadOnlyList<MavlinkMessageStats> MessageStats,
    IReadOnlyList<MavlinkLinkStats> LinkStats);

public sealed record MavlinkMessageStats(
    uint MessageId,
    uint Count,
    double RateHz,
    DateTimeOffset LastSeenAt);

public sealed record MavlinkLinkStats(
    string LinkId,
    uint TotalPacketsReceived,
    uint PacketsLost,
    double PacketLossPercent,
    byte? LastSequence,
    DateTimeOffset LastSeenAt);

public sealed class MavlinkStatisticsTracker
{
    private readonly Dictionary<uint, MavlinkMessageAccumulator> _stats = [];
    private readonly Dictionary<string, MavlinkLinkAccumulator> _linkStats = [];
    private readonly Func<DateTimeOffset> _clock;
    private readonly DateTimeOffset _startedAt;

    public MavlinkStatisticsTracker(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _startedAt = _clock();
    }

    public uint TotalReceived { get; private set; }

    public uint TotalLost { get; private set; }

    public void RecordPacket(uint messageId)
    {
        RecordPacket(messageId, sequence: null, linkId: "default");
    }

    public void RecordPacket(uint messageId, byte sequence, string linkId)
    {
        RecordPacket(messageId, (byte?)sequence, linkId);
    }

    public void RecordPacket(uint messageId, byte? sequence, string linkId)
    {
        TotalReceived++;
        var now = _clock();

        if (!_stats.TryGetValue(messageId, out var acc))
        {
            _stats.Add(messageId, new MavlinkMessageAccumulator(messageId, now));
        }
        else
        {
            acc.MarkSeen(now);
        }

        if (!_linkStats.TryGetValue(linkId, out var linkAcc))
        {
            _linkStats.Add(linkId, new MavlinkLinkAccumulator(linkId, now, sequence));
            return;
        }

        TotalLost += linkAcc.MarkSeen(now, sequence);
    }

    public MavlinkStatisticsSnapshot Snapshot()
    {
        var elapsed = Math.Max((_clock() - _startedAt).TotalSeconds, 0.001);
        return new MavlinkStatisticsSnapshot(
            TotalReceived,
            TotalLost,
            CalculateLossPercent(TotalReceived, TotalLost),
            _stats.Values
                .Select(s => new MavlinkMessageStats(s.MessageId, s.Count, Math.Round(s.Count / elapsed, 2), s.LastSeenAt))
                .OrderByDescending(s => s.Count)
                .ToArray(),
            _linkStats.Values
                .Select(s => new MavlinkLinkStats(s.LinkId, s.TotalReceived, s.PacketsLost, CalculateLossPercent(s.TotalReceived, s.PacketsLost), s.LastSequence, s.LastSeenAt))
                .OrderBy(static s => s.LinkId)
                .ToArray());
    }

    private static double CalculateLossPercent(uint received, uint lost)
    {
        var total = received + lost;
        return total == 0 ? 0 : Math.Round(lost * 100.0 / total, 2);
    }

    private sealed class MavlinkMessageAccumulator
    {
        public uint MessageId { get; }

        public uint Count { get; private set; } = 1;

        public DateTimeOffset LastSeenAt { get; private set; }

        public MavlinkMessageAccumulator(uint messageId, DateTimeOffset firstSeenAt)
        {
            MessageId = messageId;
            LastSeenAt = firstSeenAt;
        }

        public void MarkSeen(DateTimeOffset seenAt)
        {
            Count++;
            LastSeenAt = seenAt;
        }
    }

    private sealed class MavlinkLinkAccumulator
    {
        public string LinkId { get; }

        public uint TotalReceived { get; private set; } = 1;

        public uint PacketsLost { get; private set; }

        public byte? LastSequence { get; private set; }

        public DateTimeOffset LastSeenAt { get; private set; }

        public MavlinkLinkAccumulator(string linkId, DateTimeOffset firstSeenAt, byte? firstSequence)
        {
            LinkId = linkId;
            LastSeenAt = firstSeenAt;
            LastSequence = firstSequence;
        }

        public uint MarkSeen(DateTimeOffset seenAt, byte? sequence)
        {
            TotalReceived++;
            LastSeenAt = seenAt;
            var lost = CalculateLostPackets(LastSequence, sequence);
            PacketsLost += lost;
            LastSequence = sequence ?? LastSequence;
            return lost;
        }

        private static uint CalculateLostPackets(byte? lastSequence, byte? sequence)
        {
            if (!lastSequence.HasValue || !sequence.HasValue || sequence.Value == lastSequence.Value)
            {
                return 0;
            }

            var expected = (lastSequence.Value + 1) & 0xFF;
            var gap = (sequence.Value - expected + 256) & 0xFF;
            return gap > 128 ? 0u : (uint)gap;
        }
    }
}
