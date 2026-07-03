namespace VGC.Mission;

public sealed class PlanTerrainService
{
    private readonly Terrain.ITerrainService _terrainService;

    public PlanTerrainService(Terrain.ITerrainService terrainService)
    {
        _terrainService = terrainService;
    }

    public async Task<IReadOnlyList<double>> GetWaypointElevationsAsync(
        MissionPlan mission,
        CancellationToken cancellationToken = default)
    {
        var coords = mission.Items
            .Where(item => item.HasCoordinate)
            .Select(item => new Terrain.TerrainCoordinate(item.CoordinateLat, item.CoordinateLon))
            .ToList();

        if (coords.Count == 0)
        {
            return [];
        }

        var result = await _terrainService.QueryAsync(coords, cancellationToken).ConfigureAwait(false);
        return result.IsComplete
            ? result.Samples.Select(s => s.ElevationMeters).ToList()
            : [];
    }
}
