using VGC.Firmware;
using System.Text.Json;

namespace VGC.Mission;

public sealed record MissionCommandParameterMetadata(
    int Index,
    string Label,
    string? Units = null,
    string? Description = null,
    double? DefaultValue = null);

public sealed record MissionCommandMetadata(
    ushort CommandId,
    string Label,
    string Category,
    IReadOnlyList<int> SupportedFrames,
    IReadOnlyList<MissionCommandParameterMetadata> Parameters,
    bool RequiresGeoFenceSupport = false,
    bool RequiresRallyPointSupport = false,
    string? RawName = null,
    string? Description = null,
    bool FriendlyEdit = false);

public interface IMissionCommandMetadataCatalog
{
    MissionCommandMetadata? Find(ushort commandId);
}

public interface IMissionCommandMetadataSource
{
    Task<IMissionCommandMetadataCatalog> LoadAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryMissionCommandMetadataCatalog : IMissionCommandMetadataCatalog
{
    private readonly Dictionary<ushort, MissionCommandMetadata> _metadata;

    public InMemoryMissionCommandMetadataCatalog(IEnumerable<MissionCommandMetadata> metadata)
    {
        _metadata = metadata.ToDictionary(static item => item.CommandId, static item => item);
    }

    public MissionCommandMetadata? Find(ushort commandId)
    {
        return _metadata.GetValueOrDefault(commandId);
    }

    public IReadOnlyList<MissionCommandMetadata> GetAll()
    {
        return _metadata.Values.ToArray();
    }

    public static InMemoryMissionCommandMetadataCatalog CreateDefault()
    {
        return new InMemoryMissionCommandMetadataCatalog([
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavWaypoint,
                "Waypoint",
                "Basic",
                [3, 6],
                [
                    new MissionCommandParameterMetadata(1, "Hold", "s"),
                    new MissionCommandParameterMetadata(5, "Latitude", "deg"),
                    new MissionCommandParameterMetadata(6, "Longitude", "deg"),
                    new MissionCommandParameterMetadata(7, "Altitude", "m")
                ]),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavFencePolygonVertexInclusion,
                "Fence Polygon Inclusion",
                "GeoFence",
                [3, 6],
                [new MissionCommandParameterMetadata(1, "Vertex count")],
                RequiresGeoFenceSupport: true),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavFencePolygonVertexExclusion,
                "Fence Polygon Exclusion",
                "GeoFence",
                [3, 6],
                [new MissionCommandParameterMetadata(1, "Vertex count")],
                RequiresGeoFenceSupport: true),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavFenceCircleInclusion,
                "Fence Circle Inclusion",
                "GeoFence",
                [3, 6],
                [
                    new MissionCommandParameterMetadata(1, "Radius", "m"),
                    new MissionCommandParameterMetadata(5, "Latitude", "deg"),
                    new MissionCommandParameterMetadata(6, "Longitude", "deg")
                ],
                RequiresGeoFenceSupport: true),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavFenceCircleExclusion,
                "Fence Circle Exclusion",
                "GeoFence",
                [3, 6],
                [
                    new MissionCommandParameterMetadata(1, "Radius", "m"),
                    new MissionCommandParameterMetadata(5, "Latitude", "deg"),
                    new MissionCommandParameterMetadata(6, "Longitude", "deg")
                ],
                RequiresGeoFenceSupport: true),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavFenceReturnPoint,
                "Fence Return Point",
                "GeoFence",
                [3, 6],
                [
                    new MissionCommandParameterMetadata(5, "Latitude", "deg"),
                    new MissionCommandParameterMetadata(6, "Longitude", "deg"),
                    new MissionCommandParameterMetadata(7, "Altitude", "m")
                ],
                RequiresGeoFenceSupport: true),
            new MissionCommandMetadata(
                MavlinkMissionCommandIds.NavRallyPoint,
                "Rally Point",
                "Rally",
                [3, 6],
                [
                    new MissionCommandParameterMetadata(5, "Latitude", "deg"),
                    new MissionCommandParameterMetadata(6, "Longitude", "deg"),
                    new MissionCommandParameterMetadata(7, "Altitude", "m")
                ],
                RequiresRallyPointSupport: true)
        ]);
    }
}

public sealed class JsonMissionCommandMetadataSource : IMissionCommandMetadataSource
{
    private readonly Func<CancellationToken, Task<string>> _jsonLoader;

    public JsonMissionCommandMetadataSource(string json)
        : this(_ => Task.FromResult(json))
    {
    }

