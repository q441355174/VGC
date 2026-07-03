namespace VGC.Firmware;

public enum FirmwareSetupProductionStatus
{
    Complete,
    Partial,
    Blocked
}

public sealed record FirmwareSetupProductionItem(
    string Id,
    string Area,
    FirmwareSetupProductionStatus Px4Status,
    FirmwareSetupProductionStatus ArduPilotStatus,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> MissingEvidence,
    string Owner);

public sealed class FirmwareSetupProductionCatalog
{
    public IReadOnlyList<FirmwareSetupProductionItem> BuildPhase326()
    {
        return
        [
            new("FW326-AIRFRAME", "Airframe selection/apply", FirmwareSetupProductionStatus.Partial, FirmwareSetupProductionStatus.Partial, ["firmware profiles", "setup component projection"], ["PX4 airframe apply transcript", "ArduPilot frame class/type apply transcript"], "FirmwareSetupParity"),
            new("FW326-SENSORS", "Sensor calibration", FirmwareSetupProductionStatus.Partial, FirmwareSetupProductionStatus.Partial, ["calibration state machine", "cancel/fail states"], ["real accel/gyro/level/compass transcript"], "SensorCalibrationWorkflow"),
            new("FW326-RADIO", "Radio calibration", FirmwareSetupProductionStatus.Partial, FirmwareSetupProductionStatus.Partial, ["channel min/max/trim model"], ["physical transmitter calibration transcript"], "AutoPilotSetupRuntime"),
            new("FW326-POWER", "Power and battery monitor", FirmwareSetupProductionStatus.Partial, FirmwareSetupProductionStatus.Partial, ["battery/failsafe setup rows"], ["real battery monitor validation"], "AutoPilotSetupRuntime"),
            new("FW326-MOTOR-SAFETY", "Motor and safety", FirmwareSetupProductionStatus.Partial, FirmwareSetupProductionStatus.Blocked, ["PX4 motor command boundary", "safety confirmation model"], ["ArduPilot actuator setup parity", "live motor assignment evidence"], "AutoPilotSetupRuntime"),
            new("FW326-METADATA-DRIFT", "Firmware metadata drift", FirmwareSetupProductionStatus.Blocked, FirmwareSetupProductionStatus.Blocked, [], ["upstream metadata import", "drift check", "firmware-specific setup transcript"], "Unassigned")
        ];
    }
}

public sealed class FirmwareSetupProductionAudit
{
    public IReadOnlyList<string> OpenBlockers(IReadOnlyList<FirmwareSetupProductionItem> items)
    {
        return items
            .Where(static item => item.Px4Status != FirmwareSetupProductionStatus.Complete
                || item.ArduPilotStatus != FirmwareSetupProductionStatus.Complete)
            .SelectMany(static item => item.MissingEvidence.Select(evidence => $"{item.Id}: {evidence}"))
            .ToArray();
    }
}
