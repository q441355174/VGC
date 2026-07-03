using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Mission;

public sealed class GeoFenceTransferService
{
    private readonly byte _targetSystemId;
    private readonly byte _targetComponentId;

    public GeoFenceTransferService(
        ILinkTransport link,
        byte targetSystemId,
        byte targetComponentId,
        GeoFenceTransferManager? manager = null,
        MissionTransferService? missionService = null)
    {
        _targetSystemId = targetSystemId;
        _targetComponentId = targetComponentId;
        Manager = manager ?? new GeoFenceTransferManager();
        MissionService = missionService ?? new MissionTransferService(
            link,
            targetSystemId,
            targetComponentId,
            new MissionTransferManager(MavMissionType.Fence));

        if (MissionService.Manager.MissionType != MavMissionType.Fence)
        {
            throw new ArgumentException("GeoFence transfer service requires a Fence mission transfer manager.", nameof(missionService));
        }
    }

    public GeoFenceTransferManager Manager { get; }

    public MissionTransferService MissionService { get; }

    public GeoFencePlan LastReadPlan { get; private set; } = new();

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

    public async ValueTask<MissionTransferAction> BeginWriteAsync(GeoFencePlan geoFence, CancellationToken cancellationToken = default)
    {
        if (!Manager.BeginWrite(geoFence))
        {
            return MissionTransferAction.None;
        }

        var items = GeoFenceMissionItemConverter.ToMissionItems(
            geoFence,
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
            Manager.Fail(MapError(MissionService.Manager.LastError), MissionService.Manager.LastErrorMessage ?? "GeoFence transfer failed.");
            return;
        }

        if (MissionService.Manager.InProgress)
        {
            Manager.MarkProgress(MissionService.Manager.Progress);
            return;
        }

        var transferType = Manager.TransferType;
        if (transferType == GeoFenceTransferType.Read)
        {
            LastReadPlan = GeoFenceMissionItemConverter.ToGeoFencePlan(MissionService.Manager.MissionItems);
        }
        else if (transferType == GeoFenceTransferType.Clear)
        {
            LastReadPlan = new GeoFencePlan();
        }

        Manager.Complete();
    }

    private static GeoFenceTransferError MapError(MissionTransferError error)
    {
        return error switch
        {
            MissionTransferError.Busy => GeoFenceTransferError.Busy,
            MissionTransferError.MaxRetryExceeded => GeoFenceTransferError.Timeout,
            MissionTransferError.VehicleAckError => GeoFenceTransferError.VehicleRejected,
            _ => GeoFenceTransferError.ProtocolUnsupported
        };
    }
}
