namespace VGC.Maps;

public enum MapTileCacheStorageKind
{
    RuntimeHttpCache,
    ImportedOfflinePackage,
    ProviderManagedPackage
}

public sealed record MapTileCacheKey(
    MapProviderKind ProviderKind,
    string LayerId,
    int Zoom,
    int X,
    int Y)
{
    public string StableKey => $"{ProviderKind}:{LayerId}:{Zoom}/{X}/{Y}";
}

public sealed record MapTileCacheEntry(
    MapTileCacheKey Key,
    byte[] Bytes,
    string ContentType,
    DateTimeOffset CachedAt,
    DateTimeOffset? ExpiresAt = null)
{
    public long SizeBytes => Bytes.LongLength;
}

public sealed record MapTileCacheEntryMetadata(
    MapTileCacheKey Key,
    long SizeBytes,
    DateTimeOffset CachedAt,
    DateTimeOffset? ExpiresAt);

public sealed record MapTileCacheStoragePolicy(
    MapTileCacheStorageKind StorageKind,
    string DesktopPathHint,
    string AndroidPathHint,
    long MaxCacheBytes,
    TimeSpan MaxAge,
    bool AllowInteractiveNetworkCache,
    bool AllowBulkDownload,
    bool AllowOfflinePackageImport,
    string LicensingNotes);

public interface IMapTileCacheStore
{
    Task<MapTileCacheEntry?> LoadAsync(MapTileCacheKey key, CancellationToken cancellationToken = default);

    Task StoreAsync(MapTileCacheEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MapTileCacheEntryMetadata>> ListAsync(CancellationToken cancellationToken = default);

    Task EvictAsync(MapTileCacheStoragePolicy policy, CancellationToken cancellationToken = default);
}

public static class MapTileCachePolicyFactory
{
    private const long DefaultRuntimeCacheBytes = 256L * 1024 * 1024;

    public static MapTileCacheStoragePolicy CreateRuntimePolicy(MapProviderDescriptor provider)
    {
        return new MapTileCacheStoragePolicy(
            MapTileCacheStorageKind.RuntimeHttpCache,
            "LocalApplicationData/VGC/MapCache",
            "AppDataDirectory/VGC/MapCache",
            provider.Capabilities.SupportsOnlineTiles ? DefaultRuntimeCacheBytes : 0,
            TimeSpan.FromDays(7),
            AllowInteractiveNetworkCache: provider.Capabilities.SupportsOnlineTiles,
            AllowBulkDownload: provider.Capabilities.AllowsBulkTileDownload,
            AllowOfflinePackageImport: provider.Capabilities.SupportsOfflineTiles && provider.Capabilities.AllowsBulkTileDownload,
            provider.LicensingNotes);
    }
}
