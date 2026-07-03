namespace VGC.Mavlink;

public enum MavlinkSchemaStrategy
{
    ManualPayloadOnly,
    RuntimeLibraryWrapper,
    LocalXmlBuildTimeGenerator
}

public sealed record MavlinkGeneratorDecision(
    string StrategyId,
    MavlinkSchemaStrategy Strategy,
    string SchemaInputPath,
    string GeneratorProjectPath,
    string GeneratedNamespace,
    bool UsesRuntimeMavlinkPackage,
    bool GeneratesStrongMessageTypes,
    bool GeneratesCrcRegistry,
    bool PreservesManualWireBoundary,
    IReadOnlyList<uint> Phase186SeedMessageIds,
    IReadOnlyList<string> Rationale)
{
    public static MavlinkGeneratorDecision Current { get; } = new(
        StrategyId: "local-xml-build-time-generator",
        Strategy: MavlinkSchemaStrategy.LocalXmlBuildTimeGenerator,
        SchemaInputPath: "VGC/Mavlink/Definitions",
        GeneratorProjectPath: "tools/VGC.MavlinkGenerator",
        GeneratedNamespace: "VGC.Mavlink.Generated",
        UsesRuntimeMavlinkPackage: false,
        GeneratesStrongMessageTypes: true,
        GeneratesCrcRegistry: true,
        PreservesManualWireBoundary: true,
        Phase186SeedMessageIds:
        [
            0,   // HEARTBEAT
            1,   // SYS_STATUS
            20,  // PARAM_REQUEST_READ
            21,  // PARAM_REQUEST_LIST
            22,  // PARAM_VALUE
            23,  // PARAM_SET
            30,  // ATTITUDE
            33,  // GLOBAL_POSITION_INT
            43,  // MISSION_REQUEST_LIST
            44,  // MISSION_COUNT
            47,  // MISSION_ACK
            51,  // MISSION_REQUEST_INT
            73,  // MISSION_ITEM_INT
            76,  // COMMAND_LONG
            77,  // COMMAND_ACK
            253  // STATUSTEXT
        ],
        Rationale:
        [
            "The current parser and writer already own MAVLink wire framing and link integration.",
            "A runtime MAVLink package would make protocol ownership and QGC parity auditing harder.",
            "Build-time XML generation gives Phase 186 strong messages and Phase 187 generated CRC data without changing runtime transport boundaries."
        ]);
}
