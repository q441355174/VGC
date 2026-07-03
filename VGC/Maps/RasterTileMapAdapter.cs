using System.Net.Http;

namespace VGC.Maps;

public sealed class RasterTileMapAdapter : IMapAdapter, IMapProviderAdapter, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MapProviderDescriptor _descriptor;
    private MapViewport _viewport = new(new MapCoordinate(0, 0), 3);
    private readonly Dictionary<string, MapTileCacheEntry?> _tileCache = [];

    public RasterTileMapAdapter(MapProviderDescriptor descriptor, HttpClient? httpClient = null)
    {
        _descriptor = descriptor;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VGC/1.0");
    }

    public MapProviderDescriptor Descriptor => _descriptor;

    public Task SetViewportAsync(MapViewport viewport, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _viewport = viewport;
        return Task.CompletedTask;
    }

    public Task ApplyOverlaysAsync(MapOverlayFrame overlays, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

    public async Task<byte[]?> FetchTileAsync(string layerId, int z, int x, int y, CancellationToken cancellationToken = default)
    {
        var layer = _descriptor.TileLayers.FirstOrDefault(l => l.Id == layerId);
        if (layer?.Template is not { } template)
        {
            return null;
        }

        var url = template
            .Replace("{z}", z.ToString())
            .Replace("{x}", x.ToString())
            .Replace("{y}", y.ToString())
            .Replace("{s}", "a");

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _tileCache.Clear();
    }
}
