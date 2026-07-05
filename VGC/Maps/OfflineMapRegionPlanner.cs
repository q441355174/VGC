namespace VGC.Maps;

public sealed record OfflineMapRegion(
    string Name,
    MapCoordinate Center,
    int MinZoom,
    int MaxZoom,
    int EstimatedTileCount,
    long EstimatedSizeBytes)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Name)
        && MinZoom >= 0
        && MaxZoom >= MinZoom
        && EstimatedTileCount > 0
        && EstimatedSizeBytes > 0;
}

public sealed record OfflineMapProviderPolicy(
    MapProviderDescriptor Provider,
    bool CanPlanOfflineRegion,
    bool CanBulkDownload,
    string Reason);

public sealed class OfflineMapRegionPlanner
{
    private const long EstimatedBytesPerTile = 32 * 1024;

    public OfflineMapProviderPolicy BuildPolicy(MapProviderDescriptor provider)
    {
        if (!provider.Capabilities.SupportsOfflineTiles)
        {
            return new OfflineMapProviderPolicy(provider, false, false, "Provider does not advertise offline tile support.");
        }

        if (!provider.Capabilities.AllowsBulkTileDownload)
        {
            return new OfflineMapProviderPolicy(provider, true, false, "Offline region planning is available, but bulk tile download is disabled by provider policy.");
        }

        return new OfflineMapProviderPolicy(provider, true, true, "Provider allows offline tile planning and bulk download.");
    }

    public OfflineMapRegion PlanRegion(string name, MapCoordinate center, int minZoom, int maxZoom)
    {
        var normalizedMin = Math.Max(0, minZoom);
        var normalizedMax = Math.Max(normalizedMin, maxZoom);
        var tileCount = Enumerable.Range(normalizedMin, normalizedMax - normalizedMin + 1)
            .Sum(static zoom => Math.Max(1, 1 << Math.Min(zoom, 12)));

        return new OfflineMapRegion(
            name.Trim(),
            center,
            normalizedMin,
            normalizedMax,
            tileCount,
            tileCount * EstimatedBytesPerTile);
    }
}
