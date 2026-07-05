namespace VGC.Release;

public enum FullPortPriority { P0, P1, P2 }
public enum FullPortDisposition { Complete, Partial, Deferred, NotApplicable, Alternative }
public enum FullPortEvidenceKind { Tests, AndroidDevice, Sitl, RealHardware, ReleaseArtifact, Audit }

public sealed record FullPortModuleAssessment(string Id, string QgcArea, FullPortPriority Priority, FullPortDisposition Disposition, int CoveragePercent, int Weight);
public sealed record FullPortEvidenceGate(string Id, FullPortEvidenceKind Kind, bool Complete, string Blocker = "");
public sealed record FullPortDecision(string Id, FullPortDisposition Disposition, string ReopenCondition);
public sealed record FullPortReleaseBlocker(string Id, FullPortPriority Priority, string Description, string RequiredEvidence);
public sealed record FullPortFinalAuditResult(int AssessedModules, int CompleteModules, int PartialModules, int DeferredModules, int NotApplicableModules, int WeightedCoveragePercent, bool CanClaimFullPortComplete, bool CanClaimReleaseCandidate, IReadOnlyList<string> OpenBlockers, string Summary);
public sealed record FullPortEvidenceItem(string Id, string Description, bool Complete);

public sealed class FullPortModuleMatrixCatalog
{
    public IReadOnlyList<FullPortModuleAssessment> BuildV155Matrix() =>
    [
        new("FULL-303-VEHICLE", "Vehicle management", FullPortPriority.P0, FullPortDisposition.Partial, 65, 10),
        new("FULL-303-MAVLINK", "MAVLink protocol", FullPortPriority.P0, FullPortDisposition.Partial, 70, 10),
        new("FULL-303-COMMS", "Comms links", FullPortPriority.P0, FullPortDisposition.Partial, 75, 8),
        new("FULL-303-MISSION", "Mission planning", FullPortPriority.P0, FullPortDisposition.Complete, 90, 8),
        new("FULL-303-SETUP", "Setup", FullPortPriority.P1, FullPortDisposition.Partial, 65, 6),
        new("FULL-303-ANALYZE", "Analyze tools", FullPortPriority.P1, FullPortDisposition.Complete, 85, 6),
        new("FULL-303-MAPS", "Maps", FullPortPriority.P1, FullPortDisposition.Partial, 60, 6),
        new("FULL-303-PAYLOAD", "Payload", FullPortPriority.P1, FullPortDisposition.Partial, 55, 5),
        new("FULL-303-ANDROID", "Android", FullPortPriority.P0, FullPortDisposition.Partial, 45, 6),
        new("FULL-303-FIRMWARE", "Firmware", FullPortPriority.P1, FullPortDisposition.Partial, 55, 5),
        new("FULL-303-VALIDATION", "Validation", FullPortPriority.P0, FullPortDisposition.Deferred, 30, 7),
        new("FULL-303-RELEASE", "Release", FullPortPriority.P0, FullPortDisposition.Deferred, 25, 7),
        new("FULL-306-QML", "QML UI", FullPortPriority.P2, FullPortDisposition.NotApplicable, 0, 1),
        new("FULL-306-MOBILE-IOS", "iOS", FullPortPriority.P2, FullPortDisposition.NotApplicable, 0, 1)
    ];
}

public sealed class FullPortEvidenceGateCatalog
{
    public IReadOnlyList<FullPortEvidenceGate> BuildV155Gates() =>
    [
        new("GATE-304-TESTS", FullPortEvidenceKind.Tests, true),
        new("GATE-304-AUDIT", FullPortEvidenceKind.Audit, true),
        new("GATE-304-ANDROID", FullPortEvidenceKind.AndroidDevice, false, "Android device evidence missing."),
        new("GATE-304-PX4-SITL", FullPortEvidenceKind.Sitl, false, "PX4 SITL transcript missing."),
        new("GATE-304-APM-SITL", FullPortEvidenceKind.Sitl, false, "ArduPilot SITL transcript missing."),
        new("GATE-304-REAL", FullPortEvidenceKind.RealHardware, false, "real vehicle validation missing."),
        new("GATE-304-DESKTOP-RELEASE", FullPortEvidenceKind.ReleaseArtifact, false, "Desktop release artifact missing."),
        new("GATE-304-ANDROID-RELEASE", FullPortEvidenceKind.ReleaseArtifact, false, "Android signed release artifact missing.")
    ];
}

