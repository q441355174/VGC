namespace VGC.Maps;

public enum MapProviderKind
{
    LocalFallback,
    MapsuiRaster,
    TiandituRaster,
    MapLibreNative
}

public enum MapProviderProjection
{
    LocalNormalized,
    WebMercator,
    Geographic
}

public enum MapTileSourceKind
{
    None,
    Xyz,
    Wmts,
    MbTiles
}

public sealed record MapAttribution(
    string Text,
    Uri? Url = null,
    bool MustBeVisible = true);

public sealed record MapTileLayerDescriptor(
    string Id,
    string Name,
    MapTileSourceKind SourceKind,
    string? Template,
    string? ApiKeyParameterName,
    IReadOnlyList<MapAttribution> Attributions);

public sealed record MapProviderCapabilities(
    bool SupportsDesktop,
    bool SupportsAndroid,
    bool SupportsOnlineTiles,
    bool SupportsOfflineTiles,
    bool RequiresApiKey,
    bool RequiresVisibleAttribution,
    bool AllowsBulkTileDownload,
    bool SupportsProviderNativeGestures);

public sealed record MapProviderDescriptor(
    MapProviderKind Kind,
    string Id,
    string DisplayName,
    MapProviderProjection Projection,
    MapProviderCapabilities Capabilities,
    IReadOnlyList<MapTileLayerDescriptor> TileLayers,
    string LicensingNotes)
{
    public bool IsUsableOnCurrentTargets => Capabilities.SupportsDesktop && Capabilities.SupportsAndroid;
}

public sealed record MapProviderCameraState(
    MapCoordinate Center,
    double ZoomLevel,
    double BearingDegrees = 0,
    double PitchDegrees = 0)
{
    public static MapProviderCameraState FromViewport(MapViewport viewport)
    {
        return new MapProviderCameraState(viewport.Center, viewport.ZoomLevel);
    }

    public MapViewport ToViewport()
    {
        return new MapViewport(Center, ZoomLevel);
    }
}

public interface IMapProviderAdapter : IMapAdapter
{
    MapProviderDescriptor Descriptor { get; }

    Task ApplyCameraAsync(MapProviderCameraState camera, CancellationToken cancellationToken = default);

    Task<MapProviderCameraState> GetCameraAsync(CancellationToken cancellationToken = default);
}

public interface IMapProviderAdapterFactory
{
    IReadOnlyList<MapProviderDescriptor> GetAvailableProviders();

    IMapProviderAdapter CreateProvider(MapProviderKind kind);
}

public static class MapProviderCatalog
{
    public static readonly MapProviderDescriptor LocalFallback = new(
        MapProviderKind.LocalFallback,
        "local-fallback",
        "Local Vector Fallback",
        MapProviderProjection.LocalNormalized,
        new MapProviderCapabilities(
            SupportsDesktop: true,
            SupportsAndroid: true,
            SupportsOnlineTiles: false,
            SupportsOfflineTiles: false,
            RequiresApiKey: false,
            RequiresVisibleAttribution: false,
            AllowsBulkTileDownload: false,
            SupportsProviderNativeGestures: false),
        [],
        "Project-owned deterministic renderer for tests and no-provider fallback.");

    public static readonly MapProviderDescriptor MapsuiOsmRaster = new(
        MapProviderKind.MapsuiRaster,
        "mapsui-osm-raster",
        "Mapsui OSM Raster",
        MapProviderProjection.WebMercator,
        new MapProviderCapabilities(
            SupportsDesktop: true,
            SupportsAndroid: true,
            SupportsOnlineTiles: true,
            SupportsOfflineTiles: true,
            RequiresApiKey: false,
            RequiresVisibleAttribution: true,
            AllowsBulkTileDownload: false,
            SupportsProviderNativeGestures: true),
        [
            new MapTileLayerDescriptor(
                "osm-standard",
                "OpenStreetMap Standard",
                MapTileSourceKind.Xyz,
                "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                null,
                [new MapAttribution("OpenStreetMap contributors", new Uri("https://www.openstreetmap.org/copyright"))])
        ],
        "Mapsui is MIT licensed. OSM public tiles are for interactive development/demo only and must not be used for preload or offline bulk download.");

    public static readonly MapProviderDescriptor TiandituRaster = new(
        MapProviderKind.TiandituRaster,
        "tianditu-raster",
        "Tianditu Raster",
        MapProviderProjection.WebMercator,
        new MapProviderCapabilities(
            SupportsDesktop: true,
            SupportsAndroid: true,
            SupportsOnlineTiles: true,
            SupportsOfflineTiles: false,
            RequiresApiKey: true,
            RequiresVisibleAttribution: true,
            AllowsBulkTileDownload: false,
            SupportsProviderNativeGestures: true),
        [
            new MapTileLayerDescriptor(
                "tianditu-vector",
                "Tianditu Vector Base",
                MapTileSourceKind.Xyz,
                "https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk={tk}",
                "TIANDITU_TK",
                [new MapAttribution("Tianditu")]),
            new MapTileLayerDescriptor(
                "tianditu-vector-label",
                "Tianditu Vector Labels",
                MapTileSourceKind.Xyz,
                "https://t{s}.tianditu.gov.cn/cva_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=cva&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk={tk}",
                "TIANDITU_TK",
                [new MapAttribution("Tianditu")])
        ],
        "Tianditu requires a configured tk value. Offline cache, preload, or bulk download must remain disabled until service terms explicitly allow the chosen distribution mode.");

    public static IReadOnlyList<MapProviderDescriptor> Defaults { get; } =
    [
        LocalFallback,
        MapsuiOsmRaster,
        TiandituRaster
    ];

    public static MapProviderDescriptor Find(MapProviderKind kind)
    {
        return Defaults.First(provider => provider.Kind == kind);
    }
}
