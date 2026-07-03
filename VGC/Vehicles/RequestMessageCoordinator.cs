using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed record PendingMessageRequest(
    uint MessageId,
    DateTimeOffset RequestedAt,
    int RetryCount = 0,
    int MaxRetries = 3);

public sealed class RequestMessageCoordinator
{
    private readonly Dictionary<uint, PendingMessageRequest> _pending = [];
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly MavlinkCommandService _commandService;

    public RequestMessageCoordinator(byte systemId, byte componentId, MavlinkCommandService? commandService = null)
    {
        _systemId = systemId;
        _componentId = componentId;
        _commandService = commandService ?? new MavlinkCommandService(systemId, componentId);
    }

    public int PendingCount => _pending.Count;

    public TimeSpan RequestTimeout { get; set; } = DefaultTimeout;

    public async Task RequestMessageAsync(
        ILinkTransport link,
        uint messageId,
        CancellationToken cancellationToken = default)
    {
        if (_pending.ContainsKey(messageId))
        {
            return;
        }

        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: 512, // MAV_CMD_REQUEST_MESSAGE
            Confirmation: 0,
            Param1: messageId);

        await _commandService.SendCommandLongAsync(link, command, cancellationToken).ConfigureAwait(false);
        _pending[messageId] = new PendingMessageRequest(messageId, DateTimeOffset.UtcNow);
    }

    public bool AcknowledgeResponse(uint messageId)
    {
        return _pending.Remove(messageId);
    }

    public IReadOnlyList<uint> GetExpiredRequests()
    {
        var now = DateTimeOffset.UtcNow;
        return _pending
            .Where(kv => now - kv.Value.RequestedAt > RequestTimeout)
            .Select(kv => kv.Key)
            .ToArray();
    }

    public async Task<bool> RetryExpiredAsync(
        ILinkTransport link,
        uint messageId,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(messageId, out var request))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (now - request.RequestedAt <= RequestTimeout)
        {
            return false;
        }

        if (request.RetryCount >= request.MaxRetries)
        {
            _pending.Remove(messageId);
            return false;
        }

        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: 512,
            Confirmation: 0,
            Param1: messageId);

        await _commandService.SendCommandLongAsync(link, command, cancellationToken).ConfigureAwait(false);
        _pending[messageId] = request with { RetryCount = request.RetryCount + 1, RequestedAt = DateTimeOffset.UtcNow };
        return true;
    }

    public void CancelAll()
    {
        _pending.Clear();
    }
}
