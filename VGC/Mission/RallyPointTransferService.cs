using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Mission;

public sealed class RallyPointTransferService
{
    private readonly byte _targetSystemId;
    private readonly byte _targetComponentId;

    public RallyPointTransferService(
        ILinkTransport link,
        byte targetSystemId,
        byte targetComponentId,
        RallyPointTransferManager? manager = null,
        MissionTransferService? missionService = null)
    {
        _targetSystemId = targetSystemId;
        _targetComponentId = targetComponentId;
        Manager = manager ?? new RallyPointTransferManager();
        MissionService = missionService ?? new MissionTransferService(
            link,
            targetSystemId,
            targetComponentId,
            new MissionTransferManager(MavMissionType.Rally));

        if (MissionService.Manager.MissionType != MavMissionType.Rally)
        {
            throw new ArgumentException("Rally transfer service requires a Rally mission transfer manager.", nameof(missionService));
        }
    }

    public RallyPointTransferManager Manager { get; }

    public MissionTransferService MissionService { get; }

    public RallyPointsPlan LastReadPlan { get; private set; } = new();

    public async ValueTask<MissionTransferAction> BeginReadAsync(CancellationToken cancellationToken = default)
    {
        if (!Manager.BeginRead())
        {
            return MissionTransferAction.None;
        }

        var action = await MissionService.BeginReadAsync(cancellationToken);
        SynchronizeStateFromMissionTransfer();
        return action;
    }

    public async ValueTask<MissionTransferAction> BeginWriteAsync(RallyPointsPlan rallyPoints, CancellationToken cancellationToken = default)
    {
        if (!Manager.BeginWrite(rallyPoints))
        {
            return MissionTransferAction.None;
        }

        var items = RallyPointMissionItemConverter.ToMissionItems(
            rallyPoints,
            _targetSystemId,
            _targetComponentId);
        var action = await MissionService.BeginWriteAsync(items, cancellationToken);
        SynchronizeStateFromMissionTransfer();
        return action;
    }

    public async ValueTask<MissionTransferAction> BeginClearAsync(CancellationToken cancellationToken = default)
    {
        if (!Manager.BeginClear())
        {
            return MissionTransferAction.None;
        }

        var action = await MissionService.BeginClearAsync(cancellationToken);
        SynchronizeStateFromMissionTransfer();
        return action;
    }

    public async ValueTask<MissionTransferAction> HandlePacketAsync(MavlinkPacket packet, CancellationToken cancellationToken = default)
    {
        var action = await MissionService.HandlePacketAsync(packet, cancellationToken);
        SynchronizeStateFromMissionTransfer();
        return action;
    }

    public async ValueTask<MissionTransferAction> HandleTimeoutAsync(CancellationToken cancellationToken = default)
    {
        var action = await MissionService.HandleTimeoutAsync(cancellationToken);
        SynchronizeStateFromMissionTransfer();
        return action;
    }

    private void SynchronizeStateFromMissionTransfer()
    {
        if (!Manager.InProgress)
        {
            return;
        }

        if (MissionService.Manager.LastError != MissionTransferError.None)
        {
            Manager.Fail(MapError(MissionService.Manager.LastError), MissionService.Manager.LastErrorMessage ?? "Rally transfer failed.");
            return;
        }

        if (MissionService.Manager.InProgress)
        {
            Manager.MarkProgress(MissionService.Manager.Progress);
            return;
        }

        var transferType = Manager.TransferType;
        if (transferType == RallyPointTransferType.Read)
        {
            LastReadPlan = RallyPointMissionItemConverter.ToRallyPointsPlan(MissionService.Manager.MissionItems);
        }
        else if (transferType == RallyPointTransferType.Clear)
        {
            LastReadPlan = new RallyPointsPlan();
        }

        Manager.Complete();
    }

    private static RallyPointTransferError MapError(MissionTransferError error)
    {
        return error switch
        {
            MissionTransferError.Busy => RallyPointTransferError.Busy,
            MissionTransferError.MaxRetryExceeded => RallyPointTransferError.Timeout,
            MissionTransferError.VehicleAckError => RallyPointTransferError.VehicleRejected,
            _ => RallyPointTransferError.ProtocolUnsupported
        };
    }
}
