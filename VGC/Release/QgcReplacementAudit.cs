using VGC.ViewModels;

namespace VGC.Release;

public enum QgcReplacementPhase
{
    UiWorkflowHardening = 314,
    MapRuntimeParity = 315,
    PayloadRuntimeParity = 316,
    VehicleSafetyParity = 317,
    AndroidNativePlatformParity = 318,
    DesktopRuntimeValidation = 319,
    SitlValidationExecution = 320,
    ReleaseCandidatePackaging = 321,
    FinalReplacementAcceptance = 322
}

public enum QgcReplacementEvidenceStatus { Complete, Blocked, Deferred }
public enum QgcReplacementEvidenceLevel { Static, Unit, DesktopRuntime, AndroidRuntime, Sitl, RealHardware, Release }

public sealed record QgcReplacementEvidenceItem(string Id, QgcReplacementPhase Phase, QgcReplacementEvidenceStatus Status, QgcReplacementEvidenceLevel RequiredEvidence, IReadOnlyList<string> RequiredArtifacts, string Blocker = "");
public sealed record QgcReplacementPhaseStatus(QgcReplacementPhase Phase, int Total, int Complete, int Blocked, int Deferred);
public sealed record QgcReplacementAcceptanceResult(int TotalItems, int CompleteItems, int BlockedItems, int DeferredItems, IReadOnlyList<QgcReplacementPhaseStatus> PhaseStatuses, bool CanClaimQgcReplacement, bool CanClaimReleaseCandidate, IReadOnlyList<string> OpenBlockers);
public sealed record QgcReplacementEvidencePackEntry(QgcReplacementPhase Phase, string Document, IReadOnlyList<string> VerificationCommands, IReadOnlyList<string> RequiredExternalArtifacts);

public sealed class QgcReplacementPhaseEvidenceCatalog
{
    public IReadOnlyList<QgcReplacementEvidenceItem> Build() =>
    [
        new("QGCREPL-314-UI-SHARED", QgcReplacementPhase.UiWorkflowHardening, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Unit, ["UI workflow tests"]),
        new("QGCREPL-314-QML-INVENTORY", QgcReplacementPhase.UiWorkflowHardening, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Static, ["QGC QML inventory/parity catalog"]),
        new("QGCREPL-315-MAP-RUNTIME", QgcReplacementPhase.MapRuntimeParity, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Unit, ["map runtime tests"]),
        new("QGCREPL-316-STREAM", QgcReplacementPhase.PayloadRuntimeParity, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Unit, ["synthetic frame decode test"], ""),
        new("QGCREPL-317-SAFETY", QgcReplacementPhase.VehicleSafetyParity, QgcReplacementEvidenceStatus.Blocked, QgcReplacementEvidenceLevel.RealHardware, ["real vehicle checklist"], "real vehicle safety evidence missing."),
        new("QGCREPL-318-ANDROID", QgcReplacementPhase.AndroidNativePlatformParity, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.AndroidRuntime, ["Android emulator log"], ""),
        new("QGCREPL-319-DESKTOP", QgcReplacementPhase.DesktopRuntimeValidation, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.DesktopRuntime, ["desktop smoke test"]),
        new("QGCREPL-320-PX4", QgcReplacementPhase.SitlValidationExecution, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Sitl, ["SITL TCP transcript"], ""),
        new("QGCREPL-320-APM", QgcReplacementPhase.SitlValidationExecution, QgcReplacementEvidenceStatus.Blocked, QgcReplacementEvidenceLevel.Sitl, ["ArduPilot SITL transcript"], "ArduPilot SITL transcript missing."),
        new("QGCREPL-321-ANDROID-SIGNED", QgcReplacementPhase.ReleaseCandidatePackaging, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Release, ["signed Android package"], ""),
        new("QGCREPL-321-DESKTOP-PACK", QgcReplacementPhase.ReleaseCandidatePackaging, QgcReplacementEvidenceStatus.Complete, QgcReplacementEvidenceLevel.Release, ["desktop release artifact"], ""),
        new("QGCREPL-322-FINAL", QgcReplacementPhase.FinalReplacementAcceptance, QgcReplacementEvidenceStatus.Deferred, QgcReplacementEvidenceLevel.Release, ["final audit", "Release evidence pack"], "Release evidence pack and final audit missing.")
    ];
}

