namespace VGC.Maps;

public sealed record MapFeatureSelection(
    string FeatureId,
    MapProviderOverlayLayer Layer,
    MapCoordinate Coordinate,
    string Label);

public sealed record MapInteractionSnapshot(
    MapViewport Viewport,
    bool IsFollowingVehicle,
    MapFeatureSelection? SelectedFeature,
    string ModeText);

public sealed class MapInteractionRuntime
{
    private readonly MapInteractionState _state = new();
    private MapCoordinate? _activeVehicle;
    private MapFeatureSelection? _selectedFeature;

    public MapInteractionSnapshot Snapshot => BuildSnapshot();

    public void UpdateActiveVehicle(MapCoordinate coordinate)
    {
        _activeVehicle = coordinate;
    }

    public void PanTo(MapCoordinate center)
    {
        var current = _state.ResolveViewport(_activeVehicle);
        _state.MarkManualViewport(current with { Center = center });
    }

    public void ZoomTo(double zoomLevel)
    {
        var current = _state.ResolveViewport(_activeVehicle);
        _state.MarkManualViewport(current with { ZoomLevel = ClampZoom(zoomLevel) });
    }

    public void SelectFeature(MapFeatureSelection selection)
    {
        _selectedFeature = selection;
    }

    public void ClearSelection()
    {
        _selectedFeature = null;
    }

    public void RecenterOnVehicle()
    {
        _state.RecenterOnVehicle();
    }

    private MapInteractionSnapshot BuildSnapshot()
    {
        var viewport = _state.ResolveViewport(_activeVehicle);
        return new MapInteractionSnapshot(
            viewport,
            _state.IsFollowingVehicle,
            _selectedFeature,
            _state.IsFollowingVehicle ? "Follow vehicle" : "Manual map");
    }

    private static double ClampZoom(double zoomLevel)
    {
        return Math.Clamp(zoomLevel, 1, 22);
    }
}

public sealed record MapAttributionUiState(
    string ProviderName,
    IReadOnlyList<AttributionRequirement> RequiredAttributions,
    string DisplayText,
    bool MustShowAttribution,
    bool BulkDownloadAllowed,
    string PolicyText);

public sealed class MapAttributionUiProjector
{
    private readonly MapAttributionService _service = new();

    public MapAttributionUiState Project(MapProviderDescriptor provider)
    {
        var attributions = _service.GetActiveAttributions(provider);
        var displayText = _service.GetAttributionText(provider);
        return new MapAttributionUiState(
            provider.DisplayName,
            attributions,
            displayText,
            provider.Capabilities.RequiresVisibleAttribution || attributions.Any(static a => a.MustBeVisible),
            provider.Capabilities.AllowsBulkTileDownload,
            provider.LicensingNotes);
    }
}

public sealed record MapBounds(
    double South,
    double West,
    double North,
    double East)
{
    public bool IsValid => South < North && West < East;
}

public sealed record OfflineMapRegionRequest(
    string Name,
    MapProviderDescriptor Provider,
    MapBounds Bounds,
    int MinZoom,
    int MaxZoom);

public sealed record OfflineMapRegionEstimate(
    string Name,
    MapProviderKind ProviderKind,
    MapBounds Bounds,
    int MinZoom,
    int MaxZoom,
    long TileCount,
    long EstimatedBytes,
    bool IsDownloadAllowed,
    string PolicyText);

public sealed class OfflineMapRegionEstimator
{
    private const long DefaultTileBytes = 24 * 1024;

    public OfflineMapRegionEstimate Estimate(OfflineMapRegionRequest request)
    {
        if (!request.Bounds.IsValid)
        {
            throw new ArgumentException("Offline region bounds must have south<north and west<east.", nameof(request));
        }

        if (request.MinZoom < 0 || request.MaxZoom < request.MinZoom)
        {
            throw new ArgumentException("Offline region zoom bounds are invalid.", nameof(request));
        }

        var tileCount = 0L;
        for (var zoom = request.MinZoom; zoom <= request.MaxZoom; zoom++)
        {
            var westX = LongitudeToTileX(request.Bounds.West, zoom);
            var eastX = LongitudeToTileX(request.Bounds.East, zoom);
            var northY = LatitudeToTileY(request.Bounds.North, zoom);
            var southY = LatitudeToTileY(request.Bounds.South, zoom);
            tileCount += Math.Max(1, eastX - westX + 1) * Math.Max(1, southY - northY + 1);
        }

        var policy = MapTileCachePolicyFactory.CreateRuntimePolicy(request.Provider);
        return new OfflineMapRegionEstimate(
            request.Name,
            request.Provider.Kind,
            request.Bounds,
            request.MinZoom,
            request.MaxZoom,
            tileCount,
            tileCount * DefaultTileBytes,
            policy.AllowBulkDownload,
            policy.LicensingNotes);
    }

