namespace VGC.ViewModels;

public sealed record DesktopUiRuntimeEvidenceRequirement(
    string Id,
    UiWorkflowArea Area,
    string RequiredScreenshot,
    string RequiredTranscript,
    bool SharedModelComplete,
    bool RuntimeEvidenceComplete);

public sealed class DesktopUiRuntimeEvidenceCatalog
{
    public IReadOnlyList<DesktopUiRuntimeEvidenceRequirement> BuildPhase327()
    {
        return
        [
            new("UI327-FLY", UiWorkflowArea.Fly, "FlyView active vehicle with toolbar/map/payload indicators", "guided action and warning availability transcript", true, false),
            new("UI327-PLAN", UiWorkflowArea.Plan, "PlanView mission/geofence/rally editing surface", "import/export/validate/upload workflow transcript", true, false),
            new("UI327-ANALYZE", UiWorkflowArea.Analyze, "AnalyzeView inspector/replay/log tools", "replay and log workflow transcript", true, false),
            new("UI327-SETUP", UiWorkflowArea.Setup, "SetupView firmware components/calibration", "setup component readiness transcript", true, false),
            new("UI327-PARAMETERS", UiWorkflowArea.Parameters, "ParameterView search/edit/write status", "parameter edit retry transcript", true, false),
            new("UI327-SETTINGS", UiWorkflowArea.Settings, "SettingsView grouped settings", "settings edit/save transcript", true, false)
        ];
    }
}

public sealed record DesktopUiRuntimeEvidenceSummary(
    int RequiredItems,
    int SharedModelCompleteItems,
    int RuntimeEvidenceCompleteItems,
    IReadOnlyList<string> MissingRuntimeEvidence);

public sealed class DesktopUiRuntimeEvidenceAudit
{
    public DesktopUiRuntimeEvidenceSummary Audit(IReadOnlyList<DesktopUiRuntimeEvidenceRequirement> requirements)
    {
        var missing = requirements
            .Where(static item => !item.RuntimeEvidenceComplete)
            .Select(static item => $"{item.Id}: {item.RequiredScreenshot}; {item.RequiredTranscript}")
            .ToArray();

        return new DesktopUiRuntimeEvidenceSummary(
            requirements.Count,
            requirements.Count(static item => item.SharedModelComplete),
            requirements.Count(static item => item.RuntimeEvidenceComplete),
            missing);
    }
}
