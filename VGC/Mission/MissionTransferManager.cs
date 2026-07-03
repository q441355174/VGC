using System.Collections.ObjectModel;
using VGC.Mavlink;

namespace VGC.Mission;

public enum MissionTransactionType
{
    None,
    Read,
    Write,
    Clear
}

public enum MissionExpectedMessage
{
    None,
    MissionCount,
    MissionItemInt,
    MissionRequestInt,
    MissionAck
}

public enum MissionTransferError
{
    None,
    Busy,
    UnexpectedMessage,
    SequenceMismatch,
    RequestOutOfRange,
    VehicleAckError,
    MaxRetryExceeded
}

public enum MissionTransferActionType
{
    None,
    SendMissionRequestList,
    SendMissionRequestInt,
    SendMissionCount,
    SendMissionItemInt,
    SendMissionClearAll
}

public sealed record MissionTransferAction(
    MissionTransferActionType Type,
    ushort Sequence = 0,
    MavlinkMissionItemInt? Item = null)
{
    public static MissionTransferAction None { get; } = new(MissionTransferActionType.None);
}

public sealed class MissionTransferManager
{
    private readonly List<MavlinkMissionItemInt> _missionItems = new();
    private readonly List<MavlinkMissionItemInt> _writeItems = new();
    private readonly HashSet<ushort> _receivedIndexes = new();
    private readonly HashSet<ushort> _pendingWriteRequests = new();

    public MissionTransferManager(MavMissionType missionType = MavMissionType.Mission)
    {
        MissionType = missionType;
    }

    public MavMissionType MissionType { get; }

    public ReadOnlyCollection<MavlinkMissionItemInt> MissionItems => _missionItems.AsReadOnly();

    public MissionTransactionType TransactionType { get; private set; }

    public MissionExpectedMessage ExpectedMessage { get; private set; }

    public MissionTransferAction LastAction { get; private set; } = MissionTransferAction.None;

