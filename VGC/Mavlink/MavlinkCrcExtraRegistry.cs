using VGC.Mavlink.Generated;

namespace VGC.Mavlink;

public enum MavlinkCrcExtraSource
{
    GeneratedSeedDefinition,
    GeneratedDialectXml,
    LegacyManualTable
}

public sealed record MavlinkCrcExtraEntry(
    uint MessageId,
    byte CrcExtra,
    MavlinkCrcExtraSource Source,
    string? MessageName = null);

public sealed record MavlinkCrcRegistryAudit(
    int GeneratedSeedCount,
    int LegacyManualCount,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class MavlinkCrcExtraRegistry
{
    private static readonly IReadOnlyDictionary<uint, MavlinkCrcExtraEntry> GeneratedSeedEntries =
        MavlinkSeedMessageDefinitions.All.ToDictionary(
            static d => d.MessageId,
            static d => new MavlinkCrcExtraEntry(
                d.MessageId,
                d.CrcExtra,
                MavlinkCrcExtraSource.GeneratedSeedDefinition,
                d.Name));

    private static readonly IReadOnlyDictionary<uint, MavlinkCrcExtraEntry> LegacyManualEntries = new Dictionary<uint, byte>
    {
        [2] = 28,   // SYSTEM_TIME
        [4] = 234,  // PING
        [11] = 89,  // SET_MODE
        [24] = 24,  // GPS_RAW_INT
        [30] = 149, // ATTITUDE
        [35] = 87,  // LOCAL_POSITION_NED
        [45] = 232, // MISSION_CLEAR_ALL
        [74] = 154, // VFR_HUD
        [87] = 206, // POSITION_TARGET_GLOBAL_INT
        [105] = 148,// HIGHRES_IMU
        [110] = 106,// HOME_POSITION
        [111] = 146,// TIMESYNC
        [125] = 204,// POWER_STATUS
        [147] = 197,// BATTERY_STATUS
        [241] = 199,// GIMBAL_DEVICE_ATTITUDE_STATUS
        [253] = 83, // STATUSTEXT
        [256] = 159,// ESTIMATOR_STATUS
        [261] = 186,// MESSAGE_INTERVAL
        [266] = 119,// WIND_COV
        [321] = 137,// COMMAND_INT
        [331] = 160,// ODOMETRY
        [340] = 149,// EXTENDED_SYS_STATE
    }.ToDictionary(
        static kvp => kvp.Key,
        static kvp => new MavlinkCrcExtraEntry(kvp.Key, kvp.Value, MavlinkCrcExtraSource.LegacyManualTable));

    private static readonly IReadOnlyDictionary<uint, MavlinkCrcExtraEntry> Entries = BuildEntries();

    public static bool TryGet(uint messageId, out byte crcExtra)
    {
        if (Entries.TryGetValue(messageId, out var entry))
        {
            crcExtra = entry.CrcExtra;
            return true;
        }

        crcExtra = default;
        return false;
    }

    public static bool TryGetEntry(uint messageId, out MavlinkCrcExtraEntry entry)
    {
        return Entries.TryGetValue(messageId, out entry!);
    }

    public static IReadOnlyList<MavlinkCrcExtraEntry> Snapshot()
    {
        return Entries.Values.OrderBy(static e => e.MessageId).ToArray();
    }

    public static MavlinkCrcRegistryAudit Audit()
    {
        var errors = new List<string>();
        foreach (var seed in MavlinkSeedMessageDefinitions.All)
        {
            if (!GeneratedSeedEntries.TryGetValue(seed.MessageId, out var entry))
            {
                errors.Add($"Missing generated seed CRC for {seed.Name} ({seed.MessageId}).");
                continue;
            }

            if (entry.CrcExtra != seed.CrcExtra)
            {
                errors.Add($"Generated seed CRC mismatch for {seed.Name} ({seed.MessageId}).");
            }
        }

        return new MavlinkCrcRegistryAudit(
            GeneratedSeedEntries.Count,
            LegacyManualEntries.Count(static entry => !GeneratedSeedEntries.ContainsKey(entry.Key)),
            errors);
    }

    public static int RegisteredCount => Entries.Count;

    public static IReadOnlyDictionary<uint, MavlinkCrcExtraEntry> BuildRegistryFromGeneratedDefinitions(
        IEnumerable<MavlinkMessageDefinition> definitions)
    {
        return definitions.ToDictionary(
            static d => d.MessageId,
            static d => new MavlinkCrcExtraEntry(
                d.MessageId,
                d.CrcExtra,
                MavlinkCrcExtraSource.GeneratedDialectXml,
                d.Name));
    }

    private static IReadOnlyDictionary<uint, MavlinkCrcExtraEntry> BuildEntries()
    {
        var entries = GeneratedSeedEntries.Values.ToDictionary(static e => e.MessageId);
        foreach (var legacy in LegacyManualEntries.Values)
        {
            entries.TryAdd(legacy.MessageId, legacy);
        }

        return entries;
    }
}
