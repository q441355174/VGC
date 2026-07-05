using VGC.Mavlink.Generated;

namespace VGC.Mavlink;

public enum MavlinkFullProtocolCoverageDisposition
{
    Complete,
    Partial,
    Blocked,
    OwnedByLaterPhase
}

public enum MavlinkFullProtocolCoveragePriority
{
    P0,
    P1,
    P2
}

public sealed record MavlinkFullProtocolCoverageItem(
    string Id,
    string Area,
    MavlinkFullProtocolCoveragePriority Priority,
    MavlinkFullProtocolCoverageDisposition Disposition,
    string VgcOwner,
    IReadOnlyList<string> EvidenceTests,
    string Notes);

public sealed class MavlinkFullProtocolCoverageCatalog
{
    public IReadOnlyList<MavlinkFullProtocolCoverageItem> Build()
    {
        return
        [
            new(
                "MAV-312-GENERATOR-DECISION",
                "Local XML build-time generator decision",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Complete,
                "VGC.Mavlink.MavlinkGeneratorDecision",
                ["Select MAVLink generator decision"],
                "The selected strategy preserves VGC parser/writer ownership while targeting generated strong message types and CRC metadata."),
            new(
                "MAV-312-SEED-DEFINITIONS",
                "Generated seed strong message definitions",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Partial,
                "VGC.Mavlink.Generated.MavlinkSeedMessageDefinitions",
                ["Expose MAVLink seed message definitions", "Define MAVLink missing strong message records"],
                "Strong records cover the Phase 186 seed set; dialect-wide common.xml/ardupilotmega.xml generation remains open."),
            new(
                "MAV-312-CRC-REGISTRY",
                "CRC extra registry backed by generated seed metadata",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Partial,
                "VGC.Mavlink.MavlinkCrcExtraRegistry",
                ["Align MAVLink seed definitions with CRC registry", "Audit generated MAVLink CRC registry", "Keep legacy MAVLink CRC entries available"],
                "Seed CRC entries are generated from message definitions while non-seed IDs remain in the legacy table until full dialect generation lands."),
            new(
                "MAV-312-WIRE-BOUNDARY",
                "Parser and writer protocol boundary",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Complete,
                "VGC.Mavlink.MavlinkFrameParser/MavlinkFrameWriter",
                ["Parse MAVLink v1 heartbeat", "Parse MAVLink v2 heartbeat shape", "Create GCS heartbeat frame", "Route outbound service families through MAVLink router"],
                "Existing wire framing remains owned by VGC and is covered for v1, v2, CRC rejection, signing compatibility, and outbound routing."),
            new(
                "MAV-312-PROTOCOL-EVIDENCE",
                "Protocol evidence catalog",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Complete,
                "VGC.Mavlink.MavlinkProtocolEvidenceCatalog",
                ["Expose MAVLink protocol evidence catalog", "Verify MAVLink protocol evidence sources and tests"],
                "Parser, writer, signing, FTP, and statistics evidence are mapped to source files and registered tests."),
            new(
                "MAV-312-DIALECT-WIDE-GENERATOR",
                "Dialect-wide strong message and CRC generator",
                MavlinkFullProtocolCoveragePriority.P0,
                MavlinkFullProtocolCoverageDisposition.Blocked,
                "tools/VGC.MavlinkGenerator",
                [],
                "The generator project path is reserved but not implemented; full common.xml and ardupilotmega.xml generation is still a blocker."),
            new(
                "MAV-312-DIALECT-FIXTURES",
                "Complete MAVLink XML fixture ingestion",
                MavlinkFullProtocolCoveragePriority.P1,
                MavlinkFullProtocolCoverageDisposition.Partial,
                "VGC/Mavlink/Definitions",
                ["Expose MAVLink seed message definitions", "Load MAVLink ArduPilotMega seed fixture"],
                "common.seed.xml and ardupilotmega.seed.xml are present as curated seed fixtures; full upstream dialect fixture normalization and drift checks remain open."),
            new(
                "MAV-312-RUNTIME-ADOPTION",
                "Runtime adoption of generated payload readers/writers",
                MavlinkFullProtocolCoveragePriority.P1,
                MavlinkFullProtocolCoverageDisposition.OwnedByLaterPhase,
                "Future MAVLink generator adoption phase",
                ["Keep ViewModels out of MAVLink payload writing"],
                "Current services still own hand-written payload construction for many messages; generator-backed readers/writers need a later migration.")
        ];
    }
}

public sealed record MavlinkFullProtocolCoverageSummary(
    int TotalAreas,
    int CompleteAreas,
    int PartialAreas,
    int BlockedAreas,
    int OwnedByLaterPhaseAreas,
    int GeneratedSeedMessageCount,
    int RegisteredCrcCount,
    int RequiredEvidenceAreasCovered,
    bool CanClaimDialectWideGeneratedCoverage,
    IReadOnlyList<string> OpenBlockers,
    string Summary);

public sealed class MavlinkFullProtocolCoverageAudit
{
    public MavlinkFullProtocolCoverageSummary Audit(IReadOnlyList<MavlinkFullProtocolCoverageItem> items)
    {
        var blockers = items
            .Where(static item => item.Priority is MavlinkFullProtocolCoveragePriority.P0 or MavlinkFullProtocolCoveragePriority.P1
                && item.Disposition is not MavlinkFullProtocolCoverageDisposition.Complete)
            .Select(static item => $"{item.Id}: {item.Area} remains {item.Disposition} ({item.VgcOwner}).")
            .ToArray();
        var complete = items.Count(static item => item.Disposition == MavlinkFullProtocolCoverageDisposition.Complete);
        var partial = items.Count(static item => item.Disposition == MavlinkFullProtocolCoverageDisposition.Partial);
        var blocked = items.Count(static item => item.Disposition == MavlinkFullProtocolCoverageDisposition.Blocked);
        var ownedByLaterPhase = items.Count(static item => item.Disposition == MavlinkFullProtocolCoverageDisposition.OwnedByLaterPhase);
        var coveredEvidenceAreas = MavlinkProtocolEvidenceCatalog.RequiredAreas.Count - MavlinkProtocolEvidenceCatalog.MissingRequiredAreas().Count;
        var canClaim = blockers.Length == 0
            && items.All(static item => item.Disposition == MavlinkFullProtocolCoverageDisposition.Complete)
            && MavlinkGeneratorDecision.Current.GeneratesStrongMessageTypes
            && MavlinkGeneratorDecision.Current.GeneratesCrcRegistry;

        return new MavlinkFullProtocolCoverageSummary(
            items.Count,
            complete,
            partial,
            blocked,
            ownedByLaterPhase,
            MavlinkSeedMessageDefinitions.All.Count,
            MavlinkCrcExtraRegistry.RegisteredCount,
            coveredEvidenceAreas,
            canClaim,
            blockers,
            $"{complete}/{items.Count} MAVLink full-protocol areas complete; {partial} partial, {blocked} blocked, {ownedByLaterPhase} owned by later phases.");
    }
}