public sealed class QgcReplacementAcceptanceAudit
{
    public QgcReplacementAcceptanceResult Audit(IReadOnlyList<QgcReplacementEvidenceItem> evidence)
    {
        var phases = evidence.GroupBy(static item => item.Phase)
            .OrderBy(static group => group.Key)
            .Select(static group => new QgcReplacementPhaseStatus(
                group.Key,
                group.Count(),
                group.Count(static item => item.Status == QgcReplacementEvidenceStatus.Complete),
                group.Count(static item => item.Status == QgcReplacementEvidenceStatus.Blocked),
                group.Count(static item => item.Status == QgcReplacementEvidenceStatus.Deferred)))
            .ToArray();
        var blockers = evidence.Where(static item => item.Status != QgcReplacementEvidenceStatus.Complete).Select(static item => item.Blocker).ToArray();
        return new QgcReplacementAcceptanceResult(
            evidence.Count,
            evidence.Count(static item => item.Status == QgcReplacementEvidenceStatus.Complete),
            evidence.Count(static item => item.Status == QgcReplacementEvidenceStatus.Blocked),
            evidence.Count(static item => item.Status == QgcReplacementEvidenceStatus.Deferred),
            phases,
            false,
            false,
            blockers);
    }
}

public sealed record QgcReplacementFinalAuditResult(
    int TotalQgcQmlFiles,
    int MappedModules,
    int MigratedModules,
    int DeferredModules,
    int CompleteModules,
    int RuntimeEvidenceBlockers,
    int ReplacementEvidenceBlockedItems,
    int ReplacementEvidenceDeferredItems,
    bool CanClaimQmlUiParity,
    bool CanClaimQgcReplacement,
    bool CanClaimReleaseCandidate,
    bool AndroidWorkloadBlocked,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class QgcReplacementFinalAudit
{
    public QgcReplacementFinalAuditResult Audit(
        IReadOnlyList<QgcQmlModuleInventoryItem> qmlInventory,
        IReadOnlyList<Gate11RuntimeEvidenceItem> runtimeEvidence,
        QgcReplacementAcceptanceResult replacement)
    {
        var qmlAudit = new QgcQmlParityAudit().Audit(qmlInventory);
        var runtimeBlockers = runtimeEvidence.Where(static item => !item.Complete).Select(static item => item.Notes).ToArray();
        var blockers = qmlAudit.OpenBlockers
            .Concat(runtimeBlockers)
            .Concat(replacement.OpenBlockers)
            .Where(static blocker => !string.IsNullOrWhiteSpace(blocker))
            .ToArray();

        return new QgcReplacementFinalAuditResult(
            qmlAudit.TotalQmlFiles,
            qmlInventory.Count(static item => item.Status == QgcQmlParityStatus.Mapped),
            qmlInventory.Count(static item => item.Status == QgcQmlParityStatus.Migrated),
            qmlInventory.Count(static item => item.Status == QgcQmlParityStatus.Deferred),
            qmlInventory.Count(static item => item.Status == QgcQmlParityStatus.Complete),
            runtimeEvidence.Count(static item => !item.Complete),
            replacement.BlockedItems,
            replacement.DeferredItems,
            false,
            false,
            false,
            runtimeEvidence.Any(static item => item.Id == "GATE11-ANDROID-WORKLOAD" && item.Status == Gate11RuntimeEvidenceStatus.Blocked),
            blockers,
            $"{qmlAudit.TotalQmlFiles} QGC QML files audited; QGC replacement remains blocked.");
    }
}

public sealed class QgcReplacementEvidencePackPlanner
{
    public IReadOnlyList<QgcReplacementEvidencePackEntry> Build(IReadOnlyList<QgcReplacementEvidenceItem> evidence) =>
        evidence.Select(static item => item.Phase).Distinct().OrderBy(static phase => phase).Select(BuildEntry).ToArray();

    private static QgcReplacementEvidencePackEntry BuildEntry(QgcReplacementPhase phase) => phase switch
    {
        QgcReplacementPhase.AndroidNativePlatformParity => new(phase, "PHASE-318-VERIFICATION.md", ["dotnet build VGC.Android"], ["Android device transcript"]),
        QgcReplacementPhase.SitlValidationExecution => new(phase, "PHASE-320-VERIFICATION.md", ["dotnet test VGC.Tests"], ["PX4 SITL transcript", "ArduPilot SITL transcript"]),
        QgcReplacementPhase.ReleaseCandidatePackaging => new(phase, "PHASE-321-VERIFICATION.md", ["dotnet build VGC.Desktop", "dotnet build VGC.Android"], ["signed Android package", "desktop release artifact"]),
        QgcReplacementPhase.FinalReplacementAcceptance => new(phase, "PHASE-322-VERIFICATION.md", ["dotnet test VGC.Tests"], ["final audit artifact", "Release evidence pack"]),
        _ => new(phase, $"PHASE-{(int)phase}-VERIFICATION.md", ["dotnet test VGC.Tests"], [])
    };
}
