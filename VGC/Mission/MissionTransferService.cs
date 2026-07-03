using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Mission;

public sealed class MissionTransferService
{
    private readonly ILinkTransport _link;
    private readonly byte _targetSystemId;
    private readonly byte _targetComponentId;
    private readonly MissionTransferActionSender _actionSender;
    private MissionTransferAction _lastSentAction = MissionTransferAction.None;

    public MissionTransferService(
        ILinkTransport link,
        byte targetSystemId,
        byte targetComponentId,
        MissionTransferManager? manager = null,
        MissionTransferActionSender? actionSender = null)
    {
        _link = link;
        _targetSystemId = targetSystemId;
        _targetComponentId = targetComponentId;
        Manager = manager ?? new MissionTransferManager();
        _actionSender = actionSender ?? new MissionTransferActionSender();
    }

    public MissionTransferManager Manager { get; }

    public int MaxRetryCount { get; set; } = 3;

    public int RetryCount { get; private set; }

    public MissionTransferAction LastSentAction => _lastSentAction;

    public async ValueTask<MissionTransferAction> BeginReadAsync(CancellationToken cancellationToken = default)
    {
        ResetRetryState();
        var action = Manager.BeginRead();
        await SendActionAsync(action, cancellationToken);
        return action;
    }

    public async ValueTask<MissionTransferAction> BeginWriteAsync(IEnumerable<MavlinkMissionItemInt> items, CancellationToken cancellationToken = default)
    {
        ResetRetryState();
        var action = Manager.BeginWrite(items);
        await SendActionAsync(action, cancellationToken);
        return action;
    }

    public async ValueTask<MissionTransferAction> BeginClearAsync(CancellationToken cancellationToken = default)
    {
        ResetRetryState();
        var action = Manager.BeginClear();
        await SendActionAsync(action, cancellationToken);
        return action;
    }

    public async ValueTask<MissionTransferAction> HandlePacketAsync(MavlinkPacket packet, CancellationToken cancellationToken = default)
    {
        if (!Manager.ApplyPacket(packet))
        {
            return MissionTransferAction.None;
        }

        var action = Manager.LastAction;
        RetryCount = 0;
        await SendActionAsync(action, cancellationToken);
        return action;
    }

    public async ValueTask<MissionTransferAction> HandleTimeoutAsync(CancellationToken cancellationToken = default)
    {
        if (!Manager.InProgress || _lastSentAction.Type == MissionTransferActionType.None)
        {
            return MissionTransferAction.None;
        }

        if (RetryCount >= MaxRetryCount)
        {
            var failAction = Manager.FailRetryExhausted(MaxRetryCount);
            _lastSentAction = MissionTransferAction.None;
            return failAction;
        }

        RetryCount++;
        await SendActionAsync(_lastSentAction, cancellationToken);
        return _lastSentAction;
    }

    private async ValueTask SendActionAsync(MissionTransferAction action, CancellationToken cancellationToken)
    {
        if (await _actionSender.SendAsync(_link, _targetSystemId, _targetComponentId, Manager, action, cancellationToken))
        {
            _lastSentAction = action;
        }
    }

    private void ResetRetryState()
    {
        RetryCount = 0;
        _lastSentAction = MissionTransferAction.None;
    }
}
