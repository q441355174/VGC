namespace VGC.ViewModels;

public enum UiWorkflowArea
{
    Shell,
    Fly,
    Plan,
    Analyze,
    Setup,
    Parameters,
    Settings,
    Platform
}

public enum UiPlatformTarget
{
    Shared,
    Desktop,
    Android
}

public sealed record UiWorkflowCoverageItem(
    string Id,
    UiWorkflowArea Area,
    UiPlatformTarget Platform,
    string Capability,
    string EvidenceLevel,
    bool Complete,
    string ResidualGap);

public sealed record UiUsabilityCheck(
    string Id,
    UiPlatformTarget Platform,
    string Check,
    bool Complete,
    string Notes);

public sealed record UiParityAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class UiParityEvidenceCatalog
{
    public IReadOnlyList<UiWorkflowCoverageItem> Build()
    {
        return
        [
            new("UIPARITY-279", UiWorkflowArea.Shell, UiPlatformTarget.Shared, "Shell navigation exposes Fly, Plan, Parameters, Setup, Settings, and Analyze workspaces.", "L2/L5", true, string.Empty),
            new("UIPARITY-280", UiWorkflowArea.Fly, UiPlatformTarget.Shared, "Fly workflow projects vehicle status, guided actions, map state, payload state, and warning summary.", "L2/L5", true, string.Empty),
            new("UIPARITY-281", UiWorkflowArea.Plan, UiPlatformTarget.Shared, "Plan workflow projects mission sections, file workflow, validation, transfer state, and preview state.", "L2/L5", true, string.Empty),
            new("UIPARITY-282", UiWorkflowArea.Analyze, UiPlatformTarget.Shared, "Analyze workflow projects inspector, replay, log download, parser, GeoTag, and file-log state.", "L2/L5", true, string.Empty),
            new("UIPARITY-283A", UiWorkflowArea.Setup, UiPlatformTarget.Shared, "Setup workflow projects firmware, components, calibration, radio, power, and safety status.", "L2/L5", true, string.Empty),
            new("UIPARITY-283B", UiWorkflowArea.Parameters, UiPlatformTarget.Shared, "Parameters workflow projects search, metadata, cache state, pending writes, and edit status.", "L2/L5", true, string.Empty),
            new("UIPARITY-283C", UiWorkflowArea.Settings, UiPlatformTarget.Shared, "Settings workflow projects grouped facts and save behavior through a dedicated Settings workspace.", "L2/L5", true, string.Empty),
            new("UIPARITY-284A", UiWorkflowArea.Platform, UiPlatformTarget.Desktop, "Desktop usability check covers navigation, dense panels, text fit, and command availability at model level.", "L1", true, "Runtime screenshot evidence deferred to v1.53/v1.54."),
            new("UIPARITY-284B", UiWorkflowArea.Platform, UiPlatformTarget.Android, "Android usability check covers shared state, touch target intent, storage/permission risks, and lifecycle notes at model level.", "L1", true, "Physical Android device evidence deferred to v1.53/v1.54."),
            new("UIPARITY-285", UiWorkflowArea.Platform, UiPlatformTarget.Shared, "UI parity evidence catalog records complete shared workflow coverage and explicit residual runtime evidence gaps.", "L0/L5", true, string.Empty),
            new("UIPARITY-286", UiWorkflowArea.Platform, UiPlatformTarget.Shared, "Full visual parity with QGC and operator field usability remain final-release evidence.", "L0/L6", false, "QGC pixel parity, screenshots, and field usability validation remain deferred.")
        ];
    }
}

public sealed class UiUsabilityMatrix
{
    public IReadOnlyList<UiUsabilityCheck> Build()
    {
        return
        [
            new("DESKTOP-UI-01", UiPlatformTarget.Desktop, "Primary navigation reaches each core workflow.", true, "Shell commands cover Fly, Plan, Parameters, Setup, Settings, and Analyze."),
            new("DESKTOP-UI-02", UiPlatformTarget.Desktop, "Operational panels avoid decorative cards and expose dense status summaries.", true, "Existing ViewModels expose compact summaries for core workflows."),
            new("DESKTOP-UI-03", UiPlatformTarget.Desktop, "Desktop screenshot/runtime evidence.", false, "Deferred until v1.53/v1.54 runtime evidence phase."),
            new("ANDROID-UI-01", UiPlatformTarget.Android, "Shared ViewModel state can back Android UI without platform-specific MAVLink writing.", true, "ViewModels keep MAVLink frame creation out of UI code."),
            new("ANDROID-UI-02", UiPlatformTarget.Android, "Android storage, permission, lifecycle, and hardware risks are documented in workflow models.", true, "Risks are recorded across map, payload, positioning, and traffic evidence."),
            new("ANDROID-UI-03", UiPlatformTarget.Android, "Physical Android usability evidence.", false, "Deferred until device validation phase.")
        ];
    }
}

public sealed class UiParityAudit
{
    public UiParityAuditResult Audit(IReadOnlyList<UiWorkflowCoverageItem> evidence, IReadOnlyList<UiUsabilityCheck> usability)
    {
        var complete = evidence.Count(static item => item.Complete) + usability.Count(static item => item.Complete);
        var deferred = evidence
            .Where(static item => !item.Complete)
            .Select(static item => item.ResidualGap)
            .Concat(usability.Where(static item => !item.Complete).Select(static item => item.Notes))
            .Where(static gap => !string.IsNullOrWhiteSpace(gap))
            .ToArray();

        return new UiParityAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} UI parity/usability evidence items complete; {deferred.Length} visual/device evidence gaps remain.");
    }
}
