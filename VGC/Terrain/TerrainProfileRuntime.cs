using VGC.Maps;

namespace VGC.Terrain;

public sealed record TerrainProfilePoint(
    double DistanceFromStartM,
    double ElevationM,
    double Latitude,
    double Longitude);

public sealed record TerrainProfile(
    IReadOnlyList<TerrainProfilePoint> Points,
    double MinElevationM,
    double MaxElevationM,
    double TotalDistanceM);

public sealed class TerrainProfileRuntime
{
    private const double EarthRadiusM = 6_371_000.0;

    public TerrainProfile GenerateProfile(IReadOnlyList<MapCoordinate> coordinates)
    {
        if (coordinates.Count == 0)
        {
            return new TerrainProfile([], 0, 0, 0);
        }

        var points = new List<TerrainProfilePoint>();
        var cumulativeDistance = 0.0;

        for (var i = 0; i < coordinates.Count; i++)
        {
            if (i > 0)
            {
                cumulativeDistance += HaversineDistanceM(
                    coordinates[i - 1].Latitude, coordinates[i - 1].Longitude,
                    coordinates[i].Latitude, coordinates[i].Longitude);
            }

            // Flat profile: elevation defaults to 0 until a TerrainProvider is injected
            var elevation = 0.0;

            points.Add(new TerrainProfilePoint(
                cumulativeDistance,
                elevation,
                coordinates[i].Latitude,
                coordinates[i].Longitude));
        }

        var minElevation = points.Count > 0 ? points.Min(static p => p.ElevationM) : 0;
        var maxElevation = points.Count > 0 ? points.Max(static p => p.ElevationM) : 0;

        return new TerrainProfile(points, minElevation, maxElevation, cumulativeDistance);
    }

    private static double HaversineDistanceM(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2))
              * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusM * c;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
