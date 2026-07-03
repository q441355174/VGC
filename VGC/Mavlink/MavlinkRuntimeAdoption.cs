namespace VGC.Mavlink;

public enum MavlinkRuntimeServiceArea
{
    Command,
    Mission,
    Parameter,
    Camera,
    Gimbal,
    Ftp,
    Signing
}

public enum MavlinkRuntimeAdoptionStatus
{
    Complete,
    Partial,
    Blocked
}

public sealed record MavlinkRuntimeAdoptionItem(
    string Id,
    MavlinkRuntimeServiceArea Area,
    MavlinkRuntimeAdoptionStatus Status,
    string CurrentOwner,
    IReadOnlyList<string> AdoptedMessages,
    IReadOnlyList<string> RemainingMessages,
    string RequiredEvidence);

public sealed class MavlinkRuntimeAdoptionCatalog
{
    public IReadOnlyList<MavlinkRuntimeAdoptionItem> BuildPhase325()
    {
        return
        [
            new("MAV325-COMMAND", MavlinkRuntimeServiceArea.Command, MavlinkRuntimeAdoptionStatus.Partial, "MavlinkCommandService", ["COMMAND_LONG", "COMMAND_ACK"], ["COMMAND_INT", "generated command metadata writers"], "generated COMMAND_LONG/COMMAND_INT round-trip tests"),
            new("MAV325-MISSION", MavlinkRuntimeServiceArea.Mission, MavlinkRuntimeAdoptionStatus.Partial, "MavlinkMission", ["MISSION_COUNT", "MISSION_ITEM_INT", "MISSION_ACK"], ["all mission variants generated from dialect metadata"], "PX4/APM mission round-trip transcript"),
            new("MAV325-PARAMETER", MavlinkRuntimeServiceArea.Parameter, MavlinkRuntimeAdoptionStatus.Partial, "MavlinkParameterService", ["PARAM_REQUEST_LIST", "PARAM_REQUEST_READ", "PARAM_SET", "PARAM_VALUE"], ["generated param value type coverage"], "parameter download/write SITL transcript"),
            new("MAV325-CAMERA", MavlinkRuntimeServiceArea.Camera, MavlinkRuntimeAdoptionStatus.Partial, "MavlinkCameraService", ["camera command boundaries"], ["camera information", "storage information", "capture status"], "camera hardware or simulator transcript"),
            new("MAV325-GIMBAL", MavlinkRuntimeServiceArea.Gimbal, MavlinkRuntimeAdoptionStatus.Partial, "MavlinkGimbalService", ["pitch/yaw command", "ROI command boundary"], ["gimbal manager status", "device attitude"], "gimbal hardware transcript"),
            new("MAV325-FTP", MavlinkRuntimeServiceArea.Ftp, MavlinkRuntimeAdoptionStatus.Complete, "MavlinkFtp", ["list", "download chunks", "NAK retry"], [], "VGC.Tests MAVLink FTP coverage"),
            new("MAV325-SIGNING", MavlinkRuntimeServiceArea.Signing, MavlinkRuntimeAdoptionStatus.Blocked, "SigningController", ["frame signing/validation"], ["vehicle session policy", "key management adoption"], "signed vehicle session transcript")
        ];
    }
}

public sealed class MavlinkRuntimeAdoptionAudit
{
    public IReadOnlyList<string> OpenBlockers(IReadOnlyList<MavlinkRuntimeAdoptionItem> items)
    {
        return items
            .Where(static item => item.Status != MavlinkRuntimeAdoptionStatus.Complete)
            .Select(static item => $"{item.Id}: {item.Area} remains {item.Status}; requires {item.RequiredEvidence}.")
            .ToArray();
    }
}