    private static int LongitudeToTileX(double longitude, int zoom)
    {
        var n = 1 << zoom;
        return (int)Math.Floor((longitude + 180.0) / 360.0 * n);
    }

    private static int LatitudeToTileY(double latitude, int zoom)
    {
        var clamped = Math.Clamp(latitude, -85.05112878, 85.05112878);
        var radians = clamped * Math.PI / 180.0;
        var n = 1 << zoom;
        return (int)Math.Floor((1.0 - Math.Log(Math.Tan(radians) + 1.0 / Math.Cos(radians)) / Math.PI) / 2.0 * n);
    }
}

public enum OfflineMapDownloadState
{
    Queued,
    Downloading,
    Paused,
    Completed,
    Failed,
    Cancelled,
    Blocked
}

public sealed record OfflineMapDownloadJob(
    string Id,
    OfflineMapRegionEstimate Region,
    OfflineMapDownloadState State,
    long DownloadedTiles,
    string? FailureReason)
{
    public double ProgressPercent => Region.TileCount == 0
        ? 0
        : Math.Round(100.0 * DownloadedTiles / Region.TileCount, 1);
}

public sealed class OfflineMapDownloadQueue
{
    private readonly Dictionary<string, OfflineMapDownloadJob> _jobs = [];

    public IReadOnlyList<OfflineMapDownloadJob> Jobs => _jobs.Values.OrderBy(static job => job.Id).ToArray();

    public OfflineMapDownloadJob Enqueue(string id, OfflineMapRegionEstimate region)
    {
        var state = region.IsDownloadAllowed ? OfflineMapDownloadState.Queued : OfflineMapDownloadState.Blocked;
        var reason = region.IsDownloadAllowed ? null : "Provider policy blocks bulk offline tile download.";
        var job = new OfflineMapDownloadJob(id, region, state, 0, reason);
        _jobs[id] = job;
        return job;
    }

    public OfflineMapDownloadJob Start(string id)
    {
        var job = Get(id);
        return Update(job with
        {
            State = job.State == OfflineMapDownloadState.Blocked ? OfflineMapDownloadState.Blocked : OfflineMapDownloadState.Downloading
        });
    }

    public OfflineMapDownloadJob ReportProgress(string id, long downloadedTiles)
    {
        var job = Get(id);
        var clamped = Math.Clamp(downloadedTiles, 0, job.Region.TileCount);
        return Update(job with
        {
            DownloadedTiles = clamped,
            State = clamped >= job.Region.TileCount ? OfflineMapDownloadState.Completed : job.State
        });
    }

    public OfflineMapDownloadJob Pause(string id)
    {
        var job = Get(id);
        return Update(job with { State = OfflineMapDownloadState.Paused });
    }

    public OfflineMapDownloadJob Resume(string id)
    {
        var job = Get(id);
        return Update(job with { State = OfflineMapDownloadState.Downloading });
    }

    public OfflineMapDownloadJob Fail(string id, string reason)
    {
        var job = Get(id);
        return Update(job with { State = OfflineMapDownloadState.Failed, FailureReason = reason });
    }

    public OfflineMapDownloadJob Retry(string id)
    {
        var job = Get(id);
        return Update(job with { State = OfflineMapDownloadState.Queued, FailureReason = null });
    }

    public OfflineMapDownloadJob Cancel(string id)
    {
        var job = Get(id);
        return Update(job with { State = OfflineMapDownloadState.Cancelled });
    }

    private OfflineMapDownloadJob Get(string id)
    {
        return _jobs.TryGetValue(id, out var job)
            ? job
            : throw new InvalidOperationException($"Offline map download job '{id}' was not found.");
    }

    private OfflineMapDownloadJob Update(OfflineMapDownloadJob job)
    {
        _jobs[job.Id] = job;
        return job;
    }
}

public sealed record MapTileCacheCleanupResult(
    int RemovedEntries,
    long RemovedBytes,
    long RemainingBytes);

public sealed class InMemoryMapTileCacheStore : IMapTileCacheStore
{
    private readonly Dictionary<string, MapTileCacheEntry> _entries = [];

    public Task<MapTileCacheEntry?> LoadAsync(MapTileCacheKey key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue(key.StableKey, out var entry);
        return Task.FromResult(entry);
    }