    public MissionTransferError LastError { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public bool InProgress => TransactionType != MissionTransactionType.None;

    public ushort ExpectedItemCount { get; private set; }

    public ushort ReceivedItemCount => checked((ushort)_receivedIndexes.Count);

    public ushort PendingWriteRequestCount => checked((ushort)_pendingWriteRequests.Count);

    public double Progress
    {
        get
        {
            return TransactionType switch
            {
                MissionTransactionType.Read when ExpectedItemCount > 0 => Math.Min(1.0, ReceivedItemCount / (double)ExpectedItemCount),
                MissionTransactionType.Write when _writeItems.Count > 0 => Math.Min(1.0, (_writeItems.Count - _pendingWriteRequests.Count) / (double)_writeItems.Count),
                _ when !InProgress && LastError == MissionTransferError.None && _missionItems.Count > 0 => 1.0,
                _ => 0.0
            };
        }
    }

    public MissionTransferAction BeginRead()
    {
        if (!TryStart(MissionTransactionType.Read, MissionExpectedMessage.MissionCount))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionRequestList));
    }

    public MissionTransferAction BeginWrite(IEnumerable<MavlinkMissionItemInt> items)
    {
        if (!TryStart(MissionTransactionType.Write, MissionExpectedMessage.MissionRequestInt))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        _writeItems.Clear();
        _writeItems.AddRange(items
            .OrderBy(static item => item.Sequence)
            .Select(item => item with { MissionType = MissionType }));
        if (_writeItems.Count == 0)
        {
            Fail(MissionTransferError.RequestOutOfRange, "Mission write requires at least one item.");
            return SetLastAction(MissionTransferAction.None);
        }

        _pendingWriteRequests.Clear();
        foreach (var item in _writeItems)
        {
            _pendingWriteRequests.Add(item.Sequence);
        }

        ExpectedItemCount = checked((ushort)_writeItems.Count);
        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionCount));
    }

    public MissionTransferAction BeginClear()
    {
        if (!TryStart(MissionTransactionType.Clear, MissionExpectedMessage.MissionAck))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionClearAll));
    }

    public bool ApplyPacket(MavlinkPacket packet)
    {
        switch (packet.MessageId)
        {
            case 44 when MavlinkMissionService.TryReadMissionCount(packet, out var count) && IsExpectedMissionType(count.MissionType):
                HandleMissionCount(count);
                return true;
            case 47 when MavlinkMissionService.TryReadMissionAck(packet, out var ack) && IsExpectedMissionType(ack.MissionType):
                HandleMissionAck(ack);
                return true;
            case 51 when MavlinkMissionService.TryReadMissionRequestInt(packet, out var request) && IsExpectedMissionType(request.MissionType):
                HandleMissionRequestInt(request);
                return true;
            case 73 when MavlinkMissionService.TryReadMissionItemInt(packet, out var item) && IsExpectedMissionType(item.MissionType):
                HandleMissionItemInt(item);
                return true;
            default:
                return false;
        }
    }

    public MissionTransferAction FailRetryExhausted(int maxRetryCount)
    {
        Fail(
            MissionTransferError.MaxRetryExceeded,
            $"Mission transfer exceeded retry limit of {maxRetryCount}.");
        return SetLastAction(MissionTransferAction.None);
    }

    public MissionTransferAction HandleMissionCount(MavlinkMissionCount count)
    {
        if (!IsExpectedMissionType(count.MissionType))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        if (TransactionType != MissionTransactionType.Read || ExpectedMessage != MissionExpectedMessage.MissionCount)
        {
            Fail(MissionTransferError.UnexpectedMessage, "MISSION_COUNT was not expected.");
            return SetLastAction(MissionTransferAction.None);
        }

        ExpectedItemCount = count.Count;
        _missionItems.Clear();
        _receivedIndexes.Clear();
        if (count.Count == 0)
        {
            Complete();
            return SetLastAction(MissionTransferAction.None);
        }

        ExpectedMessage = MissionExpectedMessage.MissionItemInt;
        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionRequestInt, Sequence: 0));
    }

    public MissionTransferAction HandleMissionItemInt(MavlinkMissionItemInt item)
    {
        if (!IsExpectedMissionType(item.MissionType))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        if (TransactionType != MissionTransactionType.Read || ExpectedMessage != MissionExpectedMessage.MissionItemInt)
        {
            Fail(MissionTransferError.UnexpectedMessage, "MISSION_ITEM_INT was not expected.");
            return SetLastAction(MissionTransferAction.None);
        }

        var expectedSequence = ReceivedItemCount;
        if (item.Sequence != expectedSequence)
        {
            Fail(MissionTransferError.SequenceMismatch, $"Expected mission item {expectedSequence}, got {item.Sequence}.");
            return SetLastAction(MissionTransferAction.None);
        }

        _missionItems.Add(item);
        _receivedIndexes.Add(item.Sequence);
        if (ReceivedItemCount >= ExpectedItemCount)
        {
            Complete();
            return SetLastAction(MissionTransferAction.None);
        }

        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionRequestInt, Sequence: ReceivedItemCount));
    }

    public MissionTransferAction HandleMissionRequestInt(MavlinkMissionRequestInt request)
    {
        if (!IsExpectedMissionType(request.MissionType))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        if (TransactionType != MissionTransactionType.Write || ExpectedMessage != MissionExpectedMessage.MissionRequestInt)
        {
            Fail(MissionTransferError.UnexpectedMessage, "MISSION_REQUEST_INT was not expected.");
            return SetLastAction(MissionTransferAction.None);
        }

        var item = _writeItems.FirstOrDefault(candidate => candidate.Sequence == request.Sequence);
        if (item is null)
        {
            Fail(MissionTransferError.RequestOutOfRange, $"Vehicle requested mission item {request.Sequence}, which is not available.");
            return SetLastAction(MissionTransferAction.None);
        }

        _pendingWriteRequests.Remove(request.Sequence);
        ExpectedMessage = _pendingWriteRequests.Count == 0 ? MissionExpectedMessage.MissionAck : MissionExpectedMessage.MissionRequestInt;
        return SetLastAction(new MissionTransferAction(MissionTransferActionType.SendMissionItemInt, Sequence: request.Sequence, Item: item));
    }

    public MissionTransferAction HandleMissionAck(MavlinkMissionAck ack)
    {
        if (!IsExpectedMissionType(ack.MissionType))
        {
            return SetLastAction(MissionTransferAction.None);
        }

        if (ExpectedMessage != MissionExpectedMessage.MissionAck)
        {
            Fail(MissionTransferError.UnexpectedMessage, "MISSION_ACK was not expected.");
            return SetLastAction(MissionTransferAction.None);
        }

        if (ack.Result != MavlinkMissionResult.Accepted)
        {
            Fail(MissionTransferError.VehicleAckError, $"Vehicle returned mission ACK result {ack.Result}.");
            return SetLastAction(MissionTransferAction.None);
        }

        Complete();
        return SetLastAction(MissionTransferAction.None);
    }

    private bool TryStart(MissionTransactionType transactionType, MissionExpectedMessage expectedMessage)
    {
        if (InProgress)
        {
            LastError = MissionTransferError.Busy;
            LastErrorMessage = "Mission transaction already in progress.";
            return false;
        }

        TransactionType = transactionType;
        ExpectedMessage = expectedMessage;
        LastError = MissionTransferError.None;
        LastErrorMessage = null;
        ExpectedItemCount = 0;
        _missionItems.Clear();
        _receivedIndexes.Clear();
        _pendingWriteRequests.Clear();
        LastAction = MissionTransferAction.None;
        return true;
    }

    private MissionTransferAction SetLastAction(MissionTransferAction action)
    {
        LastAction = action;
        return action;
    }

    private bool IsExpectedMissionType(MavMissionType missionType)
    {
        return missionType == MissionType;
    }

    private void Complete()
    {
        TransactionType = MissionTransactionType.None;
        ExpectedMessage = MissionExpectedMessage.None;
        LastError = MissionTransferError.None;
        LastErrorMessage = null;
    }

    private void Fail(MissionTransferError error, string message)
    {
        LastError = error;
        LastErrorMessage = message;
        TransactionType = MissionTransactionType.None;
        ExpectedMessage = MissionExpectedMessage.None;
    }
}
