namespace VGC.Facts;

public sealed record ParameterSetupRow(
    int ComponentId,
    string Name,
    string Group,
    string Label,
    string DisplayValue,
    bool RestartRequired,
    ParameterWriteStatus WriteStatus);

public sealed class ParameterSetupProjection
{
    public IReadOnlyList<ParameterSetupRow> BuildRows(
        ParameterManager manager,
        IParameterMetadataCatalog metadataCatalog,
        string? groupFilter = null,
        string? searchText = null)
    {
        var rows = ParameterProjection.BuildRows(manager, metadataCatalog, searchText ?? string.Empty)
            .Select(row =>
            {
                var metadata = metadataCatalog.Find(row.ComponentId, row.Name);
                return new ParameterSetupRow(
                    row.ComponentId,
                    row.Name,
                    metadata?.Group ?? "Ungrouped",
                    metadata?.Label ?? row.Name,
                    row.Value,
                    metadata?.RebootRequired == true,
                    Enum.TryParse<ParameterWriteStatus>(row.WriteStatus, out var status) ? status : ParameterWriteStatus.None);
            });

        if (!string.IsNullOrWhiteSpace(groupFilter))
        {
            rows = rows.Where(row => string.Equals(row.Group, groupFilter, StringComparison.OrdinalIgnoreCase));
        }

        return rows
            .OrderBy(static row => row.Group, StringComparer.Ordinal)
            .ThenBy(static row => row.Name, StringComparer.Ordinal)
            .ToArray();
    }
}
