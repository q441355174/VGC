using VGC.Comms;

namespace VGC.Analyze;

public enum ReplayPlaybackState
{
    Closed,
    Ready,
    Playing,
    Paused,
    Ended
}

public sealed record ReplayPlaybackSnapshot(
    ReplayPlaybackState State,
    TimeSpan CurrentTime,
    TimeSpan Duration,
    int PacketIndex,
    int PacketCount,
    double Speed,
    string StatusText)
{
    public bool CanPlay => State is ReplayPlaybackState.Ready or ReplayPlaybackState.Paused;

    public bool CanPause => State == ReplayPlaybackState.Playing;

    public bool CanSeek => State is ReplayPlaybackState.Ready or ReplayPlaybackState.Playing or ReplayPlaybackState.Paused or ReplayPlaybackState.Ended;

    public bool IsOpen => State != ReplayPlaybackState.Closed;
}

public sealed class ReplayPlaybackSession
{
    private IReadOnlyList<LogReplayPacket> _packets = [];
    private readonly ReplayTimelineBuilder _timelineBuilder;

    public ReplayPlaybackSession(ReplayTimelineBuilder? timelineBuilder = null)
    {
        _timelineBuilder = timelineBuilder ?? new ReplayTimelineBuilder();
    }

    public ReplayPlaybackSnapshot Snapshot { get; private set; } = new(
        ReplayPlaybackState.Closed,
        TimeSpan.Zero,
        TimeSpan.Zero,
        0,
        0,
        1.0,
        "No replay loaded");

    public ReplayTimelineProjection Timeline { get; private set; } = ReplayTimelineProjection.Empty;

    public async Task OpenAsync(ILogReplaySource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        _packets = (await source.ReadPacketsAsync(cancellationToken).ConfigureAwait(false))
            .OrderBy(static packet => packet.Timestamp)
            .ToList();
        Timeline = _timelineBuilder.Build(_packets);

        var duration = _packets.Count == 0 ? TimeSpan.Zero : _packets[^1].Timestamp;
        Snapshot = Snapshot with
        {
            State = _packets.Count == 0 ? ReplayPlaybackState.Ended : ReplayPlaybackState.Ready,
            CurrentTime = TimeSpan.Zero,
            Duration = duration,
            PacketIndex = 0,
            PacketCount = _packets.Count,
            StatusText = _packets.Count == 0 ? "Replay is empty" : $"Replay ready: {_packets.Count} packets"
        };
    }

    public void Play()
    {
        if (Snapshot.State == ReplayPlaybackState.Closed || Snapshot.State == ReplayPlaybackState.Ended)
        {
            return;
        }

        Snapshot = Snapshot with
        {
            State = ReplayPlaybackState.Playing,
            StatusText = $"Playing at {Snapshot.Speed:G}x"
        };
    }

    public void Pause()
    {
        if (Snapshot.State != ReplayPlaybackState.Playing)
        {
            return;
        }

        Snapshot = Snapshot with
        {
            State = ReplayPlaybackState.Paused,
            StatusText = "Replay paused"
        };
    }

    public void SetSpeed(double speed)
    {
        if (speed <= 0 || double.IsNaN(speed) || double.IsInfinity(speed))
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Replay speed must be greater than zero.");
        }

        Snapshot = Snapshot with
        {
            Speed = speed,
            StatusText = Snapshot.State == ReplayPlaybackState.Playing ? $"Playing at {speed:G}x" : $"Replay speed {speed:G}x"
        };
    }

    public void Seek(TimeSpan position)
    {
        if (!Snapshot.CanSeek)
        {
            return;
        }

        var clamped = Clamp(position, TimeSpan.Zero, Snapshot.Duration);
        var nextIndex = NextPacketIndexAtOrAfter(clamped);
        var ended = _packets.Count == 0 || nextIndex >= _packets.Count;
        Snapshot = Snapshot with
        {
            CurrentTime = clamped,
            PacketIndex = nextIndex,
            State = ended ? ReplayPlaybackState.Ended : Snapshot.State == ReplayPlaybackState.Ended ? ReplayPlaybackState.Paused : Snapshot.State,
            StatusText = ended ? "Replay ended" : $"Replay seek {FormatTime(clamped)}"
        };
    }

    public LogReplayPacket? AdvanceToNextPacket()
    {
        if (Snapshot.State != ReplayPlaybackState.Playing)
        {
            return null;
        }

        if (Snapshot.PacketIndex >= _packets.Count)
        {
            MarkEnded();
            return null;
        }

        var packet = _packets[Snapshot.PacketIndex];
        var nextIndex = Snapshot.PacketIndex + 1;
        var ended = nextIndex >= _packets.Count;
        Snapshot = Snapshot with
        {
            CurrentTime = packet.Timestamp,
            PacketIndex = nextIndex,
            State = ended ? ReplayPlaybackState.Ended : ReplayPlaybackState.Playing,
            StatusText = ended ? "Replay ended" : $"Playing packet {nextIndex + 1}/{_packets.Count}"
        };
        return packet;
    }

    public void Close()
    {
        _packets = [];
        Timeline = ReplayTimelineProjection.Empty;
        Snapshot = new ReplayPlaybackSnapshot(
            ReplayPlaybackState.Closed,
            TimeSpan.Zero,
            TimeSpan.Zero,
            0,
            0,
            Snapshot.Speed,
            "No replay loaded");
    }

    private int NextPacketIndexAtOrAfter(TimeSpan position)
    {
        for (var index = 0; index < _packets.Count; index++)
        {
            if (_packets[index].Timestamp >= position)
            {
                return index;
            }
        }

        return _packets.Count;
    }

    private void MarkEnded()
    {
        Snapshot = Snapshot with
        {
            CurrentTime = Snapshot.Duration,
            PacketIndex = _packets.Count,
            State = ReplayPlaybackState.Ended,
            StatusText = "Replay ended"
        };
    }

    private static TimeSpan Clamp(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm\:ss");
    }
}
