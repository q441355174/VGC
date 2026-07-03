namespace VGC.Maps;

public enum MapProductionRuntimeStatus
{
    Complete,
    SharedModelOnly,
    Blocked
}

public sealed record MapProductionRuntimeItem(
    string Id,
    string Area,
    MapProductionRuntimeStatus Status,
    string Owner,
    IReadOnlyList<string> CoveredCapabilities,
    IReadOnlyList<string> RequiredRuntimeEvidence);

public sealed class MapProductionRuntimeCatalog
{
    public IReadOnlyList<MapProductionRuntimeItem> BuildPhase329()
    {
        return
        [
            new("MAP329-PROVIDER", "Provider rendering", MapProductionRuntimeStatus.SharedModelOnly, "MapProviderHost/RasterTileMapAdapter", ["provider selection", "raster tile URL generation", "local fallback"], ["desktop provider screenshot", "tile fetch transcript"]),
            new("MAP329-ATTRIBUTION", "Attribution display", MapProductionRuntimeStatus.SharedModelOnly, "MapAttributionUiProjector", ["visible attribution decision", "bulk download policy text"], ["screenshot showing provider attribution"]),
            new("MAP329-OFFLINE", "Offline regions", MapProductionRuntimeStatus.SharedModelOnly, "OfflineMapRegionEstimator/OfflineMapDownloadQueue", ["tile count estimate", "queue state", "provider policy blocking"], ["offline download transcript", "blocked provider policy transcript"]),
            new("MAP329-CACHE", "Persistent cache", MapProductionRuntimeStatus.SharedModelOnly, "IMapTileCacheStore", ["in-memory expiry and size cleanup"], ["persistent disk cache adapter", "cache reload transcript"]),
            new("MAP329-ANDROID", "Android map lifecycle/performance", MapProductionRuntimeStatus.Blocked, "AndroidMapLifecycleCoordinator", ["lifecycle/network/storage state model"], ["device FPS/memory notes", "pause/resume/network transcript"])
        ];
    }
}

public sealed class MapProductionRuntimeAudit
{
    public IReadOnlyList<string> MissingEvidence(IReadOnlyList<MapProductionRuntimeItem> items)
    {
        return items
            .Where(static item => item.Status != MapProductionRuntimeStatus.Complete)
            .SelectMany(static item => item.RequiredRuntimeEvidence.Select(evidence => $"{item.Id}: {evidence}"))
            .ToArray();
    }
}
