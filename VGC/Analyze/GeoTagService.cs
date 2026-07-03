namespace VGC.Analyze;

public sealed record GeoTagResult(
    string ImagePath,
    double? Latitude,
    double? Longitude,
    double? AltitudeMeters,
    DateTimeOffset? MatchedAt,
    bool IsTagged);

public sealed class GeoTagService
{
    public async Task<IReadOnlyList<GeoTagResult>> TagImagesAsync(
        IEnumerable<string> imagePaths,
        IReadOnlyList<(DateTimeOffset Timestamp, double Latitude, double Longitude, double Altitude)> track,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GeoTagResult>();
        var sortedTrack = track.OrderBy(t => t.Timestamp).ToList();

        foreach (var path in imagePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = FindClosestTrackPoint(sortedTrack, DateTimeOffset.UtcNow);
            results.Add(new GeoTagResult(
                path,
                match?.Latitude,
                match?.Longitude,
                match?.Altitude,
                match?.Timestamp,
                match is not null));
        }

        return results;
    }

    private static (DateTimeOffset Timestamp, double Latitude, double Longitude, double Altitude)? FindClosestTrackPoint(
        IReadOnlyList<(DateTimeOffset, double, double, double)> track,
        DateTimeOffset target)
    {
        if (track.Count == 0) return null;

        var closest = track[0];
        var minDiff = Math.Abs((target - closest.Item1).TotalSeconds);
        for (var i = 1; i < track.Count; i++)
        {
            var diff = Math.Abs((target - track[i].Item1).TotalSeconds);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = track[i];
            }
        }

        return closest;
    }
}
