namespace VGC.Maps;

public sealed record MapProviderHostState(
    MapProviderDescriptor ActiveProvider,
    IReadOnlyList<MapProviderDescriptor> AvailableProviders)
{
    public bool IsLocalFallback => ActiveProvider.Kind == MapProviderKind.LocalFallback;

    public bool IsProviderBacked => !IsLocalFallback;

    public string RuntimeLabel => IsLocalFallback
        ? "Local fallback"
        : ActiveProvider.DisplayName;
}

public sealed class MapProviderHost : IMapProviderAdapterFactory
{
    private readonly Dictionary<MapProviderKind, IMapProviderAdapter> _adapters;
    private IMapProviderAdapter _activeAdapter;

    public MapProviderHost(IEnumerable<IMapProviderAdapter> adapters, MapProviderKind preferredProvider = MapProviderKind.LocalFallback)
    {
        _adapters = adapters.ToDictionary(static adapter => adapter.Descriptor.Kind);
        if (_adapters.Count == 0)
        {
            throw new ArgumentException("At least one map provider adapter is required.", nameof(adapters));
        }

        _activeAdapter = _adapters.TryGetValue(preferredProvider, out var preferred)
            ? preferred
            : _adapters.Values.First();
    }

    public static MapProviderHost CreateLocalOnly()
    {
        return new MapProviderHost([new LocalMapRuntime()]);
    }

    public static MapProviderHost CreateDesktopDefault(TiandituApiKeyService? tiandituApiKeyService = null)
    {
        var tianditu = new TiandituRasterAdapter(tiandituApiKeyService ?? new TiandituApiKeyService());
        var adapters = new List<IMapProviderAdapter>
        {
            new LocalMapRuntime(),
            new RasterTileMapAdapter(MapProviderCatalog.MapsuiOsmRaster)
        };

        if (tianditu.IsAvailable)
        {
            adapters.Add(tianditu);
        }

        return new MapProviderHost(adapters, MapProviderKind.MapsuiRaster);
    }

    public MapProviderHostState State => new(_activeAdapter.Descriptor, GetAvailableProviders());

    public IMapProviderAdapter ActiveAdapter => _activeAdapter;

    public MapProviderDescriptor ActiveProvider => _activeAdapter.Descriptor;

    public IMapRasterTileSource? ActiveRasterTiles => _activeAdapter as IMapRasterTileSource;

    public MapTileLayerDescriptor? ActiveBaseLayer =>
        _activeAdapter.Descriptor.TileLayers.FirstOrDefault(static layer => layer.Template is not null);

    public IReadOnlyList<MapProviderDescriptor> GetAvailableProviders()
    {
        return _adapters.Values
            .Select(static adapter => adapter.Descriptor)
            .Where(static descriptor => descriptor.IsUsableOnCurrentTargets)
            .OrderBy(static descriptor => descriptor.DisplayName)
            .ToArray();
    }

    public IMapProviderAdapter CreateProvider(MapProviderKind kind)
    {
        return _adapters.TryGetValue(kind, out var adapter)
            ? adapter
            : throw new InvalidOperationException($"Map provider '{kind}' is not registered in this host.");
    }

    public bool TrySelectProvider(MapProviderKind kind)
    {
        if (!_adapters.TryGetValue(kind, out var adapter))
        {
            return false;
        }

        _activeAdapter = adapter;
        return true;
    }

    public MapDisplayFrame RenderDisplayFrame(MapOverlayFrame overlays, MapViewport viewport)
    {
        var camera = MapProviderCameraState.FromViewport(viewport);
        _activeAdapter.ApplyCameraAsync(camera).GetAwaiter().GetResult();
        _activeAdapter.ApplyOverlaysAsync(overlays).GetAwaiter().GetResult();

        return _activeAdapter is LocalMapRuntime localRuntime
            ? localRuntime.DisplayFrame
            : new MapDisplayFrame(_activeAdapter.Descriptor.DisplayName, viewport, null, null, null);
    }
}
