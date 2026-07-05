namespace VGC.Release;

public enum ReleaseItemStatus { Complete, Blocked }
public enum ReleaseEvidenceLevel { Static, PackagedArtifact, SignedArtifact, DeviceRuntime, Sitl }
public enum ReleaseReadinessArea { License, Packaging, Signing, DeviceValidation, PlatformScope, SitlValidation, OverclaimBoundary }
public enum ReleasePlatformTarget { Desktop, Android, Browser, Ios }
public enum LicenseSourceKind { ThirdPartyNotice, ReferenceOnly, ExternalSecret }
public enum PlatformScopeDecisionStatus { InScope, OutOfScope }

public sealed record ReleaseReadinessItem(string Id, ReleaseReadinessArea Area, ReleaseItemStatus Status, ReleaseEvidenceLevel RequiredEvidence, string Blocker = "");
public sealed record ReleaseLicenseItem(string Component, LicenseSourceKind Kind, bool BundledInRelease, bool RequiresAttribution);
public sealed record LicenseSourceAuditResult(bool CanBuildReleaseInventory, int AttributionRequired, int BlockingIssues);
public sealed record ReleasePackagePlan(ReleasePlatformTarget Target, string VersionName, bool RequiresSigning, bool RequiresExternalSecret);
public sealed record ReleasePackageEvaluation(bool CanProduceUnsignedArtifact, bool CanProduceSignedArtifact, IReadOnlyList<string> MissingInputs);
public sealed record AndroidReleaseDeviceCheck(string Id, string Workflow, bool Complete, string Blocker);
public sealed record PlatformScopeDecision(ReleasePlatformTarget Target, PlatformScopeDecisionStatus Status, string Reason);
public sealed record ReleaseClosureSummary(int RequiredItems, int CompleteItems, int BlockedItems, bool CanClaimReleaseCandidate, IReadOnlyList<string> OpenBlockers, string Summary);
public sealed record ReleaseEvidenceItem(string Id, string Description, bool Complete);

public sealed class ReleaseReadinessCatalog
{
    public IReadOnlyList<ReleaseReadinessItem> BuildV154Items() =>
    [
        new("REL-295-LICENSE-AUDIT", ReleaseReadinessArea.License, ReleaseItemStatus.Complete, ReleaseEvidenceLevel.Static),
        new("REL-296-NOTICES", ReleaseReadinessArea.License, ReleaseItemStatus.Complete, ReleaseEvidenceLevel.Static),
        new("REL-297-DESKTOP-PACKAGE", ReleaseReadinessArea.Packaging, ReleaseItemStatus.Blocked, ReleaseEvidenceLevel.PackagedArtifact, "Desktop release artifact missing."),
        new("REL-298-ANDROID-SIGNING", ReleaseReadinessArea.Signing, ReleaseItemStatus.Blocked, ReleaseEvidenceLevel.SignedArtifact, "Android signed APK requires external signing secret."),
        new("REL-299-ANDROID-DEVICE", ReleaseReadinessArea.DeviceValidation, ReleaseItemStatus.Blocked, ReleaseEvidenceLevel.DeviceRuntime, "Android device validation not run."),
        new("REL-300-BROWSER-IOS-SCOPE", ReleaseReadinessArea.PlatformScope, ReleaseItemStatus.Complete, ReleaseEvidenceLevel.Static),
        new("REL-301-SITL-HARDWARE", ReleaseReadinessArea.SitlValidation, ReleaseItemStatus.Blocked, ReleaseEvidenceLevel.Sitl, "SITL and hardware transcripts missing."),
        new("REL-302-OVERCLAIM", ReleaseReadinessArea.OverclaimBoundary, ReleaseItemStatus.Complete, ReleaseEvidenceLevel.Static)
    ];
}

public sealed class ReleaseLicenseCatalog
{
    public IReadOnlyList<ReleaseLicenseItem> BuildV154Inventory() =>
    [
        new("VGC", LicenseSourceKind.ThirdPartyNotice, true, false),
        new("Avalonia", LicenseSourceKind.ThirdPartyNotice, true, true),
        new("Mapsui", LicenseSourceKind.ThirdPartyNotice, true, true),
        new("QGroundControl reference source", LicenseSourceKind.ReferenceOnly, false, true),
        new("Android signing keystore", LicenseSourceKind.ExternalSecret, false, false)
    ];
}

public sealed class LicenseSourceAudit
{
    public LicenseSourceAuditResult Audit(IReadOnlyList<ReleaseLicenseItem> inventory) => new(
        CanBuildReleaseInventory: inventory.All(static item => item.BundledInRelease || item.Kind is LicenseSourceKind.ReferenceOnly or LicenseSourceKind.ExternalSecret),
        AttributionRequired: inventory.Count(static item => item.RequiresAttribution),
        BlockingIssues: inventory.Count(static item => item.BundledInRelease && item.Kind == LicenseSourceKind.ExternalSecret));
}

