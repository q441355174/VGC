namespace VGC.Validation;

public enum ValidationTarget
{
    Px4Sitl,
    ArduPilotSitl,
    DesktopRuntime,
    AndroidDevice,
    RealVehicle,
    PayloadHardware,
    ReplayFixture
}

public enum ValidationScenarioArea
{
    Connection,
    Parameters,
    Mission,
    FlightMode,
    GuidedCommand,
    Camera,
    Gimbal,
    FlightLog,
    Replay,
    DeviceLifecycle
}

public enum ValidationEvidenceLevel
{
    Static = 0,
    Unit = 1,
    Build = 2,
    DesktopRuntime = 3,
    AndroidRuntime = 4,
    Sitl = 5,
    RealHardware = 6,
    Release = 7
}

public enum ValidationResultStatus
{
    NotRun,
    Passed,
    Failed,
    Blocked
}

public sealed record ValidationScenario(
    string Id,
    ValidationTarget Target,
    ValidationScenarioArea Area,
    string Description,
    ValidationEvidenceLevel RequiredEvidence,
    IReadOnlyList<string> RequiredArtifacts);

public sealed record ValidationEnvironmentProbe(
    bool HasPx4,
    bool HasArduPilot,
    bool HasMavProxy,
    bool HasDocker,
    bool HasConfiguredWsl,
    bool HasRealVehicle,
    bool HasAndroidDevice,
    bool HasPayloadHardware)
{
    public bool CanRunPx4Sitl => HasPx4 || HasDocker || HasConfiguredWsl;

    public bool CanRunArduPilotSitl => HasArduPilot || HasMavProxy || HasDocker || HasConfiguredWsl;

    public bool CanRunAnySitl => CanRunPx4Sitl || CanRunArduPilotSitl;
}

public sealed record ValidationScenarioResult(
    ValidationScenario Scenario,
    ValidationResultStatus Status,
    ValidationEvidenceLevel EvidenceLevel,
    string EvidenceSummary,
    string? Blocker);

public sealed class SitlHardwareValidationCatalog
{
    public IReadOnlyList<ValidationScenario> BuildRequiredScenarios()
    {
        return
        [
            new("SITL-287-PX4-CONNECT", ValidationTarget.Px4Sitl, ValidationScenarioArea.Connection, "PX4 SITL connects over UDP 14550 and exposes heartbeat, vehicle identity, and telemetry.", ValidationEvidenceLevel.Sitl, ["PX4 startup transcript", "VGC connection log", "heartbeat/telemetry observation"]),
            new("SITL-288-APM-CONNECT", ValidationTarget.ArduPilotSitl, ValidationScenarioArea.Connection, "ArduPilot SITL connects over UDP/TCP and exposes heartbeat, vehicle identity, and telemetry.", ValidationEvidenceLevel.Sitl, ["ArduPilot startup transcript", "VGC connection log", "heartbeat/telemetry observation"]),
            new("SITL-289-PARAMETERS", ValidationTarget.Px4Sitl, ValidationScenarioArea.Parameters, "Parameters can be requested, downloaded, edited, and acknowledged against SITL.", ValidationEvidenceLevel.Sitl, ["parameter request transcript", "download count", "write ACK"]),
            new("SITL-289-MISSION", ValidationTarget.Px4Sitl, ValidationScenarioArea.Mission, "Mission upload/download round-trip succeeds against SITL.", ValidationEvidenceLevel.Sitl, ["mission upload log", "mission download log", "MISSION_ACK"]),
            new("SITL-289-MODE", ValidationTarget.ArduPilotSitl, ValidationScenarioArea.FlightMode, "Mode changes and guided commands receive ACK or explicit rejection from SITL.", ValidationEvidenceLevel.Sitl, ["mode command transcript", "COMMAND_ACK"]),
            new("SITL-290-CAMERA", ValidationTarget.PayloadHardware, ValidationScenarioArea.Camera, "Camera capture/record command path is validated with SITL or payload hardware.", ValidationEvidenceLevel.RealHardware, ["camera command transcript", "capture/record state"]),
            new("SITL-290-GIMBAL", ValidationTarget.PayloadHardware, ValidationScenarioArea.Gimbal, "Gimbal pitch/yaw and ROI command path is validated with SITL or payload hardware.", ValidationEvidenceLevel.RealHardware, ["gimbal command transcript", "attitude/ROI state"]),
            new("SITL-290-LOG", ValidationTarget.Px4Sitl, ValidationScenarioArea.FlightLog, "Flight log list/download workflow is validated with SITL-generated logs.", ValidationEvidenceLevel.Sitl, ["log list", "download result", "stored log path"]),
            new("SITL-291-REPLAY", ValidationTarget.ReplayFixture, ValidationScenarioArea.Replay, "Replay fixture opens, plays, seeks, and exposes packet details.", ValidationEvidenceLevel.Unit, ["durable replay fixture", "replay workflow test output"]),
            new("HW-292-ANDROID", ValidationTarget.AndroidDevice, ValidationScenarioArea.DeviceLifecycle, "Android device validates install, permissions, lifecycle, and core navigation.", ValidationEvidenceLevel.AndroidRuntime, ["device model", "install log", "permission/lifecycle notes"]),
            new("HW-292-REAL-VEHICLE", ValidationTarget.RealVehicle, ValidationScenarioArea.GuidedCommand, "Real vehicle validates connection, mode/guided command safety boundaries, and telemetry.", ValidationEvidenceLevel.RealHardware, ["hardware model", "safety checklist", "command transcript"])
        ];
    }
}

