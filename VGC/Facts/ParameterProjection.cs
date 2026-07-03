namespace VGC.Facts;

public sealed record ParameterDisplayRow(
    int ComponentId,
    string Name,
    string Value,
    string Type,
    bool IsPendingWrite,
    string Label,
    string Group,
    string Description,
    string Units,
    string Range,
    string EnumValues,
    bool RebootRequired,
    string WriteStatus,
    int WriteRetryCount,
    string WriteError,
    string CacheState,
    bool HasMetadata);

public static class ParameterProjection
{
    public static IReadOnlyList<ParameterDisplayRow> BuildRows(
        ParameterManager manager,
        IParameterMetadataCatalog? metadataCatalog = null,
        string searchText = "",
        ParameterCacheSnapshot? cacheSnapshot = null,
        DateTimeOffset? now = null,
        TimeSpan? staleAfter = null,
        bool includeMissingMetadata = false)
    {
        var rows = manager.Parameters.Select(fact => ToRow(manager, metadataCatalog, fact)).ToList();
        AddCacheRows(rows, manager, metadataCatalog, cacheSnapshot, now, staleAfter, includeMissingMetadata);
        IEnumerable<ParameterDisplayRow> result = rows;
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var search = searchText.Trim();
            result = result.Where(row => Matches(row, search));
        }

        return result
            .OrderBy(static row => row.ComponentId)
            .ThenBy(static row => row.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static ParameterDisplayRow ToRow(
        ParameterManager manager,
        IParameterMetadataCatalog? metadataCatalog,
        Fact fact)
    {
        var metadata = metadataCatalog?.Find(fact.ComponentId, fact.Name);
        var writeState = manager.GetParameterWriteState(fact.ComponentId, fact.Name);
        return new ParameterDisplayRow(
            fact.ComponentId,
            fact.Name,
            fact.DisplayValue,
            fact.MetaData.ValueType.ToString(),
            manager.IsParameterWritePending(fact.ComponentId, fact.Name),
            metadata?.Label ?? fact.Name,
            metadata?.Group ?? string.Empty,
            metadata?.Description ?? fact.MetaData.ShortDescription ?? string.Empty,
            metadata?.Units ?? fact.MetaData.Units ?? string.Empty,
            FormatRange(metadata),
            FormatEnumValues(metadata),
            metadata?.RebootRequired ?? false,
            writeState.Status.ToString(),
            writeState.RetryCount,
            writeState.LastError ?? string.Empty,
            "Live",
            metadata is not null);
    }

    private static void AddCacheRows(
        List<ParameterDisplayRow> rows,
        ParameterManager manager,
        IParameterMetadataCatalog? metadataCatalog,
        ParameterCacheSnapshot? cacheSnapshot,
        DateTimeOffset? now,
        TimeSpan? staleAfter,
        bool includeMissingMetadata)
    {
        var liveKeys = manager.Parameters
            .Select(static fact => (fact.ComponentId, fact.Name))
            .ToHashSet();
        var cacheKeys = new HashSet<(int ComponentId, string Name)>();
        var cacheState = cacheSnapshot is null
            ? "Missing"
            : cacheSnapshot.IsStale(now ?? DateTimeOffset.Now, staleAfter ?? TimeSpan.FromDays(7))
                ? "Stale"
                : "Cached";

        if (cacheSnapshot is not null)
        {
            foreach (var entry in cacheSnapshot.Parameters)
            {
                var key = (entry.ComponentId, entry.Name);
                cacheKeys.Add(key);
                if (liveKeys.Contains(key))
                {
                    continue;
                }

                var metadata = metadataCatalog?.Find(entry.ComponentId, entry.Name);
                rows.Add(new ParameterDisplayRow(
                    entry.ComponentId,
                    entry.Name,
                    entry.Value,
                    entry.ValueType.ToString(),
                    false,
                    metadata?.Label ?? entry.Name,
                    metadata?.Group ?? string.Empty,
                    metadata?.Description ?? string.Empty,
                    metadata?.Units ?? string.Empty,
                    FormatRange(metadata),
                    FormatEnumValues(metadata),
                    metadata?.RebootRequired ?? false,
                    ParameterWriteStatus.None.ToString(),
                    0,
                    string.Empty,
                    cacheState,
                    metadata is not null));
            }
        }

        if (metadataCatalog is null || !includeMissingMetadata)
        {
            return;
        }

        foreach (var metadata in metadataCatalog.All)
        {
            var componentId = metadata.ComponentId ?? 0;
            var key = (componentId, metadata.Name);
            if (liveKeys.Contains(key) || cacheKeys.Contains(key))
            {
                continue;
            }

            rows.Add(new ParameterDisplayRow(
                componentId,
                metadata.Name,
                string.Empty,
                "Unknown",
                false,
                metadata.Label ?? metadata.Name,
                metadata.Group ?? string.Empty,
                metadata.Description ?? string.Empty,
                metadata.Units ?? string.Empty,
                FormatRange(metadata),
                FormatEnumValues(metadata),
                metadata.RebootRequired,
                ParameterWriteStatus.None.ToString(),
                0,
                string.Empty,
                "Missing",
                true));
        }
    }

    private static string FormatRange(ParameterMetadata? metadata)
    {
        if (metadata is null || (metadata.Min is null && metadata.Max is null))
        {
            return string.Empty;
        }

        var min = metadata.Min?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-inf";
        var max = metadata.Max?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "+inf";
        return $"{min}..{max}";
    }

    private static string FormatEnumValues(ParameterMetadata? metadata)
    {
        return metadata?.EnumValues is { Count: > 0 } values
            ? string.Join(", ", values.Select(static item => $"{item.Value}={item.Label}"))
            : string.Empty;
    }

    private static bool Matches(ParameterDisplayRow row, string search)
    {
        return row.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Type.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.ComponentId.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Group.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Units.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Range.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.EnumValues.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.WriteStatus.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.WriteError.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.CacheState.Contains(search, StringComparison.OrdinalIgnoreCase);
    }
}
