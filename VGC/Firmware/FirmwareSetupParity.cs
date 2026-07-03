using VGC.Facts;
using VGC.Setup;
using VGC.Vehicles;

namespace VGC.Firmware;

public enum FirmwareSetupParityDisposition
{
    Complete,
    Partial,
    Blocked,
    OwnedByLaterPhase
}

public enum FirmwareSetupParityPriority
{
    P0,
    P1,
    P2
}

public enum FirmwareSetupFlowArea
{
    Metadata,
    SetupComponents,
    SensorCalibration,
    RadioCalibration,
    PowerBattery,
    SafetyFailsafe,
    MotorsActuators,
    FlightModes,
    Airframe,
    RuntimeEvidence
}

public sealed record FirmwareSetupParityItem(
    string Id,
    FirmwareSetupFlowArea Area,
    FirmwareSetupParityPriority Priority,
    FirmwareSetupParityDisposition Px4Disposition,
    FirmwareSetupParityDisposition ArduPilotDisposition,
    string VgcOwner,
    IReadOnlyList<string> EvidenceTests,
    string Notes);

public sealed class FirmwareSetupParityCatalog
{
    public IReadOnlyList<FirmwareSetupParityItem> Build()
    {
        return
        [
            new(
                "FW-313-METADATA",
                FirmwareSetupFlowArea.Metadata,
                FirmwareSetupParityPriority.P0,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Firmware.FirmwareMetadataPackageRegistry",
                ["Load firmware metadata packages", "Project parameter setup rows"],
                "Builtin PX4/APM metadata seeds exist; full upstream metadata import and drift checks remain open."),
            new(
                "FW-313-COMPONENTS",
                FirmwareSetupFlowArea.SetupComponents,
                FirmwareSetupParityPriority.P0,
                FirmwareSetupParityDisposition.Complete,
                FirmwareSetupParityDisposition.Complete,
                "VGC.Setup.AutoPilotSetupComponentService",
                ["Project autopilot setup components"],
                "Shared setup component projection covers required/optional/ready/warning/blocked states."),
            new(
                "FW-313-SENSORS",
                FirmwareSetupFlowArea.SensorCalibration,
                FirmwareSetupParityPriority.P0,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Setup.SensorCalibrationWorkflow",
                ["Run sensor calibration workflow states", "Cancel and fail sensor calibration workflow", "Create calibration command boundaries"],
                "Compass, accelerometer, gyroscope, and level workflows are modeled; live vehicle prompts/transcripts remain open."),
            new(
                "FW-313-RADIO",
                FirmwareSetupFlowArea.RadioCalibration,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Setup.RadioCalibrationService",
                ["Project radio calibration and manual control"],
                "Channel min/max/trim and MANUAL_CONTROL boundaries exist; physical transmitter calibration evidence remains open."),
            new(
                "FW-313-POWER",
                FirmwareSetupFlowArea.PowerBattery,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Setup.PowerBatterySetupService",
                ["Project power battery setup metadata"],
                "Battery/failsafe setup rows are projected from metadata; real battery monitor validation remains open."),
            new(
                "FW-313-SAFETY",
                FirmwareSetupFlowArea.SafetyFailsafe,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Setup.MotorSafetyWorkflow",
                ["Run motor safety workflow states", "Create safety motor command boundary"],
                "Explicit confirmation and command boundaries exist; real hardware safety validation remains required."),
            new(
                "FW-313-MOTORS-ACTUATORS",
                FirmwareSetupFlowArea.MotorsActuators,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Blocked,
                "VGC.Setup.MotorSafetySetupService",
                ["Project motor safety setup boundaries"],
                "PX4 multirotor motor test boundary exists; ArduPilot actuator setup parity and live motor assignment evidence remain blocked."),
            new(
                "FW-313-FLIGHT-MODES",
                FirmwareSetupFlowArea.FlightModes,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Partial,
                FirmwareSetupParityDisposition.Partial,
                "VGC.Firmware.FirmwarePluginManager",
                ["Resolve vehicle standard modes", "Project autopilot setup components"],
                "Firmware mode metadata is available, but full QGC setup editing UX and vehicle write evidence remain open."),
            new(
                "FW-313-AIRFRAME",
                FirmwareSetupFlowArea.Airframe,
                FirmwareSetupParityPriority.P1,
                FirmwareSetupParityDisposition.Blocked,
                FirmwareSetupParityDisposition.Blocked,
                "Future firmware setup phase",
                [],
                "Airframe frame-class selection, geometry, and firmware-specific frame apply flows are not implemented."),
            new(
                "FW-313-RUNTIME-EVIDENCE",
                FirmwareSetupFlowArea.RuntimeEvidence,
                FirmwareSetupParityPriority.P0,
                FirmwareSetupParityDisposition.Blocked,
                FirmwareSetupParityDisposition.Blocked,
                "Phases 319-320",
                ["Catalog SITL hardware validation scenarios"],
                "SITL and physical-device setup/calibration transcripts remain later-phase gates.")
        ];
    }
}

