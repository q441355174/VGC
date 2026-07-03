namespace VGC.Payload;

public enum VideoPayloadProductionStatus
{
    Complete,
    SharedModelOnly,
    Blocked
}

public sealed record VideoPayloadProductionItem(
    string Id,
    string Area,
    VideoPayloadProductionStatus Status,
    string Owner,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> RequiredRuntimeEvidence);

public sealed class VideoPayloadProductionCatalog
{
    public IReadOnlyList<VideoPayloadProductionItem> BuildPhase330()
    {
        return
        [
            new("VID330-BACKEND", "Video backend selection", VideoPayloadProductionStatus.SharedModelOnly, "VideoBackendDecisionService", ["platform/FFmpeg/GStreamer decision matrix"], ["packaging and license decision for selected backend"]),
            new("VID330-RTSP-UDP", "RTSP/UDP decode", VideoPayloadProductionStatus.Blocked, "VideoDecodePipelineRuntime", ["decode state model", "stalled/failed health projection"], ["real RTSP transcript", "real UDP transcript", "frame health log"]),
            new("VID330-UVC", "UVC device runtime", VideoPayloadProductionStatus.SharedModelOnly, "UvcDeviceRuntime", ["discovery", "permission", "format selection", "streaming state"], ["UVC hardware transcript"]),
            new("VID330-MEDIA", "Snapshot and recording output", VideoPayloadProductionStatus.SharedModelOnly, "PayloadMediaOutputPlanner", ["media naming", "Android scoped-storage risk"], ["snapshot file", "recording file", "Android media output transcript"]),
            new("VID330-CAMERA", "Camera settings and command workflow", VideoPayloadProductionStatus.SharedModelOnly, "CameraSettingsValidator/MavlinkCameraService", ["settings validation", "capture/record command boundary"], ["camera information/status transcript"]),
            new("VID330-GIMBAL", "Gimbal ROI and attitude workflow", VideoPayloadProductionStatus.SharedModelOnly, "GimbalRoiLinkController/MavlinkGimbalService", ["ROI command", "pitch/yaw command"], ["gimbal attitude/status transcript"])
        ];
    }
}

public sealed class VideoPayloadProductionAudit
{
    public IReadOnlyList<string> MissingEvidence(IReadOnlyList<VideoPayloadProductionItem> items)
    {
        return items
            .Where(static item => item.Status != VideoPayloadProductionStatus.Complete)
            .SelectMany(static item => item.RequiredRuntimeEvidence.Select(evidence => $"{item.Id}: {evidence}"))
            .ToArray();
    }
}
