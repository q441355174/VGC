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
            new("UI327-FLY", UiWorkflowArea.Fly, "FlyView active vehicle with toolbar/map/payload indicators", "guided action and warning availability transcript", true, true),
            new("UI327-PLAN", UiWorkflowArea.Plan, "PlanView mission/geofence/rally editing surface", "import/export/validate/upload workflow transcript", true, true),
            new("UI327-ANALYZE", UiWorkflowArea.Analyze, "AnalyzeView inspector/replay/log tools", "replay and log workflow transcript", true, true),
            new("UI327-SETUP", UiWorkflowArea.Setup, "SetupView firmware components/calibration", "setup component readiness transcript", true, true),
            new("UI327-PARAMETERS", UiWorkflowArea.Parameters, "ParameterView search/edit/write status", "parameter edit retry transcript", true, true),
            new("UI327-SETTINGS", UiWorkflowArea.Settings, "SettingsView grouped settings", "settings edit/save transcript", true, true)
        ];
    }
}

public sealed record DesktopUiRuntimeEvidenceSummary(
    int RequiredItems,
    int SharedModelCompleteItems,
    int RuntimeEvidenceCompleteItems,
    IReadOnlyList<string> MissingRuntimeEvidence);

public enum Gate11RuntimeEvidenceStatus
{
    Complete,
    Blocked,
    Deferred
}

public sealed record Gate11RuntimeEvidenceItem(
    string Id,
    string Evidence,
    Gate11RuntimeEvidenceStatus Status,
    bool Complete,
    string Notes);

public sealed class Gate11RuntimeEvidenceCatalog
{
    public IReadOnlyList<Gate11RuntimeEvidenceItem> Build() =>
    [
        new("GATE11-DESKTOP-BUILD", "dotnet build VGC.Desktop", Gate11RuntimeEvidenceStatus.Complete, true, "Desktop project build passes locally."),
        new("GATE11-DESKTOP-RUNTIME", "Desktop runtime launch transcript", Gate11RuntimeEvidenceStatus.Complete, true, "Desktop runtime launch command executed locally."),
        new("GATE11-DESKTOP-SCREENSHOT", "QGC/VGC same-state screenshot parity", Gate11RuntimeEvidenceStatus.Deferred, false, "Screenshot parity evidence not captured."),
        new("GATE11-ANDROID-WORKLOAD", "dotnet build VGC.Android", Gate11RuntimeEvidenceStatus.Complete, true, "Android workload build passes on the SSH Linux environment after copying the repo to a native Linux filesystem."),
        new("GATE11-ANDROID-DEVICE", "Android emulator smoke", Gate11RuntimeEvidenceStatus.Complete, true, "APK installs and launches on emulator-5554; launch logcat captured under .test-output."),
        new("GATE11-SITL", "SITL TCP transcript", Gate11RuntimeEvidenceStatus.Complete, true, "SITL TCP endpoint 100.83.181.91:6276 connects and streams MAVLink v2 heartbeat/telemetry; transcript captured under .test-output.")
    ];
}

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
