using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed record MessageIntervalRequest(
    uint MessageId,
    int IntervalMicroseconds,
    DateTimeOffset RequestedAt,
    int RetryCount = 0,
    int MaxRetries = 3)
{
    public bool IsExpired(TimeSpan timeout, DateTimeOffset now)
    {
        return now - RequestedAt > timeout;
    }

    public bool CanRetry => RetryCount < MaxRetries;

    public MessageIntervalRequest WithRetry()
    {
        return this with { RetryCount = RetryCount + 1, RequestedAt = DateTimeOffset.UtcNow };
    }
}

public enum MavlinkStreamProfile
{
    Default,
    HighRateTelemetry,
    LowRateTelemetry
}

public sealed record MavlinkStreamConfigEntry(
    uint MessageId,
    int IntervalMicroseconds,
    string Name);

public sealed record MavlinkStreamConfigSnapshot(
    MavlinkStreamProfile? ActiveProfile,
    IReadOnlyList<MavlinkStreamConfigEntry> DesiredRates,
    IReadOnlyList<MessageIntervalRequest> PendingRequests);

public static class MavlinkStreamConfig
{
    private static readonly IReadOnlyDictionary<MavlinkStreamProfile, IReadOnlyList<MavlinkStreamConfigEntry>> Profiles =
        new Dictionary<MavlinkStreamProfile, IReadOnlyList<MavlinkStreamConfigEntry>>
        {
            [MavlinkStreamProfile.Default] =
            [
                new(MavlinkMessageIds.Heartbeat, 1000000, "HEARTBEAT"),
                new(MavlinkMessageIds.GlobalPositionInt, 200000, "GLOBAL_POSITION_INT"),
                new(30, 200000, "ATTITUDE"),
                new(24, 200000, "GPS_RAW_INT"),
                new(MavlinkMessageIds.SysStatus, 1000000, "SYS_STATUS")
            ],
            [MavlinkStreamProfile.HighRateTelemetry] =
            [
                new(MavlinkMessageIds.GlobalPositionInt, 100000, "GLOBAL_POSITION_INT"),
                new(30, 50000, "ATTITUDE")
            ],
            [MavlinkStreamProfile.LowRateTelemetry] =
            [
                new(MavlinkMessageIds.GlobalPositionInt, 1000000, "GLOBAL_POSITION_INT"),
                new(30, 500000, "ATTITUDE"),
                new(MavlinkMessageIds.SysStatus, 5000000, "SYS_STATUS")
            ]
        };

    public static IReadOnlyList<MavlinkStreamConfigEntry> GetProfile(MavlinkStreamProfile profile)
    {
        return Profiles[profile];
    }
}

public sealed class MessageIntervalManager
{
    private readonly Dictionary<uint, MessageIntervalRequest> _requests = [];
    private IReadOnlyList<MavlinkStreamConfigEntry> _desiredRates = [];
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly MavlinkCommandService _commandService;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private const ushort MavCmdSetMessageInterval = 511;

    public MessageIntervalManager(byte systemId, byte componentId, MavlinkCommandService? commandService = null)
    {
        _systemId = systemId;
        _componentId = componentId;
        _commandService = commandService ?? new MavlinkCommandService(systemId, componentId);
    }

    public IReadOnlyDictionary<uint, MessageIntervalRequest> ActiveRequests => _requests;

    public int RequestCount => _requests.Count;

    public TimeSpan RequestTimeout { get; set; } = DefaultTimeout;

    public MavlinkStreamProfile? ActiveProfile { get; private set; }

    public MavlinkStreamConfigSnapshot Snapshot()
    {
        return new MavlinkStreamConfigSnapshot(
            ActiveProfile,
            _desiredRates.ToArray(),
            _requests.Values.OrderBy(static r => r.MessageId).ToArray());
    }

    public async Task SetMessageIntervalAsync(
        ILinkTransport link,
        uint messageId,
        int intervalMicroseconds,
        CancellationToken cancellationToken = default)
    {
        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: MavCmdSetMessageInterval,
            Confirmation: 0,
            Param1: messageId,
            Param2: intervalMicroseconds);

        await _commandService.SendCommandLongAsync(link, command, cancellationToken).ConfigureAwait(false);
        _requests[messageId] = new MessageIntervalRequest(messageId, intervalMicroseconds, DateTimeOffset.UtcNow);
    }

    public async Task SetDefaultRatesAsync(ILinkTransport link, CancellationToken cancellationToken = default)
    {
        await ApplyStreamProfileAsync(link, MavlinkStreamProfile.Default, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetHighRateTelemetryAsync(ILinkTransport link, CancellationToken cancellationToken = default)
    {
        await ApplyStreamProfileAsync(link, MavlinkStreamProfile.HighRateTelemetry, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetLowRateTelemetryAsync(ILinkTransport link, CancellationToken cancellationToken = default)
    {
        await ApplyStreamProfileAsync(link, MavlinkStreamProfile.LowRateTelemetry, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyStreamProfileAsync(
        ILinkTransport link,
        MavlinkStreamProfile profile,
        CancellationToken cancellationToken = default)
    {
        var entries = MavlinkStreamConfig.GetProfile(profile);
        ActiveProfile = profile;
        _desiredRates = entries.ToArray();

        foreach (var entry in entries)
        {
            await SetMessageIntervalAsync(link, entry.MessageId, entry.IntervalMicroseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    public bool AcknowledgeInterval(uint messageId)
    {
        return _requests.Remove(messageId);
    }

    public IReadOnlyList<uint> GetExpiredRequests()
    {
        var now = DateTimeOffset.UtcNow;
        return _requests
            .Where(kv => kv.Value.IsExpired(RequestTimeout, now))
            .Select(kv => kv.Key)
            .ToArray();
    }

    public async Task<bool> RetryExpiredAsync(
        ILinkTransport link,
        uint messageId,
        CancellationToken cancellationToken = default)
    {
        if (!_requests.TryGetValue(messageId, out var request))
        {
            return false;
        }

        if (!request.IsExpired(RequestTimeout, DateTimeOffset.UtcNow))
        {
            return false;
        }

        if (!request.CanRetry)
        {
            _requests.Remove(messageId);
            return false;
        }

        await SetMessageIntervalAsync(
            link,
            request.MessageId,
            request.IntervalMicroseconds,
            cancellationToken).ConfigureAwait(false);
        _requests[messageId] = request.WithRetry();
        return true;
    }
}
