using VGC.Mission;

namespace VGC.Maps;

public sealed class TiandituProviderAdapter : IMapProviderAdapter
{
    private readonly TiandituApiKeyService _apiKeyService;
    private readonly MapProviderDescriptor _baseDescriptor;
    private MapProviderCameraState? _camera;
    private MapOverlayFrame _overlays = new(null, null, null);

    public TiandituProviderAdapter(TiandituApiKeyService apiKeyService)
    {
        _apiKeyService = apiKeyService;
        _baseDescriptor = MapProviderCatalog.TiandituRaster;
    }

    public MapProviderDescriptor Descriptor => _apiKeyService.HasKey
        ? _baseDescriptor
        : _baseDescriptor with
        {
            Capabilities = _baseDescriptor.Capabilities with
            {
                SupportsDesktop = false,
                SupportsAndroid = false
            }
        };

    public Task SetViewportAsync(MapViewport viewport, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _camera = MapProviderCameraState.FromViewport(viewport);
        return Task.CompletedTask;
    }

    public Task ApplyOverlaysAsync(MapOverlayFrame overlays, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _overlays = overlays;
        return Task.CompletedTask;
    }

    public Task ApplyCameraAsync(MapProviderCameraState camera, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _camera = camera;
        return Task.CompletedTask;
    }

    public Task<MapProviderCameraState> GetCameraAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_camera ?? new MapProviderCameraState(new MapCoordinate(0, 0), 1));
    }

    public string? GetTileUrl(string layerId, int z, int x, int y)
    {
        if (!_apiKeyService.HasKey)
        {
            return null;
        }

        var layer = _baseDescriptor.TileLayers.FirstOrDefault(l => l.Id == layerId);
        if (layer?.Template is not { } template)
        {
            return null;
        }

        var apiKey = _apiKeyService.GetKey()!;
        var result = template
            .Replace("{s}", GetSubdomainChar())
            .Replace("{z}", z.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{x}", x.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{y}", y.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Replace("{tk}", apiKey);
        return result;
    }

    private static string GetSubdomainChar()
    {
        return $"t{Random.Shared.Next(0, 8)}";
    }
}
