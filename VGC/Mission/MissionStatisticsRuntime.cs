namespace VGC.Mission;

public sealed record MissionStatistics(
    double TotalDistanceM,
    double EstimatedFlightTimeSec,
    int WaypointCount,
    double MaxAltitudeM,
    double CruiseSpeedMs,
    double BatteryEstimatePercent,
    string StatusText);

public sealed class MissionStatisticsRuntime
{
    private const double EarthRadiusM = 6_371_000.0;

    public MissionStatistics Calculate(MissionPlan plan, double cruiseSpeed, double batteryCapacityMah)
    {
        if (cruiseSpeed <= 0)
        {
            return new MissionStatistics(0, 0, 0, 0, cruiseSpeed, 100, "Invalid cruise speed.");
        }

        var waypoints = plan.Items
            .Where(static item => item.HasCoordinate)
            .ToList();

        if (waypoints.Count == 0)
        {
            return new MissionStatistics(0, 0, 0, 0, cruiseSpeed, 100, "No waypoints in mission.");
        }

        var totalDistance = 0.0;
        var maxAltitude = 0.0;

        for (var i = 0; i < waypoints.Count; i++)
        {
            var alt = waypoints[i].CoordinateAlt;
            if (alt > maxAltitude)
            {
                maxAltitude = alt;
            }

            if (i > 0)
            {
                totalDistance += HaversineDistanceM(
                    waypoints[i - 1].CoordinateLat, waypoints[i - 1].CoordinateLon,
                    waypoints[i].CoordinateLat, waypoints[i].CoordinateLon);
            }
        }

        var estimatedFlightTimeSec = totalDistance / cruiseSpeed;

        var batteryEstimate = 100.0;
        if (batteryCapacityMah > 0)
        {
            // Rough estimate: assume ~20A average current draw for a typical multirotor
            const double averageCurrentA = 20.0;
            var flightTimeHours = estimatedFlightTimeSec / 3600.0;
            var consumedMah = averageCurrentA * 1000.0 * flightTimeHours;
            batteryEstimate = Math.Max(0, 100.0 * (1.0 - consumedMah / batteryCapacityMah));
        }

        var statusText = $"{waypoints.Count} waypoints, {totalDistance:F0} m, ~{estimatedFlightTimeSec:F0} s flight time";

        return new MissionStatistics(
            totalDistance,
            estimatedFlightTimeSec,
            waypoints.Count,
            maxAltitude,
            cruiseSpeed,
            Math.Round(batteryEstimate, 1),
            statusText);
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
