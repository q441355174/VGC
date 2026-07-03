namespace VGC.Comms;

public sealed record LogReplayPacket(TimeSpan Timestamp, byte[] Bytes);

public interface ILogReplaySource
{
    Task<IReadOnlyList<LogReplayPacket>> ReadPacketsAsync(CancellationToken cancellationToken = default);
}

public interface ILogReplayDelayScheduler
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default);
}

public sealed class TaskDelayLogReplayDelayScheduler : ILogReplayDelayScheduler
{
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        return delay <= TimeSpan.Zero ? Task.CompletedTask : Task.Delay(delay, cancellationToken);
    }
}

public sealed class InMemoryLogReplaySource : ILogReplaySource
{
    private readonly IReadOnlyList<LogReplayPacket> _packets;

    public InMemoryLogReplaySource(IEnumerable<LogReplayPacket> packets)
    {
        _packets = packets.ToList();
    }

    public Task<IReadOnlyList<LogReplayPacket>> ReadPacketsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_packets);
    }
}

public sealed class LogReplayLinkTransport : ILinkTransport
{
    private readonly ILogReplaySource _source;
    private readonly ILogReplayTimingPolicy _timingPolicy;
    private readonly ILogReplayDelayScheduler _delayScheduler;

    public LogReplayLinkTransport(
        LogReplayLinkConfiguration configuration,
        ILogReplaySource source,
        ILogReplayTimingPolicy? timingPolicy = null,
        ILogReplayDelayScheduler? delayScheduler = null)
    {
        Configuration = configuration;
        _source = source;
        _timingPolicy = timingPolicy ?? new DefaultLogReplayTimingPolicy();
        _delayScheduler = delayScheduler ?? new TaskDelayLogReplayDelayScheduler();
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

#pragma warning disable CS0067
    public event EventHandler<BytesReceivedEventArgs>? BytesSent;
#pragma warning restore CS0067

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration { get; }

    public bool IsConnected { get; private set; }

    public bool CanSend => false;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public async Task ReplayOnceAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            CommunicationError?.Invoke(this, "Log replay link is not connected.");
            return;
        }

        var configuration = (LogReplayLinkConfiguration)Configuration;
        var previousTimestamp = TimeSpan.Zero;
        foreach (var packet in (await _source.ReadPacketsAsync(cancellationToken).ConfigureAwait(false)).OrderBy(static packet => packet.Timestamp))
        {
            var delay = _timingPolicy.ScaleDelay(packet.Timestamp - previousTimestamp, configuration.Speed);
            await _delayScheduler.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
            BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, packet.Bytes));
            previousTimestamp = packet.Timestamp;
        }
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        CommunicationError?.Invoke(this, "Log replay links are read-only.");
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
