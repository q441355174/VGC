namespace VGC.Mavlink;

public enum MavlinkDialectGenerationStatus
{
    Complete,
    Partial,
    Blocked
}

public sealed record MavlinkDialectGenerationItem(
    string Id,
    string Area,
    MavlinkDialectGenerationStatus Status,
    string Owner,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> MissingInputs,
    string Notes);

public sealed class MavlinkDialectGenerationCatalog
{
    public IReadOnlyList<MavlinkDialectGenerationItem> BuildPhase324()
    {
        return
        [
            new("MAV324-XML-LOADER", "MAVLink XML loader", MavlinkDialectGenerationStatus.Partial, "VGC.Mavlink.Generated seed definitions", ["seed message metadata"], ["full common.xml", "full ardupilotmega.xml"], "Current seed metadata is enough for tests but not dialect-wide generation."),
            new("MAV324-STRONG-RECORDS", "Generated strong message records", MavlinkDialectGenerationStatus.Partial, "VGC.Mavlink.MavlinkStrongMessages", ["seed strong records"], ["all common/ardupilotmega records"], "Strong record shape exists; full generator output is blocked by full XML ingestion."),
            new("MAV324-CRC", "Generated CRC extra registry", MavlinkDialectGenerationStatus.Partial, "VGC.Mavlink.MavlinkCrcExtraRegistry", ["seed CRC entries", "legacy fallback entries"], ["generated all-message CRC table"], "Registry has mixed generated/legacy coverage."),
            new("MAV324-ENUMS", "Dialect enum metadata", MavlinkDialectGenerationStatus.Blocked, "Unassigned generator", [], ["enum parser", "value metadata", "bitmask metadata"], "Required before generated UI metadata can match QGC breadth."),
            new("MAV324-CI-DRIFT", "Dialect drift check", MavlinkDialectGenerationStatus.Blocked, "Unassigned generator", [], ["source XML hash", "generated file manifest", "drift test"], "Needed to keep generated sources reproducible.")
        ];
    }
}

public sealed record MavlinkDialectGenerationSummary(
    int TotalItems,
    int CompleteItems,
    int PartialItems,
    int BlockedItems,
    bool CanClaimDialectWideGeneration,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class MavlinkDialectGenerationAudit
{
    public MavlinkDialectGenerationSummary Audit(IReadOnlyList<MavlinkDialectGenerationItem> items)
    {
        var blockers = items
            .Where(static item => item.Status != MavlinkDialectGenerationStatus.Complete)
            .Select(static item => $"{item.Id}: {item.Area} missing {string.Join(", ", item.MissingInputs)}.")
            .ToArray();
        var complete = items.Count(static item => item.Status == MavlinkDialectGenerationStatus.Complete);
        var partial = items.Count(static item => item.Status == MavlinkDialectGenerationStatus.Partial);
        var blocked = items.Count(static item => item.Status == MavlinkDialectGenerationStatus.Blocked);

        return new MavlinkDialectGenerationSummary(
            items.Count,
            complete,
            partial,
            blocked,
            blockers.Length == 0,
            blockers,
            $"{complete}/{items.Count} MAVLink dialect generation items complete; {partial} partial, {blocked} blocked.");
    }
}
