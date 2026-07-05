namespace VGC.Mavlink;

public enum MavlinkDialectIngestionStatus
{
    SeedFixturePresent,
    FullUpstreamMissing,
    Generated,
    Adopted
}

public sealed record MavlinkDialectIngestionItem(
    string Dialect,
    string SeedFixture,
    string FullUpstreamFile,
    string FullUpstreamPath,
    bool FullUpstreamExists,
    MavlinkDialectIngestionStatus Status,
    IReadOnlyList<string> Blockers);

public sealed record MavlinkDialectIngestionProjection(
    string Dialect,
    MavlinkDialectIngestionStatus Status,
    string Summary);

public sealed class MavlinkDialectIngestionPlan
{
    public IReadOnlyList<MavlinkDialectIngestionItem> Build() =>
    [
        new(
            "common",
            "common.seed.xml",
            "common.xml",
            "VGC/Mavlink/Definitions/common.xml",
            false,
            MavlinkDialectIngestionStatus.FullUpstreamMissing,
            ["Normalize full upstream common.xml", "Generate complete strong message set", "Replace legacy CRC fallbacks"]),
        new(
            "ardupilotmega",
            "ardupilotmega.seed.xml",
            "ardupilotmega.xml",
            "VGC/Mavlink/Definitions/ardupilotmega.xml",
            false,
            MavlinkDialectIngestionStatus.FullUpstreamMissing,
            ["Normalize full upstream ardupilotmega.xml", "Merge include graph with common.xml", "Adopt generated ArduPilotMega messages"])
    ];

    public IReadOnlyList<MavlinkDialectIngestionProjection> Project(IReadOnlyList<MavlinkDialectIngestionItem> items) =>
        items.Select(static item => new MavlinkDialectIngestionProjection(
            item.Dialect,
            item.Status,
            item.FullUpstreamExists
                ? $"{item.Dialect} full upstream file present at {item.FullUpstreamPath}."
                : $"{item.Dialect} full upstream file missing at {item.FullUpstreamPath}."))
        .ToArray();
}

public sealed class MavlinkFullUpstreamStubLoader
{
    public string DescribeMissingFile(string dialect, string fullUpstreamPath) =>
        $"Missing upstream dialect input for {dialect}: {fullUpstreamPath}";
}

public sealed class MavlinkDialectIngestionAudit
{
    public bool CanClaimFullDialectCoverage(IReadOnlyList<MavlinkDialectIngestionItem> items) =>
        items.Count > 0 && items.All(static item => item.Status == MavlinkDialectIngestionStatus.Adopted);

    public IReadOnlyList<string> OpenBlockers(IReadOnlyList<MavlinkDialectIngestionItem> items) =>
        items.Where(static item => item.Status != MavlinkDialectIngestionStatus.Adopted)
            .SelectMany(static item => item.Blockers.Select(blocker => $"{item.Dialect}: {blocker}"))
            .ToArray();
}

public sealed class MavlinkDialectIngestionConsistency
{
    public bool AllFullUpstreamFilesMissing(IReadOnlyList<MavlinkDialectIngestionItem> items) =>
        items.All(static item => !item.FullUpstreamExists && item.Status == MavlinkDialectIngestionStatus.FullUpstreamMissing);
}
