using VGC.Mavlink;

namespace VGC.Mavlink.Generated;

public enum MavlinkMessageCategory
{
    VehicleState,
    Parameter,
    Mission,
    Command,
    Position,
    Status
}

public sealed record MavlinkFieldDefinition(
    string Name,
    string WireType,
    int ArrayLength = 0,
    bool IsExtension = false);

public sealed record MavlinkMessageDefinition(
    uint MessageId,
    string Name,
    byte CrcExtra,
    int MinPayloadLength,
    int MaxPayloadLength,
    string ClrTypeName,
    MavlinkMessageCategory Category,
    IReadOnlyList<MavlinkFieldDefinition> Fields)
{
    public bool HasExtensionFields => Fields.Any(static f => f.IsExtension);
}

public static class MavlinkSeedMessageDefinitions
{
    public static IReadOnlyList<MavlinkMessageDefinition> All { get; } =
    [
        new(MavlinkMessageIds.Heartbeat, "HEARTBEAT", 50, 9, 9, nameof(MavlinkHeartbeat), MavlinkMessageCategory.VehicleState,
        [
            new("custom_mode", "uint32_t"),
            new("type", "uint8_t"),
            new("autopilot", "uint8_t"),
            new("base_mode", "uint8_t"),
            new("system_status", "uint8_t"),
            new("mavlink_version", "uint8_t")
        ]),
        new(MavlinkMessageIds.SysStatus, "SYS_STATUS", 124, 31, 31, nameof(MavlinkSysStatus), MavlinkMessageCategory.VehicleState,
        [
            new("onboard_control_sensors_present", "uint32_t"),
            new("onboard_control_sensors_enabled", "uint32_t"),
            new("onboard_control_sensors_health", "uint32_t"),
            new("load", "uint16_t"),
            new("voltage_battery", "uint16_t"),
            new("current_battery", "int16_t"),
            new("drop_rate_comm", "uint16_t"),
            new("errors_comm", "uint16_t"),
            new("errors_count1", "uint16_t"),
            new("errors_count2", "uint16_t"),
            new("errors_count3", "uint16_t"),
            new("errors_count4", "uint16_t"),
            new("battery_remaining", "int8_t")
        ]),
        new(MavlinkMessageIds.ParamRequestRead, "PARAM_REQUEST_READ", 214, 20, 20, nameof(MavlinkParameterRequestRead), MavlinkMessageCategory.Parameter,
        [
            new("param_index", "int16_t"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("param_id", "char", 16)
        ]),
        new(MavlinkMessageIds.ParamRequestList, "PARAM_REQUEST_LIST", 159, 2, 2, nameof(MavlinkParameterRequestList), MavlinkMessageCategory.Parameter,
        [
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t")
        ]),
        new(MavlinkMessageIds.ParamValue, "PARAM_VALUE", 220, 25, 25, nameof(MavlinkParameterValue), MavlinkMessageCategory.Parameter,
        [
            new("param_value", "float"),
            new("param_count", "uint16_t"),
            new("param_index", "uint16_t"),
            new("param_id", "char", 16),
            new("param_type", "uint8_t")
        ]),
        new(MavlinkMessageIds.ParamSet, "PARAM_SET", 168, 23, 23, nameof(MavlinkParameterSet), MavlinkMessageCategory.Parameter,
        [
            new("param_value", "float"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("param_id", "char", 16),
            new("param_type", "uint8_t")
        ]),
        new(MavlinkMessageIds.GlobalPositionInt, "GLOBAL_POSITION_INT", 104, 28, 28, nameof(MavlinkGlobalPositionInt), MavlinkMessageCategory.Position,
        [
            new("time_boot_ms", "uint32_t"),
            new("lat", "int32_t"),
            new("lon", "int32_t"),
            new("alt", "int32_t"),
            new("relative_alt", "int32_t"),
            new("vx", "int16_t"),
            new("vy", "int16_t"),
            new("vz", "int16_t"),
            new("hdg", "uint16_t")
        ]),
        new(MavlinkMessageIds.Attitude, "ATTITUDE", 39, 28, 28, nameof(MavlinkAttitude), MavlinkMessageCategory.Position,
        [
            new("time_boot_ms", "uint32_t"),
            new("roll", "float"),
            new("pitch", "float"),
            new("yaw", "float"),
            new("rollspeed", "float"),
            new("pitchspeed", "float"),
            new("yawspeed", "float")
        ]),
        new(MavlinkMessageIds.MissionRequestList, "MISSION_REQUEST_LIST", 132, 2, 3, nameof(MavlinkMissionRequestList), MavlinkMessageCategory.Mission,
        [
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("mission_type", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.MissionCount, "MISSION_COUNT", 221, 4, 5, nameof(MavlinkMissionCount), MavlinkMessageCategory.Mission,
        [
            new("count", "uint16_t"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("mission_type", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.MissionAck, "MISSION_ACK", 153, 3, 4, nameof(MavlinkMissionAck), MavlinkMessageCategory.Mission,
        [
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("type", "uint8_t"),
            new("mission_type", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.MissionRequestInt, "MISSION_REQUEST_INT", 196, 4, 5, nameof(MavlinkMissionRequestInt), MavlinkMessageCategory.Mission,
        [
            new("seq", "uint16_t"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("mission_type", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.MissionItemInt, "MISSION_ITEM_INT", 38, 37, 38, nameof(MavlinkMissionItemInt), MavlinkMessageCategory.Mission,
        [
            new("param1", "float"),
            new("param2", "float"),
            new("param3", "float"),
            new("param4", "float"),
            new("x", "int32_t"),
            new("y", "int32_t"),
            new("z", "float"),
            new("seq", "uint16_t"),
            new("command", "uint16_t"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("frame", "uint8_t"),
            new("current", "uint8_t"),
            new("autocontinue", "uint8_t"),
            new("mission_type", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.CommandLong, "COMMAND_LONG", 152, 33, 33, nameof(MavlinkCommandLong), MavlinkMessageCategory.Command,
        [
            new("param1", "float"),
            new("param2", "float"),
            new("param3", "float"),
            new("param4", "float"),
            new("param5", "float"),
            new("param6", "float"),
            new("param7", "float"),
            new("command", "uint16_t"),
            new("target_system", "uint8_t"),
            new("target_component", "uint8_t"),
            new("confirmation", "uint8_t")
        ]),
        new(MavlinkMessageIds.CommandAck, "COMMAND_ACK", 143, 3, 10, nameof(MavlinkCommandAck), MavlinkMessageCategory.Command,
        [
            new("command", "uint16_t"),
            new("result", "uint8_t"),
            new("progress", "uint8_t", IsExtension: true),
            new("result_param2", "int32_t", IsExtension: true),
            new("target_system", "uint8_t", IsExtension: true),
            new("target_component", "uint8_t", IsExtension: true)
        ]),
        new(MavlinkMessageIds.Statustext, "STATUSTEXT", 83, 51, 54, nameof(MavlinkStatusText), MavlinkMessageCategory.Status,
        [
            new("severity", "uint8_t"),
            new("text", "char", 50),
            new("id", "uint16_t", IsExtension: true),
            new("chunk_seq", "uint8_t", IsExtension: true)
        ])
    ];

    private static readonly IReadOnlyDictionary<uint, MavlinkMessageDefinition> ById =
        All.ToDictionary(static d => d.MessageId);

    private static readonly IReadOnlyDictionary<string, MavlinkMessageDefinition> ByName =
        All.ToDictionary(static d => d.Name, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(uint messageId, out MavlinkMessageDefinition definition)
    {
        return ById.TryGetValue(messageId, out definition!);
    }

    public static bool TryGet(string name, out MavlinkMessageDefinition definition)
    {
        return ByName.TryGetValue(name, out definition!);
    }
}