public sealed class FullPortDecisionCatalog
{
    public IReadOnlyList<FullPortDecision> BuildV155Decisions() =>
    [
        new("DEC-305-QML", FullPortDisposition.NotApplicable, "Reopen only if Avalonia is replaced by QML."),
        new("DEC-305-MAPSUI", FullPortDisposition.Alternative, "Reopen if Mapsui cannot satisfy map UX."),
        new("DEC-305-SITL", FullPortDisposition.Deferred, "Reopen when simulator is available."),
        new("DEC-305-HARDWARE", FullPortDisposition.Deferred, "Reopen when real vehicle is available."),
        new("DEC-305-IOS", FullPortDisposition.NotApplicable, "Reopen when an iOS project exists."),
        new("DEC-305-ANDROID", FullPortDisposition.Partial, "Reopen after device matrix passes."),
        new("DEC-305-RELEASE", FullPortDisposition.Deferred, "Reopen after signed release artifacts exist.")
    ];
}

public sealed class FullPortReleaseBlockerCatalog
{
    public IReadOnlyList<FullPortReleaseBlocker> BuildV155Blockers() =>
    [
        new("BLOCK-306-DESKTOP-PUBLISH", FullPortPriority.P0, "Desktop release artifact has not been published.", "dotnet publish output"),
        new("BLOCK-306-ANDROID-SIGNED", FullPortPriority.P0, "Android signed package is missing.", "signed APK/AAB"),
        new("BLOCK-306-PX4-SITL", FullPortPriority.P0, "PX4 SITL validation transcript is missing.", "PX4 SITL log"),
        new("BLOCK-306-APM-SITL", FullPortPriority.P0, "ArduPilot SITL validation transcript is missing.", "ArduPilot SITL log"),
        new("BLOCK-306-REAL-VEHICLE", FullPortPriority.P0, "real vehicle validation is missing.", "real vehicle safety checklist"),
        new("BLOCK-306-ANDROID-DEVICE", FullPortPriority.P0, "Android device matrix is incomplete.", "device matrix transcript"),
        new("BLOCK-306-RELEASE-EVIDENCE", FullPortPriority.P0, "Release artifacts and evidence pack are incomplete.", "Release evidence pack")
    ];
}

public sealed class FullPortFinalAudit
{
    public FullPortFinalAuditResult Audit(IReadOnlyList<FullPortModuleAssessment> modules, IReadOnlyList<FullPortEvidenceGate> gates, IReadOnlyList<FullPortReleaseBlocker> blockers)
    {
        var open = modules.Where(static module => module.Disposition is FullPortDisposition.Partial or FullPortDisposition.Deferred).Select(static module => $"{module.QgcArea} remains {module.Disposition}.")
            .Concat(gates.Where(static gate => !gate.Complete).Select(static gate => gate.Blocker))
            .Concat(blockers.Select(static blocker => blocker.Description))
            .ToArray();
        var weightedCoverage = (int)Math.Round(modules.Sum(static module => module.CoveragePercent * module.Weight) / (double)modules.Sum(static module => module.Weight));
        return new FullPortFinalAuditResult(
            modules.Count,
            modules.Count(static module => module.Disposition == FullPortDisposition.Complete),
            modules.Count(static module => module.Disposition == FullPortDisposition.Partial),
            modules.Count(static module => module.Disposition == FullPortDisposition.Deferred),
            modules.Count(static module => module.Disposition == FullPortDisposition.NotApplicable),
            weightedCoverage,
            false,
            false,
            open,
            $"Full-port audit remains blocked at {weightedCoverage}% weighted coverage; release candidate blocked.");
    }
}

public sealed class FullPortEvidenceCatalog
{
    public IReadOnlyList<FullPortEvidenceItem> BuildV155Evidence() =>
    [
        new("FULL-303", "Final module matrix complete without claiming full parity.", true),
        new("FULL-304", "Evidence gates catalog complete.", true),
        new("FULL-305", "Deferred and not-applicable decisions recorded.", true),
        new("FULL-306", "Release blocker catalog complete.", true),
        new("FULL-307", "Final audit result blocks overclaim.", true),
        new("FULL-308", "Risk register consolidated.", true),
        new("FULL-309", "QGC replacement boundary documented.", true),
        new("FULL-310", "Final audit closes assessment without claiming QGC replacement.", true)
    ];
}