public sealed record FirmwareSetupFlowRuntimeRow(
    string ComponentId,
    string Title,
    SetupComponentRequirement Requirement,
    VehicleSetupReadiness Readiness,
    string StatusText,
    IReadOnlyList<string> MissingParameters);

public sealed record FirmwareSetupFlowRuntimeProjection(
    MavAutopilot Autopilot,
    MavType VehicleType,
    string FirmwareName,
    bool HasMetadataPackage,
    IReadOnlyList<FirmwareSetupFlowRuntimeRow> Components,
    IReadOnlyList<FirmwareSetupParityItem> ParityItems,
    IReadOnlyList<string> BlockingReasons)
{
    public bool HasBlockedComponents => Components.Any(static component => component.Readiness == VehicleSetupReadiness.Blocked);
}

public sealed class FirmwareSetupFlowRuntimeProjector
{
    private readonly FirmwarePluginManager _firmwarePluginManager;
    private readonly FirmwareMetadataPackageRegistry _metadataRegistry;
    private readonly AutoPilotSetupComponentService _componentService;
    private readonly FirmwareSetupParityCatalog _parityCatalog;

    public FirmwareSetupFlowRuntimeProjector(
        FirmwarePluginManager? firmwarePluginManager = null,
        FirmwareMetadataPackageRegistry? metadataRegistry = null,
        AutoPilotSetupComponentService? componentService = null,
        FirmwareSetupParityCatalog? parityCatalog = null)
    {
        _firmwarePluginManager = firmwarePluginManager ?? new FirmwarePluginManager();
        _metadataRegistry = metadataRegistry ?? FirmwareMetadataPackageRegistry.CreateDefault();
        _componentService = componentService ?? new AutoPilotSetupComponentService();
        _parityCatalog = parityCatalog ?? new FirmwareSetupParityCatalog();
    }

    public FirmwareSetupFlowRuntimeProjection Project(
        MavAutopilot autopilot,
        MavType vehicleType,
        ParameterManager parameterManager)
    {
        var firmware = _firmwarePluginManager.GetPlugin(autopilot);
        var metadata = _metadataRegistry.Resolve(autopilot, vehicleType);
        var components = _componentService.Project(firmware, vehicleType, parameterManager)
            .Select(static component => new FirmwareSetupFlowRuntimeRow(
                component.Id,
                component.Title,
                component.Requirement,
                component.Readiness,
                component.StatusText,
                component.MissingParameters))
            .ToArray();
        var parityItems = _parityCatalog.Build();
        var blockingReasons = components
            .Where(static component => component.Readiness == VehicleSetupReadiness.Blocked)
            .Select(static component => $"{component.Title}: {component.StatusText}")
            .Concat(metadata is null ? [$"No metadata package for {autopilot}/{vehicleType}."] : [])
            .ToArray();

        return new FirmwareSetupFlowRuntimeProjection(
            autopilot,
            vehicleType,
            firmware.Name,
            metadata is not null,
            components,
            parityItems,
            blockingReasons);
    }
}

public sealed record FirmwareSetupParitySummary(
    int TotalAreas,
    int CompleteForPx4,
    int CompleteForArduPilot,
    int PartialForPx4,
    int PartialForArduPilot,
    int BlockedAreas,
    bool CanClaimQgcFirmwareSetupParity,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class FirmwareSetupParityAudit
{
    public FirmwareSetupParitySummary Audit(IReadOnlyList<FirmwareSetupParityItem> items)
    {
        var blockers = items
            .Where(static item => item.Priority is FirmwareSetupParityPriority.P0 or FirmwareSetupParityPriority.P1
                && (item.Px4Disposition is not FirmwareSetupParityDisposition.Complete
                    || item.ArduPilotDisposition is not FirmwareSetupParityDisposition.Complete))
            .Select(static item => $"{item.Id}: {item.Area} remains PX4={item.Px4Disposition}, ArduPilot={item.ArduPilotDisposition} ({item.VgcOwner}).")
            .ToArray();
        var completePx4 = items.Count(static item => item.Px4Disposition == FirmwareSetupParityDisposition.Complete);
        var completeApm = items.Count(static item => item.ArduPilotDisposition == FirmwareSetupParityDisposition.Complete);
        var partialPx4 = items.Count(static item => item.Px4Disposition == FirmwareSetupParityDisposition.Partial);
        var partialApm = items.Count(static item => item.ArduPilotDisposition == FirmwareSetupParityDisposition.Partial);
        var blocked = items.Count(static item => item.Px4Disposition == FirmwareSetupParityDisposition.Blocked
            || item.ArduPilotDisposition == FirmwareSetupParityDisposition.Blocked);
        var canClaim = blockers.Length == 0
            && items.All(static item => item.Px4Disposition == FirmwareSetupParityDisposition.Complete
                && item.ArduPilotDisposition == FirmwareSetupParityDisposition.Complete);

        return new FirmwareSetupParitySummary(
            items.Count,
            completePx4,
            completeApm,
            partialPx4,
            partialApm,
            blocked,
            canClaim,
            blockers,
            $"{completePx4}/{items.Count} PX4 setup areas complete; {completeApm}/{items.Count} ArduPilot setup areas complete; {blocked} areas blocked.");
    }
}