    public JsonMissionCommandMetadataSource(Func<CancellationToken, Task<string>> jsonLoader)
    {
        _jsonLoader = jsonLoader;
    }

    public async Task<IMissionCommandMetadataCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        var json = await _jsonLoader(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("fileType", out var fileType)
            || !string.Equals(fileType.GetString(), "MavCmdInfo", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Mission command metadata JSON must have fileType 'MavCmdInfo'.");
        }

        if (!root.TryGetProperty("mavCmdInfo", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Mission command metadata JSON must contain a mavCmdInfo array.");
        }

        var metadata = new List<MissionCommandMetadata>();
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            metadata.Add(ParseCommand(item));
        }

        return new InMemoryMissionCommandMetadataCatalog(metadata);
    }

    public static JsonMissionCommandMetadataSource FromFile(string path)
    {
        return new JsonMissionCommandMetadataSource(cancellationToken => File.ReadAllTextAsync(path, cancellationToken));
    }

    private static MissionCommandMetadata ParseCommand(JsonElement item)
    {
        var id = checked((ushort)GetRequiredInt(item, "id"));
        var rawName = GetString(item, "rawName");
        var label = GetString(item, "friendlyName")
            ?? rawName
            ?? GetString(item, "comment")
            ?? $"Command {id}";
        var category = GetString(item, "category") ?? "Advanced";
        var removedParams = ParseParamRemove(GetString(item, "paramRemove"));

        return new MissionCommandMetadata(
            id,
            label,
            category,
            [3, 6],
            ParseParameters(item, removedParams),
            RequiresGeoFenceSupport: string.Equals(category, "GeoFence", StringComparison.OrdinalIgnoreCase),
            RequiresRallyPointSupport: string.Equals(category, "Rally", StringComparison.OrdinalIgnoreCase),
            RawName: rawName,
            Description: GetString(item, "description"),
            FriendlyEdit: GetBool(item, "friendlyEdit"));
    }

    private static IReadOnlyList<MissionCommandParameterMetadata> ParseParameters(JsonElement item, IReadOnlySet<int> removedParams)
    {
        var parameters = new List<MissionCommandParameterMetadata>();
        for (var index = 1; index <= 7; index++)
        {
            if (removedParams.Contains(index))
            {
                continue;
            }

            var key = $"param{index}";
            if (!item.TryGetProperty(key, out var param) || param.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var label = GetString(param, "label");
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            parameters.Add(new MissionCommandParameterMetadata(
                index,
                label,
                GetString(param, "units"),
                GetString(param, "description"),
                GetNullableDouble(param, "default")));
        }

        return parameters;
    }

    private static HashSet<int> ParseParamRemove(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(static part => int.TryParse(part, out var index) ? index : 0)
            .Where(static index => index is >= 1 and <= 7)
            .ToHashSet();
    }

    private static int GetRequiredInt(JsonElement item, string key)
    {
        if (!item.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException($"Mission command metadata item is missing numeric '{key}'.");
        }

        return value.GetInt32();
    }

    private static string? GetString(JsonElement item, string key)
    {
        return item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool GetBool(JsonElement item, string key)
    {
        return item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static double? GetNullableDouble(JsonElement item, string key)
    {
        return item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;
    }
}

public sealed record MissionCommandAvailability(bool IsAvailable, string? Reason = null)
{
    public static MissionCommandAvailability Available { get; } = new(true);

    public static MissionCommandAvailability Unavailable(string reason)
    {
        return new MissionCommandAvailability(false, reason);
    }
}

public sealed class MissionCommandAvailabilityService
{
    private readonly IMissionCommandMetadataCatalog _catalog;

    public MissionCommandAvailabilityService(IMissionCommandMetadataCatalog catalog)
    {
        _catalog = catalog;
    }

    public MissionCommandAvailability GetAvailability(ushort commandId, IFirmwarePlugin firmwarePlugin)
    {
        var metadata = _catalog.Find(commandId);
        if (metadata is null)
        {
            return MissionCommandAvailability.Unavailable($"Mission command {commandId} is not known.");
        }

        if (metadata.RequiresGeoFenceSupport && !firmwarePlugin.Supports.GeoFenceTransfer)
        {
            return MissionCommandAvailability.Unavailable($"{metadata.Label} requires GeoFence support.");
        }

        if (metadata.RequiresRallyPointSupport && !firmwarePlugin.Supports.RallyPointTransfer)
        {
            return MissionCommandAvailability.Unavailable($"{metadata.Label} requires Rally support.");
        }

        return MissionCommandAvailability.Available;
    }
}
