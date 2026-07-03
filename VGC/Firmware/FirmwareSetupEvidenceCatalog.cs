namespace VGC.Firmware;

public sealed record FirmwareSetupEvidenceItem(
    string Name,
    string Target,
    string Verification,
    bool IsComplete);

public static class FirmwareSetupEvidenceCatalog
{
    public static IReadOnlyList<FirmwareSetupEvidenceItem> CreateV145Checklist()
    {
        return
        [
            new("Firmware metadata", "PX4/APM metadata package registry and catalog selection.", "VGC.Tests", true),
            new("Command availability", "Mission command availability includes category and UI metadata.", "VGC.Tests", true),
            new("Setup components", "Required/optional/ready/warning/blocked setup projection is tested.", "VGC.Tests", true),
            new("Calibration workflows", "Compass, accel, gyro, and level command boundaries are modeled.", "VGC.Tests", true),
            new("Radio power safety", "RC calibration, power/failsafe, and motor safety boundaries are modeled.", "VGC.Tests", true),
            new("SITL checklist", "SITL remains deferred to v1.53 with explicit checklist coverage.", "Milestone audit", true)
        ];
    }
}

public sealed record FirmwareSetupRuntimeReviewItem(
    string Area,
    string Px4Status,
    string ArduPilotStatus,
    string ResidualGap);

public static class FirmwareSetupRuntimeReview
{
    public static IReadOnlyList<FirmwareSetupRuntimeReviewItem> CreateV145Review()
    {
        return
        [
            new("Metadata", "Builtin PX4 seed metadata present.", "Builtin ArduPilot seed metadata present.", "Full upstream metadata import remains future work."),
            new("Calibration", "Preflight calibration command boundary modeled.", "Preflight calibration command boundary modeled.", "SITL/device confirmation remains future work."),
            new("Radio", "RC map and channel min/max/trim boundary modeled.", "RC map and channel min/max/trim boundary modeled.", "Physical transmitter evidence remains future work."),
            new("Power/Safety", "Battery/failsafe parameters modeled.", "Battery/failsafe parameters modeled.", "Real hardware safety validation remains required.")
        ];
    }
}