    public Task StoreAsync(MapTileCacheEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries[entry.Key.StableKey] = entry;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MapTileCacheEntryMetadata>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<MapTileCacheEntryMetadata> result = _entries.Values
            .Select(static entry => new MapTileCacheEntryMetadata(entry.Key, entry.SizeBytes, entry.CachedAt, entry.ExpiresAt))
            .OrderBy(static entry => entry.CachedAt)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task EvictAsync(MapTileCacheStoragePolicy policy, CancellationToken cancellationToken = default)
    {
        return CleanupAsync(policy, DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task<MapTileCacheCleanupResult> CleanupAsync(
        MapTileCacheStoragePolicy policy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removedEntries = 0;
        var removedBytes = 0L;

        foreach (var entry in _entries.Values.ToArray())
        {
            if (entry.ExpiresAt <= now || now - entry.CachedAt > policy.MaxAge)
            {
                removedEntries++;
                removedBytes += entry.SizeBytes;
                _entries.Remove(entry.Key.StableKey);
            }
        }

        var totalBytes = _entries.Values.Sum(static entry => entry.SizeBytes);
        foreach (var entry in _entries.Values.OrderBy(static entry => entry.CachedAt).ToArray())
        {
            if (totalBytes <= policy.MaxCacheBytes)
            {
                break;
            }

            removedEntries++;
            removedBytes += entry.SizeBytes;
            totalBytes -= entry.SizeBytes;
            _entries.Remove(entry.Key.StableKey);
        }

        return Task.FromResult(new MapTileCacheCleanupResult(removedEntries, removedBytes, totalBytes));
    }
}

public enum AndroidMapLifecycleEvent
{
    Start,
    Pause,
    Resume,
    Stop,
    NetworkLost,
    NetworkAvailable,
    StorageLow,
    StorageRecovered
}

public sealed record AndroidMapLifecycleState(
    bool IsVisible,
    bool IsNetworkAvailable,
    bool IsStorageConstrained,
    bool ShouldSuspendTileRequests,
    bool ShouldTrimMemory,
    string Summary);

public sealed class AndroidMapLifecycleCoordinator
{
    private bool _isVisible;
    private bool _networkAvailable = true;
    private bool _storageConstrained;

    public AndroidMapLifecycleState State => BuildState();

    public AndroidMapLifecycleState Apply(AndroidMapLifecycleEvent lifecycleEvent)
    {
        switch (lifecycleEvent)
        {
            case AndroidMapLifecycleEvent.Start:
            case AndroidMapLifecycleEvent.Resume:
                _isVisible = true;
                break;
            case AndroidMapLifecycleEvent.Pause:
            case AndroidMapLifecycleEvent.Stop:
                _isVisible = false;
                break;
            case AndroidMapLifecycleEvent.NetworkLost:
                _networkAvailable = false;
                break;
            case AndroidMapLifecycleEvent.NetworkAvailable:
                _networkAvailable = true;
                break;
            case AndroidMapLifecycleEvent.StorageLow:
                _storageConstrained = true;
                break;
            case AndroidMapLifecycleEvent.StorageRecovered:
                _storageConstrained = false;
                break;
        }

        return BuildState();
    }

    private AndroidMapLifecycleState BuildState()
    {
        var shouldSuspend = !_isVisible || !_networkAvailable;
        return new AndroidMapLifecycleState(
            _isVisible,
            _networkAvailable,
            _storageConstrained,
            shouldSuspend,
            _storageConstrained || !_isVisible,
            shouldSuspend
                ? "Map tile requests suspended until lifecycle/network is ready."
                : "Map tile requests allowed.");
    }
}

public sealed record MapRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class MapRuntimeEvidenceCatalog
{
    public IReadOnlyList<MapRuntimeEvidenceItem> Build()
    {
        return
        [
            new("MAPOFF-235", "L1/L2", "Shared map interaction runtime covers pan, zoom, select, follow, and recenter.", true),
            new("MAPOFF-236", "L1/L2", "Provider attribution UI projection keeps visible attribution and policy text outside platform views.", true),
            new("MAPOFF-237", "L1", "Offline region model estimates zoom-bounded WebMercator tile counts.", true),
            new("MAPOFF-238", "L1", "Offline download queue models queued, pause, resume, cancel, retry, failure, and provider-policy blocking.", true),
            new("MAPOFF-239", "L1", "Tile cache store models expiry and size-based cleanup with platform path hints.", true),
            new("MAPOFF-240", "L1/L4", "Android map lifecycle coordinator documents pause/resume/network/storage behavior; physical-device evidence deferred.", true),
            new("MAPOFF-241", "L0/L3/L4", "Desktop/Android runtime evidence remains checklist/catalog level until UI/device validation is available.", false)
        ];
    }
}

public sealed record MapOfflineParityAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class MapOfflineParityAudit
{
    public MapOfflineParityAuditResult Audit(IReadOnlyList<MapRuntimeEvidenceItem> evidence)
    {
        var complete = evidence.Count(static item => item.Complete);
        var deferred = evidence.Where(static item => !item.Complete).Select(static item => item.Description).ToArray();
        return new MapOfflineParityAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} map/offline evidence items complete; {deferred.Length} deferred runtime/device gaps remain.");
    }
}
