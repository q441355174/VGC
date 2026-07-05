namespace VGC.ViewModels;

public enum QgcQmlParityStatus
{
    Blocked,
    Mapped,
    Migrated,
    Merged,
    NotApplicable,
    Deferred,
    Complete
}

public enum QgcQmlEvidenceLevel
{
    Static,
    Implementation,
    Behavior,
    Screenshot,
    Runtime,
    Android,
    Sitl,
    Hardware,
    Release
}

public sealed record QgcQmlModuleInventoryItem(
    string Module,
    int FileCount,
    UiWorkflowArea Area,
    string VgcTarget,
    QgcQmlParityStatus Status,
    QgcQmlEvidenceLevel RequiredEvidence,
    string Blocker);

public sealed record QgcQmlParityAuditResult(
    int ModuleCount,
    int TotalQmlFiles,
    int MappedModules,
    int BlockedModules,
    int DeferredModules,
    int CompleteModules,
    bool CanClaimQmlUiParity,
    bool CanClaimQgcReplacement,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class QgcQmlParityCatalog
{
    public IReadOnlyList<QgcQmlModuleInventoryItem> Build() =>
    [
        new("QmlControls", 98, UiWorkflowArea.Platform, "VGC.UI/Controls", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Implementation, "QgcTheme, ScreenLayout, dialogs, shell buttons, and base controls migrated; screenshot parity remains open."),
        new("AutoPilotPlugins", 84, UiWorkflowArea.Setup, "VGC/Views/SetupView.axaml", QgcQmlParityStatus.Mapped, QgcQmlEvidenceLevel.Behavior, "Setup shell, component flows, safety, sensors, radio, and flight-mode surfaces are mapped; firmware-specific calibration, airframe, SITL, and device evidence remain open."),
        new("FlyView", 59, UiWorkflowArea.Fly, "VGC/Views/FlyView.axaml", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, new QgcQmlParitySubEvidenceCatalog().BuildBlockerText("FlyView")),
        new("PlanView", 43, UiWorkflowArea.Plan, "VGC/Views/PlanView.axaml", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, new QgcQmlParitySubEvidenceCatalog().BuildBlockerText("PlanView")),
        new("AppSettings", 29, UiWorkflowArea.Settings, "VGC/Views/SettingsView.axaml", QgcQmlParityStatus.Mapped, QgcQmlEvidenceLevel.Behavior, "Settings groups, persistence, navigation, and comm-link lifecycle are mapped; full QGC settings runtime and device evidence remain open."),
        new("FlightMap", 29, UiWorkflowArea.Fly, "VGC.UI/Controls/MapControls.cs", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, new QgcQmlParitySubEvidenceCatalog().BuildBlockerText("FlightMap")),
        new("Toolbar", 29, UiWorkflowArea.Shell, "VGC.UI/Controls/ToolbarIndicators.cs", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Implementation, "Toolbar indicator controls migrated; runtime and screenshot evidence remain open."),
        new("AnalyzeView", 18, UiWorkflowArea.Analyze, "VGC/Views/AnalyzeView.axaml", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, "Analyze inspector, replay, console, telemetry chart, and bound workflow paths are migrated; screenshot, real log, SITL, and device runtime evidence remain open."),
        new("FactSystem", 18, UiWorkflowArea.Parameters, "VGC.UI/Controls/FactControls.cs", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Implementation, "Fact text, combo, slider, label, unit, bitmask, and table controls migrated; per-control behavior evidence remains open."),
        new("Vehicle", 12, UiWorkflowArea.Fly, "VGC/ViewModels/FlyViewModel.cs", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, "Vehicle runtime, multi-vehicle heartbeat/link handling, status groups, modes, trajectory, and command boundaries are migrated; object avoidance, signing UX, SITL, and real vehicle evidence remain open."),
        new("Viewer3D", 8, UiWorkflowArea.Platform, "Deferred", QgcQmlParityStatus.Deferred, QgcQmlEvidenceLevel.Implementation, "Viewer3D migration deferred until 3D view is prioritized."),
        new("FirmwarePlugin", 7, UiWorkflowArea.Setup, "VGC/Firmware", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Behavior, "Firmware metadata, setup runtime, component parity, and plugin selection paths are migrated; airframe, live firmware behavior, SITL, and device evidence remain open."),
        new("GPS", 3, UiWorkflowArea.Settings, "VGC/Positioning", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, "Native/external GPS, NMEA parsing, source selection, permissions, RTK/NTRIP state, and FollowMe boundaries are migrated; real GPS/NTRIP and mobile field evidence remain open."),
        new("FirstRunPromptDialogs", 2, UiWorkflowArea.Shell, "VGC/Core/FirstRunPromptService.cs", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Behavior, "Startup prompt sequencing and dismiss flow are migrated; persistence, screenshots, and full QGC first-run UX evidence remain open."),
        new("MainWindow", 2, UiWorkflowArea.Shell, "VGC/Views/MainWindow.axaml", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Screenshot, "Avalonia desktop shell host and MainView layout are migrated; screenshot, resize/platform shell, and native window chrome parity evidence remain open."),
        new("LogManager", 1, UiWorkflowArea.Analyze, "VGC/Analyze", QgcQmlParityStatus.Migrated, QgcQmlEvidenceLevel.Runtime, "Log manager parsers, download workflow, PX4 metadata, geotag, file-log, and replay projectors are migrated in shared Analyze runtime; real PX4/ArduPilot logs, SITL transcripts, and UI runtime evidence remain open.")
    ];
}

public sealed class QgcQmlParityAudit
{
    public QgcQmlParityAuditResult Audit(IReadOnlyList<QgcQmlModuleInventoryItem> inventory)
    {
        var blockers = inventory
            .Where(static item => item.Status is QgcQmlParityStatus.Blocked or QgcQmlParityStatus.Deferred or QgcQmlParityStatus.Mapped or QgcQmlParityStatus.Migrated)
            .Select(static item => $"{item.Module}: {item.Blocker}")
            .ToArray();
        var complete = inventory.Count(static item => item.Status == QgcQmlParityStatus.Complete);
        var totalFiles = inventory.Sum(static item => item.FileCount);

        return new QgcQmlParityAuditResult(
            inventory.Count,
            totalFiles,
            inventory.Count(static item => item.Status == QgcQmlParityStatus.Mapped),
            inventory.Count(static item => item.Status == QgcQmlParityStatus.Blocked),
            inventory.Count(static item => item.Status == QgcQmlParityStatus.Deferred),
            complete,
            CanClaimQmlUiParity: complete == inventory.Count && totalFiles == 442,
            CanClaimQgcReplacement: false,
            blockers,
            $"{totalFiles} QGC QML files cataloged across {inventory.Count} modules; {blockers.Length} modules still require migration evidence.");
    }
}
