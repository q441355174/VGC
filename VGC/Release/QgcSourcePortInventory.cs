namespace VGC.Release;

public enum QgcSourceAreaStatus
{
    Complete,
    Partial,
    Mapped,
    Deferred,
    NotApplicable,
    Blocked
}

public enum QgcSourceEvidenceLevel
{
    Static,
    Unit,
    DesktopRuntime,
    AndroidRuntime,
    Sitl,
    Hardware,
    Release
}

public sealed record QgcSourcePortInventoryItem(
    string QgcArea,
    int QmlFiles,
    int CppFiles,
    int HeaderFiles,
    string QgcSource,
    string VgcTarget,
    QgcSourceAreaStatus Status,
    QgcSourceEvidenceLevel RequiredEvidence,
    int EstimatedCoveragePercent,
    int Weight,
    string MissingWork);

public sealed record QgcSourcePortAuditResult(
    int AreaCount,
    int TotalQmlFiles,
    int TotalCppFiles,
    int TotalHeaderFiles,
    int CompleteAreas,
    int PartialAreas,
    int MappedAreas,
    int DeferredAreas,
    int BlockedAreas,
    int NotApplicableAreas,
    int WeightedCoveragePercent,
    bool CanClaimQgcSourceParity,
    bool CanClaimQgcReplacement,
    bool CanClaimReleaseCandidate,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class QgcSourcePortInventoryCatalog
{
    public IReadOnlyList<QgcSourcePortInventoryItem> Build() =>
    [
        new("QmlControls", 98, 32, 33, "src/QmlControls", "VGC.UI/Controls", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 78, 6, "Base controls/theme/dialogs exist; screenshot parity and full per-control behavior evidence remain open."),
        new("FlyView", 59, 0, 0, "src/FlyView; src/FlightMap/Widgets", "VGC/Views/FlyView.axaml; VGC/ViewModels/FlyViewModel.cs", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Sitl, 72, 8, "Fly UI, guided actions, payload and map paths exist; SITL, real vehicle, screenshot parity, and full QGC action menu behavior remain open."),
        new("FlightMap", 29, 0, 0, "src/FlightMap; src/QtLocationPlugin", "VGC.UI/Controls/MapControls.cs; VGC/Maps", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Sitl, 68, 8, "Desktop raster map, overlays and basic interaction exist; offline regions, Android map lifecycle, provider settings, screenshots and SITL evidence remain open."),
        new("PlanView", 43, 0, 0, "src/PlanView; src/MissionManager", "VGC/Views/PlanView.axaml; VGC/ViewModels/PlanViewModel.cs", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Sitl, 72, 8, "Mission/geofence/rally editing and transfer models exist; full complex item authoring, terrain workflow, SITL and upload/download transcripts remain open."),
        new("AnalyzeView", 18, 22, 24, "src/AnalyzeView; src/LogCompressor", "VGC/Analyze; VGC/ViewModels/AnalyzeViewModel.cs", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 70, 6, "Inspector, replay, console, charts, log parsing and geotag models exist; real PX4/APM log packs and screenshot parity remain open."),
        new("AppSettings", 29, 0, 0, "src/Settings", "VGC/Views/SettingsView.axaml; VGC/Core/Settings", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 55, 5, "Settings persistence and grouped UI exist; full QGC settings pages, map/offline settings, signing UI and device evidence remain open."),
        new("Settings", 0, 27, 27, "src/Settings; src/Comms", "VGC/Core/Settings; VGC/Comms", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 55, 5, "Settings store and comm link configuration exist; complete QGC settings backend parity remains open."),
        new("AutoPilotPlugins", 84, 55, 55, "src/AutoPilotPlugins", "VGC/Firmware; VGC/Setup", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 58, 8, "PX4/APM profiles, setup surfaces and firmware metadata exist; airframe, calibration depth, live firmware behavior and hardware evidence remain open."),
        new("FirmwarePlugin", 7, 14, 16, "src/FirmwarePlugin", "VGC/Firmware", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Sitl, 62, 7, "Command availability, modes and firmware profiles exist; full QGC plugin behavior and SITL evidence remain open."),
        new("FactSystem", 18, 10, 10, "src/FactSystem", "VGC/Facts; VGC.UI/Controls/FactControls.cs", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Unit, 70, 6, "Fact metadata, validation and controls exist; full QGC metadata and edge-case behavior parity remain open."),
        new("Vehicle", 12, 62, 64, "src/Vehicle", "VGC/Vehicles; VGC/Mavlink", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 60, 10, "Multi-vehicle telemetry, status groups and command boundaries exist; full QGC Vehicle, failsafe, terrain, avoidance, signing and hardware evidence remain open."),
        new("MissionManager", 0, 37, 39, "src/MissionManager", "VGC/Mission", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Sitl, 72, 9, "Plan import/export, mission transfer, geofence/rally and complex previews exist; protocol completeness and SITL upload/download evidence remain open."),
        new("Comms", 0, 24, 24, "src/Comms", "VGC/Comms", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 68, 9, "Serial/TCP/UDP/mock/replay links exist; Bluetooth, Android USB/device field validation and link recovery parity remain open."),
        new("MAVLink", 0, 14, 19, "src/Comms/MAVLinkProtocol.*; src/MAVLink", "VGC/Mavlink", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Unit, 65, 9, "Parser/writer/CRC and selected message models exist; full dialect coverage, signing and protocol edge cases remain open."),
        new("GPS", 3, 17, 25, "src/GPS; src/PositionManager", "VGC/Positioning", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 55, 5, "NMEA/RTK/NTRIP state exists; real GPS, permissions and mobile field evidence remain open."),
        new("VideoManager", 0, 46, 55, "src/VideoManager", "VGC/Payload", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 45, 7, "Video stream model exists; real stream pipeline, decoding/rendering and payload evidence remain open."),
        new("Camera", 0, 7, 7, "src/Camera; src/Gimbal", "VGC/Payload", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 55, 5, "Camera/gimbal command models exist; real camera capability and media workflow evidence remain open."),
        new("Terrain", 0, 6, 7, "src/Terrain", "VGC/Terrain", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 45, 4, "Terrain cache/query and altitude preview exist; real terrain service evidence and full QGC terrain workflow remain open."),
        new("Viewer3D", 8, 11, 13, "src/Viewer3D", "Deferred", QgcSourceAreaStatus.Deferred, QgcSourceEvidenceLevel.DesktopRuntime, 0, 3, "3D viewer migration is deferred."),
        new("Android", 0, 4, 10, "src/Android; android/src/org/mavlink/qgroundcontrol", "VGC.Android; VGC/Comms/Android*", QgcSourceAreaStatus.Blocked, QgcSourceEvidenceLevel.AndroidRuntime, 30, 8, "Android project and native integration stubs exist; workload/device validation and native serial parity remain blocked."),
        new("ADSB", 0, 3, 4, "src/ADSB", "VGC/Traffic", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Hardware, 45, 3, "Traffic model exists; live ADSB source and map/runtime evidence remain open."),
        new("LogManager", 1, 5, 5, "src/AnalyzeView; src/LogCompressor", "VGC/Analyze", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 65, 4, "Log parsers/download/geotag workflows exist; real log corpus and UI runtime evidence remain open."),
        new("Utilities", 0, 88, 95, "src/Utilities", "VGC/Core; VGC/Validation; VGC/Release", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.Unit, 50, 4, "Core utilities are replaced selectively; not all QGC utility helpers are mapped one-to-one."),
        new("QtLocationPlugin", 0, 0, 26, "src/QtLocationPlugin", "VGC/Maps", QgcSourceAreaStatus.Partial, QgcSourceEvidenceLevel.DesktopRuntime, 45, 4, "Map provider abstraction exists; QtLocation plugin parity is intentionally replaced by Avalonia/Mapsui-style rendering."),
        new("API", 0, 3, 3, "src/API", "VGC/Composition; VGC/ViewModels", QgcSourceAreaStatus.Mapped, QgcSourceEvidenceLevel.Static, 40, 3, "Core plugin extension points are mapped to composition/view-model boundaries; full public plugin API parity is not claimed."),
        new("ReleasePackaging", 0, 0, 0, "deploy; package; cmake", "VGC/Release", QgcSourceAreaStatus.Blocked, QgcSourceEvidenceLevel.Release, 25, 8, "Release blockers are cataloged; signed Android package, desktop artifact and final evidence pack are missing.")
    ];
}

public sealed class QgcSourcePortAudit
{
    public QgcSourcePortAuditResult Audit(IReadOnlyList<QgcSourcePortInventoryItem> inventory)
    {
        var totalWeight = inventory.Sum(static item => item.Weight);
        var weightedCoverage = totalWeight == 0
            ? 0
            : (int)Math.Round(inventory.Sum(static item => item.EstimatedCoveragePercent * item.Weight) / (double)totalWeight);
        var blockers = inventory
            .Where(static item => item.Status != QgcSourceAreaStatus.Complete && item.Status != QgcSourceAreaStatus.NotApplicable)
            .Select(static item => $"{item.QgcArea}: {item.MissingWork}")
            .ToArray();

        return new QgcSourcePortAuditResult(
            inventory.Count,
            inventory.Sum(static item => item.QmlFiles),
            inventory.Sum(static item => item.CppFiles),
            inventory.Sum(static item => item.HeaderFiles),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.Complete),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.Partial),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.Mapped),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.Deferred),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.Blocked),
            inventory.Count(static item => item.Status == QgcSourceAreaStatus.NotApplicable),
            weightedCoverage,
            false,
            false,
            false,
            blockers,
            $"QGC source audit covers {inventory.Sum(static item => item.QmlFiles)} QML, {inventory.Sum(static item => item.CppFiles)} C++ and {inventory.Sum(static item => item.HeaderFiles)} header files; replacement remains blocked at {weightedCoverage}% estimated coverage.");
    }
}
