namespace VGC.Facts;

using System.Text.Json;

public sealed record ParameterEnumValue(string Value, string Label);

public sealed record ParameterMetadata(
    string Name,
    int? ComponentId = null,
    string? Group = null,
    string? Label = null,
    string? Description = null,
    string? Units = null,
    double? Min = null,
    double? Max = null,
    IReadOnlyList<ParameterEnumValue>? EnumValues = null,
    bool RebootRequired = false);

public sealed record ParameterMetadataSourceContext(
    string FirmwareId,
    string VehicleType,
    string? MetadataPackageVersion = null);

public interface IParameterMetadataCatalog
{
    IReadOnlyList<ParameterMetadata> All { get; }

    ParameterMetadata? Find(int componentId, string name);
}

public interface IParameterMetadataSource
{
    Task<IParameterMetadataCatalog> LoadAsync(
        ParameterMetadataSourceContext context,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryParameterMetadataCatalog : IParameterMetadataCatalog
{
    private readonly Dictionary<(int? ComponentId, string Name), ParameterMetadata> _metadata;

    public InMemoryParameterMetadataCatalog(IEnumerable<ParameterMetadata> metadata)
    {
        _metadata = metadata.ToDictionary(
            static item => (item.ComponentId, item.Name),
            static item => item);
    }

    public static InMemoryParameterMetadataCatalog Empty { get; } = new([]);

    public IReadOnlyList<ParameterMetadata> All => _metadata.Values
        .OrderBy(static item => item.ComponentId ?? 0)
        .ThenBy(static item => item.Name, StringComparer.Ordinal)
        .ToArray();

    public ParameterMetadata? Find(int componentId, string name)
    {
        return _metadata.TryGetValue((componentId, name), out var componentMatch)
            ? componentMatch
            : _metadata.TryGetValue((null, name), out var globalMatch)
                ? globalMatch
                : null;
    }
}

public sealed class ParameterMetadataRuntime
{
    public IParameterMetadataCatalog Catalog { get; private set; } = InMemoryParameterMetadataCatalog.Empty;

    public ParameterMetadataSourceContext? LastContext { get; private set; }

    public async Task<IParameterMetadataCatalog> LoadAsync(
        IParameterMetadataSource source,
        ParameterMetadataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        Catalog = await source.LoadAsync(context, cancellationToken).ConfigureAwait(false);
        LastContext = context;
        return Catalog;
    }
}

public sealed class JsonParameterMetadataSource : IParameterMetadataSource
{
    private readonly Func<CancellationToken, Task<string>> _jsonLoader;

    public JsonParameterMetadataSource(string json)
        : this(_ => Task.FromResult(json))
    {
    }

    public JsonParameterMetadataSource(Func<CancellationToken, Task<string>> jsonLoader)
    {
        _jsonLoader = jsonLoader;
    }

    public async Task<IParameterMetadataCatalog> LoadAsync(
        ParameterMetadataSourceContext context,
        CancellationToken cancellationToken = default)
    {
        var json = await _jsonLoader(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var metadata = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => ParseParameterArray(document.RootElement),
            JsonValueKind.Object => ParseObject(document.RootElement, context),
            _ => []
        };

        return new InMemoryParameterMetadataCatalog(metadata);
    }

    public static JsonParameterMetadataSource FromFile(string path)
    {
        return new JsonParameterMetadataSource(cancellationToken => File.ReadAllTextAsync(path, cancellationToken));
    }

    private static IReadOnlyList<ParameterMetadata> ParseObject(JsonElement root, ParameterMetadataSourceContext context)
    {
        if (root.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Array)
        {
            var metadata = new List<ParameterMetadata>();
            foreach (var package in packages.EnumerateArray())
            {
                if (package.ValueKind == JsonValueKind.Object && MatchesContext(package, context))
                {
                    metadata.AddRange(ParsePackageParameters(package));
                }
            }

            return metadata;
        }

        return MatchesContext(root, context)
            ? ParsePackageParameters(root)
            : [];
    }

    private static IReadOnlyList<ParameterMetadata> ParsePackageParameters(JsonElement package)
    {
        if (package.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Array)
        {
            return ParseParameterArray(parameters);
        }

        return package.TryGetProperty("parameterFacts", out var parameterFacts) && parameterFacts.ValueKind == JsonValueKind.Array
            ? ParseParameterArray(parameterFacts)
            : [];
    }

    private static bool MatchesContext(JsonElement item, ParameterMetadataSourceContext context)
    {
        return MatchesStringProperty(item, "firmwareId", context.FirmwareId)
            && MatchesStringProperty(item, "vehicleType", context.VehicleType)
            && MatchesStringProperty(item, "metadataPackageVersion", context.MetadataPackageVersion, allowMissingExpected: true);
    }

    private static bool MatchesStringProperty(JsonElement item, string propertyName, string? expected, bool allowMissingExpected = false)
    {
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(expected))
        {
            return allowMissingExpected;
        }

        return string.Equals(property.GetString(), expected, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ParameterMetadata> ParseParameterArray(JsonElement parameters)
    {
        var metadata = new List<ParameterMetadata>();
        foreach (var item in parameters.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !TryGetString(item, "name", out var name))
            {
                continue;
            }

            metadata.Add(new ParameterMetadata(
                name,
                ComponentId: TryGetInt(item, "componentId", out var componentId) ? componentId : null,
                Group: GetOptionalString(item, "group"),
                Label: GetOptionalString(item, "label") ?? GetOptionalString(item, "shortDescription") ?? GetOptionalString(item, "shortDesc"),
                Description: GetOptionalString(item, "description") ?? GetOptionalString(item, "longDescription") ?? GetOptionalString(item, "longDesc"),
                Units: GetOptionalString(item, "units"),
                Min: TryGetDouble(item, "min", out var min) ? min : null,
                Max: TryGetDouble(item, "max", out var max) ? max : null,
                EnumValues: ParseEnumValues(item),
                RebootRequired: TryGetBool(item, "rebootRequired", out var rebootRequired) && rebootRequired));
        }

        return metadata;
    }

    private static IReadOnlyList<ParameterEnumValue>? ParseEnumValues(JsonElement item)
    {
        var property = item.TryGetProperty("enumValues", out var enumValues)
            ? enumValues
            : item.TryGetProperty("values", out var values)
                ? values
                : default;

        if (property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parsed = new List<ParameterEnumValue>();
        foreach (var value in property.EnumerateArray())
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var rawValue = GetOptionalString(value, "value") ?? GetOptionalString(value, "code");
                var label = GetOptionalString(value, "label") ?? GetOptionalString(value, "description") ?? rawValue;
                if (!string.IsNullOrWhiteSpace(rawValue) && !string.IsNullOrWhiteSpace(label))
                {
                    parsed.Add(new ParameterEnumValue(rawValue, label));
                }
            }
        }

        return parsed.Count == 0 ? null : parsed;
    }

    private static bool TryGetString(JsonElement item, string propertyName, out string value)
    {
        value = string.Empty;
        if (!item.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? GetOptionalString(JsonElement item, string propertyName)
    {
        return TryGetString(item, propertyName, out var value) ? value : null;
    }

    private static bool TryGetInt(JsonElement item, string propertyName, out int value)
    {
        value = 0;
        return item.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out value);
    }

    private static bool TryGetDouble(JsonElement item, string propertyName, out double value)
    {
        value = 0;
        return item.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out value);
    }

    private static bool TryGetBool(JsonElement item, string propertyName, out bool value)
    {
        value = false;
        if (!item.TryGetProperty(propertyName, out var property) || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }
}