public sealed class ValidationEvidenceRecorder
{
    private readonly List<ValidationScenarioResult> _results = [];

    public IReadOnlyList<ValidationScenarioResult> Results => _results.ToArray();

    public ValidationScenarioResult RecordPassed(ValidationScenario scenario, ValidationEvidenceLevel evidenceLevel, string summary)
    {
        var result = new ValidationScenarioResult(scenario, ValidationResultStatus.Passed, evidenceLevel, summary, null);
        _results.Add(result);
        return result;
    }

    public ValidationScenarioResult RecordBlocked(ValidationScenario scenario, ValidationEnvironmentProbe probe, string blocker)
    {
        var result = new ValidationScenarioResult(
            scenario,
            ValidationResultStatus.Blocked,
            ValidationEvidenceLevel.Static,
            BuildProbeSummary(probe),
            string.IsNullOrWhiteSpace(blocker) ? "Required simulator or hardware is unavailable." : blocker);
        _results.Add(result);
        return result;
    }

    public ValidationScenarioResult RecordFailed(ValidationScenario scenario, ValidationEvidenceLevel evidenceLevel, string summary)
    {
        var result = new ValidationScenarioResult(scenario, ValidationResultStatus.Failed, evidenceLevel, summary, null);
        _results.Add(result);
        return result;
    }

    private static string BuildProbeSummary(ValidationEnvironmentProbe probe)
    {
        return $"PX4={probe.HasPx4}, ArduPilot={probe.HasArduPilot}, MAVProxy={probe.HasMavProxy}, Docker={probe.HasDocker}, WSL={probe.HasConfiguredWsl}, Android={probe.HasAndroidDevice}, Hardware={probe.HasRealVehicle}, Payload={probe.HasPayloadHardware}";
    }
}

public sealed record ValidationClosureSummary(
    int RequiredScenarios,
    int Passed,
    int Blocked,
    int Failed,
    int Missing,
    bool CanClaimSitlValidated,
    bool CanClaimRealHardwareValidated,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class ValidationClosureAudit
{
    public ValidationClosureSummary Audit(IReadOnlyList<ValidationScenario> required, IReadOnlyList<ValidationScenarioResult> results)
    {
        var byId = results.GroupBy(static result => result.Scenario.Id).ToDictionary(static group => group.Key, static group => group.Last());
        var matched = required.Select(scenario => byId.TryGetValue(scenario.Id, out var result) ? result : null).ToArray();
        var passed = matched.Count(static result => result?.Status == ValidationResultStatus.Passed);
        var blocked = matched.Count(static result => result?.Status == ValidationResultStatus.Blocked);
        var failed = matched.Count(static result => result?.Status == ValidationResultStatus.Failed);
        var missing = matched.Count(static result => result is null);
        var sitlRequired = required.Where(static scenario => scenario.RequiredEvidence == ValidationEvidenceLevel.Sitl).ToArray();
        var hardwareRequired = required.Where(static scenario => scenario.RequiredEvidence == ValidationEvidenceLevel.RealHardware).ToArray();
        var canClaimSitl = sitlRequired.All(scenario => byId.TryGetValue(scenario.Id, out var result) && result.Status == ValidationResultStatus.Passed && result.EvidenceLevel >= ValidationEvidenceLevel.Sitl);
        var canClaimHardware = hardwareRequired.All(scenario => byId.TryGetValue(scenario.Id, out var result) && result.Status == ValidationResultStatus.Passed && result.EvidenceLevel >= ValidationEvidenceLevel.RealHardware);
        var blockers = matched
            .Where(static result => result?.Status == ValidationResultStatus.Blocked)
            .Select(static result => result!.Blocker ?? "Blocked")
            .Concat(required.Where(scenario => !byId.ContainsKey(scenario.Id)).Select(static scenario => $"Missing evidence for {scenario.Id}."))
            .ToArray();

        return new ValidationClosureSummary(
            required.Count,
            passed,
            blocked,
            failed,
            missing,
            canClaimSitl,
            canClaimHardware,
            blockers,
            $"{passed}/{required.Count} validation scenarios passed; {blocked} blocked, {failed} failed, {missing} missing.");
    }
}

public sealed record SitlHardwareEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class SitlHardwareEvidenceCatalog
{
    public IReadOnlyList<SitlHardwareEvidenceItem> Build()
    {
        return
        [
            new("SITLHW-287", "L0", "PX4 SITL connection scenario and required artifacts are defined.", true),
            new("SITLHW-288", "L0", "ArduPilot SITL connection scenario and required artifacts are defined.", true),
            new("SITLHW-289", "L0", "Parameter, mission, mode, and guided-command SITL scenarios are defined.", true),
            new("SITLHW-290", "L0", "Camera, gimbal, and flight-log validation scenarios are defined.", true),
            new("SITLHW-291", "L1", "Replay fixture workflow remains covered by unit-level replay tests.", true),
            new("SITLHW-292", "L0", "Android and real-vehicle hardware validation scenarios are defined.", true),
            new("SITLHW-293", "L0", "Environment blocker handling prevents false SITL/hardware claims.", true),
            new("SITLHW-294", "L0", "Actual PX4/ArduPilot SITL, real hardware, and Android device transcripts remain required external evidence.", false)
        ];
    }
}
