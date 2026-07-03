using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Mission;

public sealed class MissionTransferActionSender
{
    private readonly MavlinkMissionService _missionService;

    public MissionTransferActionSender(MavlinkMissionService? missionService = null)
    {
        _missionService = missionService ?? new MavlinkMissionService();
    }

    public async ValueTask<bool> SendAsync(
        ILinkTransport link,
        byte targetSystemId,
        byte targetComponentId,
        MissionTransferManager manager,
        MissionTransferAction action,
        CancellationToken cancellationToken = default)
    {
        switch (action.Type)
        {
            case MissionTransferActionType.None:
                return false;
            case MissionTransferActionType.SendMissionRequestList:
                await _missionService.SendMissionRequestListAsync(link, new MavlinkMissionRequestList(targetSystemId, targetComponentId, manager.MissionType), cancellationToken);
                return true;
            case MissionTransferActionType.SendMissionRequestInt:
                await _missionService.SendMissionRequestIntAsync(link, new MavlinkMissionRequestInt(targetSystemId, targetComponentId, action.Sequence, manager.MissionType), cancellationToken);
                return true;
            case MissionTransferActionType.SendMissionCount:
                await _missionService.SendMissionCountAsync(link, new MavlinkMissionCount(targetSystemId, targetComponentId, manager.ExpectedItemCount, manager.MissionType), cancellationToken);
                return true;
            case MissionTransferActionType.SendMissionItemInt:
                if (action.Item is null)
                {
                    throw new InvalidOperationException("Mission item action requires an item payload.");
                }

                await _missionService.SendMissionItemIntAsync(link, action.Item, cancellationToken);
                return true;
            case MissionTransferActionType.SendMissionClearAll:
                await _missionService.SendMissionClearAllAsync(link, new MavlinkMissionClearAll(targetSystemId, targetComponentId, manager.MissionType), cancellationToken);
                return true;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action.Type, "Unsupported mission transfer action.");
        }
    }
}
