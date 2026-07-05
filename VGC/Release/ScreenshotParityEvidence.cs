namespace VGC.Release;

public enum ScreenshotParityStatus
{
    Pending,
    Captured,
    Blocked
}

public sealed record ScreenshotParityTarget(
    string Id,
    string Area,
    string QgcReference,
    string VgcTarget,
    string RequiredState,
    ScreenshotParityStatus Status,
    string ArtifactPath,
    string Notes);

public sealed class ScreenshotParityEvidenceCatalog
{
    public IReadOnlyList<ScreenshotParityTarget> BuildFlyPlanTargets() =>
    [
        new("SHOT-FLY-TOOLBAR", "Fly", "QGC FlyView toolbar", "VGC/Views/FlyView.axaml", "active vehicle with toolbar indicators", ScreenshotParityStatus.Pending, ".test-output/screenshots/fly-toolbar.png", "Capture requires interactive desktop rendering."),
        new("SHOT-FLY-MAP", "Fly", "QGC FlightMap overlays", "VGC.UI/Controls/MapControls.cs", "vehicle/mission/fence/rally overlays", ScreenshotParityStatus.Pending, ".test-output/screenshots/fly-map.png", "Capture requires same SITL state."),
        new("SHOT-PLAN-EDITOR", "Plan", "QGC Plan editor", "VGC/Views/PlanView.axaml", "mission/fence/rally editor", ScreenshotParityStatus.Pending, ".test-output/screenshots/plan-editor.png", "Capture requires same plan fixture."),
        new("SHOT-PLAN-MAP", "Plan", "QGC Plan map tools", "VGC/Views/PlanView.axaml", "waypoint/fence/rally map edit", ScreenshotParityStatus.Pending, ".test-output/screenshots/plan-map.png", "Capture requires interactive desktop rendering.")
    ];
}

public sealed record ScreenshotParityManifest(
    int TargetCount,
    int CapturedCount,
    int PendingCount,
    IReadOnlyList<string> MissingArtifacts,
    IReadOnlyList<string> CaptureInstructions);

public sealed class ScreenshotParityManifestBuilder
{
    public ScreenshotParityManifest Build(IReadOnlyList<ScreenshotParityTarget> targets)
    {
        var missing = targets
            .Where(static target => target.Status != ScreenshotParityStatus.Captured)
            .Select(static target => $"{target.Id}: {target.ArtifactPath}")
            .ToArray();

        var instructions = targets.Select(static target => $"Capture {target.Id} using state '{target.RequiredState}' and save to {target.ArtifactPath}.").ToArray();

        return new ScreenshotParityManifest(
            targets.Count,
            targets.Count(static target => target.Status == ScreenshotParityStatus.Captured),
            targets.Count(static target => target.Status != ScreenshotParityStatus.Captured),
            missing,
            instructions);
    }
}

public sealed class ScreenshotParityEvidenceExporter
{
    public string ExportFlyPlanManifestMarkdown()
    {
        var targets = new ScreenshotParityEvidenceCatalog().BuildFlyPlanTargets();
        var manifest = new ScreenshotParityManifestBuilder().Build(targets);
        var lines = new List<string>
        {
            "# Screenshot Parity Export",
            $"Targets: {manifest.TargetCount}",
            $"Captured: {manifest.CapturedCount}",
            $"Pending: {manifest.PendingCount}",
            string.Empty,
            "## Missing artifacts"
        };

        lines.AddRange(manifest.MissingArtifacts.Select(static item => $"- {item}"));
        lines.Add(string.Empty);
        lines.Add("## Capture instructions");
        lines.AddRange(manifest.CaptureInstructions.Select(static item => $"- {item}"));
        return string.Join(Environment.NewLine, lines);
    }

    public string WriteFlyPlanManifest(string outputPath)
    {
        var markdown = ExportFlyPlanManifestMarkdown();
        File.WriteAllText(outputPath, markdown);
        return outputPath;
    }
}
