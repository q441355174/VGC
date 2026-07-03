namespace VGC.Maps;

public enum MapProviderType
{
    Local,
    Tianditu,
    Google,
    Bing,
    OpenStreetMap,
    ArcGIS,
    Mapbox,
    Custom
}

public sealed record MapProviderInfo(
    MapProviderType Type,
    string DisplayName,
    bool RequiresApiKey,
    string TileUrlTemplate,
    int MinZoom,
    int MaxZoom,
    string Attribution);

public sealed class MapProviderRegistry
{
    private readonly Dictionary<MapProviderType, MapProviderInfo> _providers = [];

    public MapProviderRegistry()
    {
        foreach (var provider in CreateDefaults())
        {
            _providers[provider.Type] = provider;
        }
    }

    public void RegisterProvider(MapProviderInfo provider)
    {
        _providers[provider.Type] = provider;
    }

    public MapProviderInfo? GetProvider(MapProviderType type)
    {
        return _providers.GetValueOrDefault(type);
    }

    public IReadOnlyList<MapProviderInfo> GetAllProviders()
    {
        return _providers.Values
            .OrderBy(static p => p.DisplayName)
            .ToArray();
    }

    private static IReadOnlyList<MapProviderInfo> CreateDefaults()
    {
        return
        [
            new MapProviderInfo(
                MapProviderType.Local,
                "Local Fallback",
                RequiresApiKey: false,
                TileUrlTemplate: "",
                MinZoom: 0,
                MaxZoom: 18,
                Attribution: "Local vector renderer"),

            new MapProviderInfo(
                MapProviderType.Tianditu,
                "Tianditu",
                RequiresApiKey: true,
                TileUrlTemplate: "https://t{s}.tianditu.gov.cn/vec_w/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=vec&STYLE=default&TILEMATRIXSET=w&FORMAT=tiles&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&tk={key}",
                MinZoom: 1,
                MaxZoom: 18,
                Attribution: "Tianditu"),

            new MapProviderInfo(
                MapProviderType.Google,
                "Google Maps",
                RequiresApiKey: true,
                TileUrlTemplate: "https://mt{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}",
                MinZoom: 0,
                MaxZoom: 21,
                Attribution: "Google"),

            new MapProviderInfo(
                MapProviderType.Bing,
                "Bing Maps",
                RequiresApiKey: true,
                TileUrlTemplate: "https://ecn.t{s}.tiles.virtualearth.net/tiles/r{quadkey}.jpeg?g=1&mkt=en-US&shading=hill",
                MinZoom: 1,
                MaxZoom: 19,
                Attribution: "Microsoft"),

            new MapProviderInfo(
                MapProviderType.OpenStreetMap,
                "OpenStreetMap",
                RequiresApiKey: false,
                TileUrlTemplate: "https://tile.openstreetmap.org/{z}/{x}/{y}.png",
                MinZoom: 0,
                MaxZoom: 19,
                Attribution: "OpenStreetMap contributors"),

            new MapProviderInfo(
                MapProviderType.ArcGIS,
                "ArcGIS World Imagery",
                RequiresApiKey: false,
                TileUrlTemplate: "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
                MinZoom: 0,
                MaxZoom: 19,
                Attribution: "Esri"),

            new MapProviderInfo(
                MapProviderType.Mapbox,
                "Mapbox",
                RequiresApiKey: true,
                TileUrlTemplate: "https://api.mapbox.com/styles/v1/mapbox/streets-v12/tiles/{z}/{x}/{y}?access_token={key}",
                MinZoom: 0,
                MaxZoom: 22,
                Attribution: "Mapbox"),

            new MapProviderInfo(
                MapProviderType.Custom,
                "Custom",
                RequiresApiKey: false,
                TileUrlTemplate: "",
                MinZoom: 0,
                MaxZoom: 22,
                Attribution: "")
        ];
    }
}
