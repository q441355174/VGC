using System.Net.Http;

namespace VGC.Maps;

public sealed class TiandituRasterAdapter : IMapAdapter, IMapProviderAdapter, IMapRasterTileSource, IDisposable
{
    private readonly TiandituApiKeyService _apiKeyService;
    private readonly TiandituProviderAdapter _urlProvider;
    private readonly HttpClient _httpClient;
    private MapViewport _viewport = new(new MapCoordinate(39.9042, 116.4074), 5);

    public TiandituRasterAdapter(TiandituApiKeyService apiKeyService, HttpClient? httpClient = null)
    {
        _apiKeyService = apiKeyService;
        _urlProvider = new TiandituProviderAdapter(apiKeyService);
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VGC/1.0");
    }

    public MapProviderDescriptor Descriptor => _urlProvider.Descriptor;

    public bool IsAvailable => _apiKeyService.HasKey;

    public Task SetViewportAsync(MapViewport viewport, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _viewport = viewport;
        return Task.CompletedTask;
    }

    public Task ApplyOverlaysAsync(MapOverlayFrame overlays, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ApplyCameraAsync(MapProviderCameraState camera, CancellationToken cancellationToken = default)
    {
        return SetViewportAsync(camera.ToViewport(), cancellationToken);
    }

    public Task<MapProviderCameraState> GetCameraAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new MapProviderCameraState(_viewport.Center, _viewport.ZoomLevel));
    }

    public Task<byte[]?> FetchVectorTileAsync(int z, int x, int y, CancellationToken cancellationToken = default)
    {
        return FetchTileAsync("tianditu-vector", z, x, y, cancellationToken);
    }

    public Task<byte[]?> FetchLabelTileAsync(int z, int x, int y, CancellationToken cancellationToken = default)
    {
        return FetchTileAsync("tianditu-vector-label", z, x, y, cancellationToken);
    }

    public async Task<byte[]?> FetchTileAsync(string layerId, int z, int x, int y, CancellationToken cancellationToken = default)
    {
        var url = GetTileUrl(layerId, z, x, y);
        if (url is null)
        {
            return null;
        }

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private string? GetTileUrl(string layerId, int z, int x, int y)
    {
        if (!_apiKeyService.HasKey)
        {
            return null;
        }

        var layer = Descriptor.TileLayers.FirstOrDefault(layer => layer.Id == layerId);
        return layer is null
            ? null
            : MapTileUrlBuilder.Build(layer, z, x, y, _ => _apiKeyService.GetKey());
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
