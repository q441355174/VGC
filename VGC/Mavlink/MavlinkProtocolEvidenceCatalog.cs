namespace VGC.Mavlink;

public enum MavlinkProtocolEvidenceArea
{
    Parser,
    Writer,
    Signing,
    Ftp,
    Statistics
}

public enum MavlinkProtocolEvidenceLevel
{
    L0CodeEvidence,
    L1UnitLogic,
    L2BuildVerified
}

public sealed record MavlinkProtocolEvidenceItem(
    MavlinkProtocolEvidenceArea Area,
    MavlinkProtocolEvidenceLevel EvidenceLevel,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> TestNames,
    string Notes);

public static class MavlinkProtocolEvidenceCatalog
{
    public static IReadOnlyList<MavlinkProtocolEvidenceArea> RequiredAreas { get; } =
    [
        MavlinkProtocolEvidenceArea.Parser,
        MavlinkProtocolEvidenceArea.Writer,
        MavlinkProtocolEvidenceArea.Signing,
        MavlinkProtocolEvidenceArea.Ftp,
        MavlinkProtocolEvidenceArea.Statistics
    ];

    public static IReadOnlyList<MavlinkProtocolEvidenceItem> Items { get; } =
    [
        new(
            MavlinkProtocolEvidenceArea.Parser,
            MavlinkProtocolEvidenceLevel.L2BuildVerified,
            ["VGC/Mavlink/MavlinkFrameParser.cs"],
            [
                "Parse MAVLink v1 heartbeat",
                "Reject invalid MAVLink v1 heartbeat CRC",
                "Parse MAVLink v2 heartbeat shape",
                "Parse signed MAVLink v2 frame"
            ],
            "Parser evidence covers v1, v2, CRC rejection, split recovery, and signed v2 frame shape."),
        new(
            MavlinkProtocolEvidenceArea.Writer,
            MavlinkProtocolEvidenceLevel.L2BuildVerified,
            ["VGC/Mavlink/MavlinkFrameWriter.cs", "VGC/Mavlink/MavlinkOutboundRouter.cs"],
            [
                "Create GCS heartbeat frame",
                "Create COMMAND_LONG frame",
                "Create and parse mission type frames",
                "Route outbound service families through MAVLink router"
            ],
            "Writer evidence covers core frame creation, v2 mission extensions, and outbound routing."),
        new(
            MavlinkProtocolEvidenceArea.Signing,
            MavlinkProtocolEvidenceLevel.L2BuildVerified,
            ["VGC/Mavlink/SigningController.cs"],
            [
                "Sign and validate MAVLink v2 frame",
                "Reject tampered MAVLink v2 signature",
                "Parse signed MAVLink v2 frame"
            ],
            "Signing evidence covers sign, verify, disabled signing, tampered payload/signature, wrong key, and parser compatibility."),
        new(
            MavlinkProtocolEvidenceArea.Ftp,
            MavlinkProtocolEvidenceLevel.L2BuildVerified,
            ["VGC/Mavlink/MavlinkFtp.cs"],
            [
                "Round-trip MAVLink FTP payload",
                "List MAVLink FTP directory",
                "Download MAVLink FTP file chunks",
                "Handle MAVLink FTP NAK and retry"
            ],
            "FTP evidence covers payload shape, list, open/read/download, NAK, and retry exhaustion."),
        new(
            MavlinkProtocolEvidenceArea.Statistics,
            MavlinkProtocolEvidenceLevel.L2BuildVerified,
            ["VGC/Mavlink/MavlinkStatisticsTracker.cs", "VGC/Mavlink/MavlinkProtocol.cs"],
            [
                "Track MAVLink message statistics per type",
                "Expose MAVLink parser frame sequence",
                "Track MAVLink sequence loss per link",
                "Record MAVLink protocol per-link statistics"
            ],
            "Statistics evidence covers message counts, parser sequence exposure, packet loss, and protocol per-link aggregation.")
    ];

    public static IReadOnlyList<MavlinkProtocolEvidenceArea> MissingRequiredAreas()
    {
        return RequiredAreas
            .Where(area => Items.All(item => item.Area != area))
            .ToArray();
    }
}
