namespace VGC.ViewModels;

public enum QgcParitySubEvidenceStatus
{
    Complete,
    Pending,
    Blocked,
    Skipped
}

public sealed record QgcParitySubEvidenceItem(
    string Module,
    string EvidenceId,
    string Category,
    QgcParitySubEvidenceStatus Status,
    string Artifact,
    string Notes);

public sealed class QgcQmlParitySubEvidenceCatalog
{
    public IReadOnlyList<QgcParitySubEvidenceItem> BuildFlyPlanFlightMap() =>
    [
        new("FlyView", "FLY-SHOT", "screenshot", QgcParitySubEvidenceStatus.Pending, ".test-output/screenshots/fly-toolbar.png", "Same-state screenshot missing."),
        new("FlyView", "FLY-ANDROID", "android", QgcParitySubEvidenceStatus.Complete, ".test-output/android-lifecycle-logcat.txt", "Emulator lifecycle evidence present."),
        new("FlyView", "FLY-SITL", "sitl", QgcParitySubEvidenceStatus.Pending, ".test-output/sitl-deep-transcript.txt", "Passive SITL only; no active command transcript."),
        new("FlyView", "FLY-CMD", "command", QgcParitySubEvidenceStatus.Blocked, ".test-output/sitl-command-scope.txt", "Requires explicit authorization."),
        new("PlanView", "PLAN-SHOT", "screenshot", QgcParitySubEvidenceStatus.Pending, ".test-output/screenshots/plan-editor.png", "Same-state screenshot missing."),
        new("PlanView", "PLAN-ANDROID", "android", QgcParitySubEvidenceStatus.Complete, ".test-output/android-lifecycle-logcat.txt", "Emulator lifecycle evidence present."),
        new("PlanView", "PLAN-SITL", "sitl", QgcParitySubEvidenceStatus.Pending, ".test-output/sitl-deep-transcript.txt", "Mission transfer transcript missing."),
        new("PlanView", "PLAN-CMD", "command", QgcParitySubEvidenceStatus.Blocked, ".test-output/sitl-command-scope.txt", "Requires explicit authorization."),
        new("FlightMap", "MAP-SHOT", "screenshot", QgcParitySubEvidenceStatus.Pending, ".test-output/screenshots/fly-map.png", "Same-state overlay screenshot missing."),
        new("FlightMap", "MAP-ANDROID", "android", QgcParitySubEvidenceStatus.Pending, ".test-output/android-lifecycle-logcat.txt", "Map-specific Android lifecycle evidence not isolated."),
        new("FlightMap", "MAP-SITL", "sitl", QgcParitySubEvidenceStatus.Pending, ".test-output/sitl-deep-transcript.txt", "Overlay transcript missing."),
        new("FlightMap", "MAP-CMD", "command", QgcParitySubEvidenceStatus.Skipped, ".test-output/sitl-command-scope.txt", "Map overlay does not require direct command mutation in this pass.")
    ];

    public string BuildBlockerText(string module)
    {
        var items = BuildFlyPlanFlightMap()
            .Where(item => string.Equals(item.Module, module, StringComparison.Ordinal))
            .ToArray();
        if (items.Length == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", items
            .Where(static item => item.Status != QgcParitySubEvidenceStatus.Complete)
            .Select(static item => $"{item.Category}: {item.Notes}"));
    }
}

public sealed class QgcQmlParitySubEvidenceAudit
{
    public IReadOnlyList<string> OpenBlockers(IReadOnlyList<QgcParitySubEvidenceItem> items) =>
        items.Where(static item => item.Status is QgcParitySubEvidenceStatus.Pending or QgcParitySubEvidenceStatus.Blocked)
            .Select(static item => $"{item.Module}/{item.Category}: {item.Notes}")
            .ToArray();
}

public sealed class QgcQmlParitySubEvidenceExport
{
    public string ExportMarkdown(IReadOnlyList<QgcParitySubEvidenceItem> items)
    {
        var lines = new List<string>
        {
            "# QML Parity Sub Evidence",
            string.Empty
        };

        lines.AddRange(items.Select(static item => $"- {item.Module} | {item.Category} | {item.Status} | {item.Artifact}"));
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class QgcQmlParitySubEvidenceFileWriter
{
    public string WriteMarkdown(string outputPath)
    {
        var items = new QgcQmlParitySubEvidenceCatalog().BuildFlyPlanFlightMap();
        var markdown = new QgcQmlParitySubEvidenceExport().ExportMarkdown(items);
        File.WriteAllText(outputPath, markdown);
        return outputPath;
    }
}