public sealed class ReleasePackagePlanner
{
    public IReadOnlyList<ReleasePackagePlan> BuildV154Plans() =>
    [
        new(ReleasePlatformTarget.Desktop, "1.54.0-internal", RequiresSigning: false, RequiresExternalSecret: false),
        new(ReleasePlatformTarget.Android, "1.54.0-internal", RequiresSigning: true, RequiresExternalSecret: true)
    ];

    public ReleasePackageEvaluation Evaluate(ReleasePackagePlan plan, bool hasPublishOutput, bool hasExternalSigningSecret)
    {
        var missing = new List<string>();
        if (!hasPublishOutput) missing.Add($"{plan.Target} publish output");
        if (plan.RequiresExternalSecret && !hasExternalSigningSecret) missing.Add($"{plan.Target} signing secret");
        return new ReleasePackageEvaluation(hasPublishOutput, hasPublishOutput && (!plan.RequiresSigning || !plan.RequiresExternalSecret || hasExternalSigningSecret), missing);
    }
}

public sealed class AndroidReleaseDeviceMatrix
{
    public IReadOnlyList<AndroidReleaseDeviceCheck> BuildV154Matrix() =>
    [
        new("ANDROID-299-INSTALL", "Install", false, "Physical Android device required."),
        new("ANDROID-299-PERMISSIONS", "Permissions", false, "Runtime permission pass required."),
        new("ANDROID-299-LIFECYCLE", "Lifecycle", false, "Lifecycle smoke test required."),
        new("ANDROID-299-LINKS", "Links", false, "USB/Bluetooth link validation required."),
        new("ANDROID-299-MAP-PAYLOAD", "Map and payload", false, "Map and payload workflow validation required."),
        new("ANDROID-299-LOGS", "Logs", false, "Device log artifact required.")
    ];
}

public sealed class PlatformScopeCatalog
{
    public IReadOnlyList<PlatformScopeDecision> BuildV154Decisions(bool hasBrowserProject, bool hasIosProject) =>
    [
        new(ReleasePlatformTarget.Desktop, PlatformScopeDecisionStatus.InScope, "Desktop project exists."),
        new(ReleasePlatformTarget.Android, PlatformScopeDecisionStatus.InScope, "Android release remains planned."),
        new(ReleasePlatformTarget.Browser, hasBrowserProject ? PlatformScopeDecisionStatus.InScope : PlatformScopeDecisionStatus.OutOfScope, "No browser project directory."),
        new(ReleasePlatformTarget.Ios, hasIosProject ? PlatformScopeDecisionStatus.InScope : PlatformScopeDecisionStatus.OutOfScope, "No iOS project directory.")
    ];
}

public sealed class ReleaseClosureAudit
{
    public ReleaseClosureSummary Audit(IReadOnlyList<ReleaseReadinessItem> items, LicenseSourceAuditResult license, IReadOnlyList<AndroidReleaseDeviceCheck> android, IReadOnlyList<PlatformScopeDecision> scope)
    {
        var blockers = items.Where(static item => item.Status == ReleaseItemStatus.Blocked).Select(static item => item.Blocker)
            .Concat(android.Where(static check => !check.Complete).Select(static check => check.Blocker))
            .Concat(scope.Where(static decision => decision.Status == PlatformScopeDecisionStatus.OutOfScope).Select(static decision => decision.Reason))
            .ToList();
        if (!license.CanBuildReleaseInventory) blockers.Add("License inventory has redistributable blockers.");
        var blocked = items.Count(static item => item.Status == ReleaseItemStatus.Blocked);
        return new ReleaseClosureSummary(items.Count, items.Count(static item => item.Status == ReleaseItemStatus.Complete), blocked, blockers.Count == 0, blockers, $"{blocked} release readiness items blocked; release-candidate claim blocked.");
    }
}

public sealed class ReleaseEvidenceCatalog
{
    public IReadOnlyList<ReleaseEvidenceItem> BuildV154Evidence() =>
    [
        new("REL-295", "License audit inventory complete.", true),
        new("REL-296", "Third-party notices cataloged.", true),
        new("REL-297", "Desktop release artifact must be produced.", false),
        new("REL-298", "Android signing evidence must stay external.", false),
        new("REL-299", "Android physical-device evidence required.", false),
        new("REL-300", "Browser and iOS release scope decided.", true),
        new("REL-301", "SITL and hardware evidence required.", false),
        new("REL-302", "release-candidate overclaim boundary recorded.", true)
    ];
}
