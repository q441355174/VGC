namespace VGC.Analyze;

public enum AnalyzeUtilityCompletionStatus
{
    Complete,
    SharedModelOnly,
    Blocked
}

public sealed record AnalyzeUtilityCompletionItem(
    string Id,
    string Area,
    AnalyzeUtilityCompletionStatus Status,
    string Owner,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> MissingRuntimeEvidence);

public sealed class AnalyzeLogsUtilitiesCompletionCatalog
{
    public IReadOnlyList<AnalyzeUtilityCompletionItem> BuildPhase331()
    {
        return
        [
            new("AN331-CONSOLE", "MAVLink console", AnalyzeUtilityCompletionStatus.Complete, "MavlinkConsoleRuntime", ["command/response/error lines", "pending command state"], []),
            new("AN331-LOG-DOWNLOAD", "Onboard log list/download", AnalyzeUtilityCompletionStatus.SharedModelOnly, "FlightLogDownloadWorkflow", ["list/download/cancel/retry state"], ["PX4 onboard log transcript", "ArduPilot onboard log transcript"]),
            new("AN331-PARSERS", "ULog/DataFlash parsers", AnalyzeUtilityCompletionStatus.SharedModelOnly, "FlightLogParserRuntime", ["format recognition", "diagnostics projection"], ["real PX4 ULog sample", "real ArduPilot DataFlash sample"]),
            new("AN331-GEOTAG", "GeoTag workflow", AnalyzeUtilityCompletionStatus.SharedModelOnly, "GeoTagWorkflow/GeoTagRuntimeProjector", ["image/log matching", "offset tolerance"], ["real image/log bundle"]),
            new("AN331-FILE-LOG", "Application file log viewer/export", AnalyzeUtilityCompletionStatus.SharedModelOnly, "FileLogViewerProjector", ["filter", "export text"], ["rotated file log fixture"]),
            new("AN331-UTILITIES", "Operator-critical utilities", AnalyzeUtilityCompletionStatus.Blocked, "Unassigned", [], ["QGC utilities triage", "selected compression/file/network utility replacements"])
        ];
    }
}

public sealed class AnalyzeLogsUtilitiesCompletionAudit
{
    public IReadOnlyList<string> MissingEvidence(IReadOnlyList<AnalyzeUtilityCompletionItem> items)
    {
        return items
            .Where(static item => item.Status != AnalyzeUtilityCompletionStatus.Complete)
            .SelectMany(static item => item.MissingRuntimeEvidence.Select(evidence => $"{item.Id}: {evidence}"))
            .ToArray();
    }
}
