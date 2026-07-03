namespace VGC.Vehicles;

public sealed record ProximitySector(
    double AngleDeg,
    double DistanceM,
    double MinDistanceM,
    double MaxDistanceM);

public sealed record ProximityRadarSnapshot(
    IReadOnlyList<ProximitySector> Sectors,
    double MinDistanceM,
    bool HasData,
    string StatusText);

public sealed class ProximityRadarRuntime
{
    private const int SectorCount = 72;
    private const double SectorWidthDeg = 5.0;

    private readonly double[] _distances = new double[SectorCount];
    private readonly double[] _minDistances = new double[SectorCount];
    private readonly double[] _maxDistances = new double[SectorCount];
    private bool _hasData;

    public ProximityRadarRuntime()
    {
        for (var i = 0; i < SectorCount; i++)
        {
            _minDistances[i] = 0.2;
            _maxDistances[i] = 60.0;
        }
    }

    public void UpdateFromObstacleDistance(double angleMinDeg, double angleMaxDeg, IReadOnlyList<double> distances)
    {
        if (distances.Count == 0)
        {
            return;
        }

        var normalizedMin = NormalizeAngle(angleMinDeg);
        var normalizedMax = NormalizeAngle(angleMaxDeg);

        if (distances.Count == 1)
        {
            var centerAngle = normalizedMin;
            if (normalizedMax > normalizedMin)
            {
                centerAngle = (normalizedMin + normalizedMax) / 2.0;
            }

            var sectorIndex = AngleToSectorIndex(centerAngle);
            _distances[sectorIndex] = distances[0];
            _hasData = true;
            return;
        }

        var totalArc = normalizedMax > normalizedMin
            ? normalizedMax - normalizedMin
            : 360.0 - normalizedMin + normalizedMax;

        var step = totalArc / distances.Count;

        for (var i = 0; i < distances.Count; i++)
        {
            var angle = NormalizeAngle(normalizedMin + i * step);
            var sectorIndex = AngleToSectorIndex(angle);
            _distances[sectorIndex] = distances[i];
        }

        _hasData = true;
    }

    public void Clear()
    {
        Array.Clear(_distances);
        _hasData = false;
    }

    public ProximityRadarSnapshot Snapshot
    {
        get
        {
            var sectors = new ProximitySector[SectorCount];
            var minDistance = double.MaxValue;
            var anyReading = false;

            for (var i = 0; i < SectorCount; i++)
            {
                var angle = i * SectorWidthDeg;
                sectors[i] = new ProximitySector(angle, _distances[i], _minDistances[i], _maxDistances[i]);

                if (_distances[i] > 0)
                {
                    anyReading = true;
                    if (_distances[i] < minDistance)
                    {
                        minDistance = _distances[i];
                    }
                }
            }

            if (!anyReading)
            {
                minDistance = 0;
            }

            var statusText = _hasData
                ? $"Proximity active, min distance: {minDistance:F1} m"
                : "No proximity data";

            return new ProximityRadarSnapshot(sectors, minDistance, _hasData, statusText);
        }
    }

    private static int AngleToSectorIndex(double angleDeg)
    {
        var index = (int)(angleDeg / SectorWidthDeg) % SectorCount;
        return index < 0 ? index + SectorCount : index;
    }

    private static double NormalizeAngle(double angleDeg)
    {
        var result = angleDeg % 360.0;
        return result < 0 ? result + 360.0 : result;
    }
}
