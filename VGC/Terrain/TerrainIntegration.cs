using System.Globalization;
using VGC.Mission;

namespace VGC.Terrain;

public sealed record TerrainCoordinate(double Latitude, double Longitude)
{
    public bool IsValid()
    {
        return IsFinite(Latitude)
            && IsFinite(Longitude)
            && Latitude is >= -90 and <= 90
            && Longitude is >= -180 and <= 180;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

public sealed record TerrainSample(
    TerrainCoordinate Coordinate,
    double ElevationMeters,
    string Source = "unknown");

public sealed record TerrainQueryResult(
    IReadOnlyList<TerrainSample> Samples,
    IReadOnlyList<TerrainCoordinate> MissingCoordinates)
{
    public bool IsComplete => MissingCoordinates.Count == 0;
}

public interface ITerrainQueryProvider
{
    Task<IReadOnlyList<TerrainSample>> QueryAsync(
        IReadOnlyList<TerrainCoordinate> coordinates,
        CancellationToken cancellationToken = default);
}

public interface ITerrainCache
{
    bool TryGet(TerrainCoordinate coordinate, out TerrainSample sample);

    void Store(TerrainSample sample);
}

public interface ITerrainService
{
    Task<TerrainQueryResult> QueryAsync(
        IReadOnlyList<TerrainCoordinate> coordinates,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryTerrainCache : ITerrainCache
{
    private readonly Dictionary<string, TerrainSample> _samples = [];

    public bool TryGet(TerrainCoordinate coordinate, out TerrainSample sample)
    {
        return _samples.TryGetValue(CreateKey(coordinate), out sample!);
    }

    public void Store(TerrainSample sample)
    {
        _samples[CreateKey(sample.Coordinate)] = sample;
    }

    private static string CreateKey(TerrainCoordinate coordinate)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{coordinate.Latitude:F7}:{coordinate.Longitude:F7}");
    }
}

public sealed class TerrainService : ITerrainService
{
    private readonly ITerrainQueryProvider _provider;
    private readonly ITerrainCache _cache;

    public TerrainService(ITerrainQueryProvider provider, ITerrainCache cache)
    {
        _provider = provider;
        _cache = cache;
    }

    public async Task<TerrainQueryResult> QueryAsync(
        IReadOnlyList<TerrainCoordinate> coordinates,
        CancellationToken cancellationToken = default)
    {
        var missing = new List<TerrainCoordinate>();

        foreach (var coordinate in coordinates)
        {
            if (!coordinate.IsValid())
            {
                missing.Add(coordinate);
                continue;
            }

            if (!_cache.TryGet(coordinate, out _))
            {
                missing.Add(coordinate);
            }
        }

        if (missing.Count == 0)
        {
            return new TerrainQueryResult(GetSamplesInInputOrder(coordinates), []);
        }

        var providerCoordinates = missing.Where(static coordinate => coordinate.IsValid()).ToList();
        if (providerCoordinates.Count > 0)
        {
            var providerSamples = await _provider.QueryAsync(providerCoordinates, cancellationToken).ConfigureAwait(false);
            foreach (var sample in providerSamples)
            {
                if (!sample.Coordinate.IsValid())
                {
                    continue;
                }

                _cache.Store(sample);
            }
        }

        var unresolved = missing
            .Where(coordinate => !_cache.TryGet(coordinate, out _))
            .ToList();
        return new TerrainQueryResult(GetSamplesInInputOrder(coordinates), unresolved);
    }

    private IReadOnlyList<TerrainSample> GetSamplesInInputOrder(IReadOnlyList<TerrainCoordinate> coordinates)
    {
        return coordinates
            .Where(coordinate => _cache.TryGet(coordinate, out _))
            .Select(coordinate =>
            {
                _cache.TryGet(coordinate, out var sample);
                return sample;
            })
            .ToList();
    }
}

public sealed record MissionTerrainAltitudeRequest(
    IReadOnlyList<PlanCoordinate> Coordinates,
    double ClearanceMeters);

public sealed record TerrainAdjustedCoordinate(
    PlanCoordinate Source,
    double TerrainElevationMeters,
    double PlannedAltitudeMeters);

public sealed class MissionTerrainAltitudePlanner
{
    private readonly ITerrainService _terrainService;

    public MissionTerrainAltitudePlanner(ITerrainService terrainService)
    {
        _terrainService = terrainService;
    }

    public async Task<IReadOnlyList<TerrainAdjustedCoordinate>> PlanAsync(
        MissionTerrainAltitudeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClearanceMeters < 0 || double.IsNaN(request.ClearanceMeters) || double.IsInfinity(request.ClearanceMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Terrain clearance must be a finite non-negative value.");
        }

        var terrainCoordinates = request.Coordinates
            .Select(static coordinate => new TerrainCoordinate(coordinate.Latitude, coordinate.Longitude))
            .ToList();
        var terrain = await _terrainService.QueryAsync(terrainCoordinates, cancellationToken).ConfigureAwait(false);
        if (!terrain.IsComplete)
        {
            throw new InvalidOperationException("Terrain data is incomplete for mission altitude planning.");
        }

        return request.Coordinates
            .Zip(terrain.Samples, (source, sample) => new TerrainAdjustedCoordinate(
                source,
                sample.ElevationMeters,
                sample.ElevationMeters + request.ClearanceMeters))
            .ToList();
    }
}

public sealed record MissionTerrainPreviewRequest(
    IReadOnlyList<PlanCoordinate> Coordinates,
    double ClearanceMeters,
    double FallbackTerrainElevationMeters = 0);

public sealed record TerrainPreviewAltitude(
    PlanCoordinate Source,
    double? TerrainElevationMeters,
    double PlannedAltitudeMeters,
    bool HasTerrainData,
    string Annotation);

public sealed class MissionTerrainPreviewService
{
    private readonly ITerrainService _terrainService;

    public MissionTerrainPreviewService(ITerrainService terrainService)
    {
        _terrainService = terrainService;
    }

    public async Task<IReadOnlyList<TerrainPreviewAltitude>> PreviewAsync(
        MissionTerrainPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClearanceMeters < 0 || double.IsNaN(request.ClearanceMeters) || double.IsInfinity(request.ClearanceMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Terrain clearance must be a finite non-negative value.");
        }

        if (double.IsNaN(request.FallbackTerrainElevationMeters) || double.IsInfinity(request.FallbackTerrainElevationMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Fallback terrain elevation must be finite.");
        }

        var terrainCoordinates = request.Coordinates
            .Select(static coordinate => new TerrainCoordinate(coordinate.Latitude, coordinate.Longitude))
            .ToList();
        var terrain = await _terrainService.QueryAsync(terrainCoordinates, cancellationToken).ConfigureAwait(false);
        var samples = terrain.Samples.ToDictionary(static sample => sample.Coordinate, static sample => sample);
        var preview = new List<TerrainPreviewAltitude>();

        foreach (var source in request.Coordinates)
        {
            var terrainCoordinate = new TerrainCoordinate(source.Latitude, source.Longitude);
            if (samples.TryGetValue(terrainCoordinate, out var sample))
            {
                preview.Add(new TerrainPreviewAltitude(
                    source,
                    sample.ElevationMeters,
                    sample.ElevationMeters + request.ClearanceMeters,
                    HasTerrainData: true,
                    Annotation: $"Terrain {sample.Source} + {request.ClearanceMeters:0.##} m clearance"));
                continue;
            }

            var fallbackAltitude = source.Altitude ?? request.FallbackTerrainElevationMeters + request.ClearanceMeters;
            preview.Add(new TerrainPreviewAltitude(
                source,
                null,
                fallbackAltitude,
                HasTerrainData: false,
                Annotation: source.Altitude is null
                    ? $"Terrain unavailable; using fallback {request.FallbackTerrainElevationMeters:0.##} m + {request.ClearanceMeters:0.##} m clearance"
                    : "Terrain unavailable; using source altitude"));
        }

        return preview;
    }
}
