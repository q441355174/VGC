using VGC.Maps;

namespace VGC.Analyze;

public enum GeoTagMatchState
{
    Exact,
    Nearest,
    Unmatched
}

public sealed record GeoTagImageDescriptor(
    string Path,
    DateTimeOffset CaptureTime,
    long SizeBytes = 0);

public sealed record GeoTagTrackPoint(
    DateTimeOffset Timestamp,
    MapCoordinate Coordinate);

public sealed record GeoTagMatchPolicy(
    TimeSpan Tolerance,
    TimeSpan ImageTimeOffset)
{
    public static GeoTagMatchPolicy Default { get; } = new(TimeSpan.FromSeconds(2), TimeSpan.Zero);
}

public sealed record GeoTagMatchResult(
    GeoTagImageDescriptor Image,
    GeoTagMatchState State,
    DateTimeOffset AdjustedCaptureTime,
    GeoTagTrackPoint? TrackPoint,
    TimeSpan? Delta,
    string StatusText);

public sealed record GeoTagWorkflowSummary(
    int ImageCount,
    int MatchedCount,
    int UnmatchedCount,
    TimeSpan Tolerance,
    TimeSpan ImageTimeOffset,
    IReadOnlyList<GeoTagMatchResult> Results)
{
    public bool HasUnmatched => UnmatchedCount > 0;
}

public sealed class GeoTagMatcher
{
    public GeoTagWorkflowSummary Match(
        IEnumerable<GeoTagImageDescriptor> images,
        IEnumerable<GeoTagTrackPoint> trackPoints,
        GeoTagMatchPolicy? policy = null)
    {
        var effectivePolicy = policy ?? GeoTagMatchPolicy.Default;
        var orderedTrack = trackPoints
            .OrderBy(static point => point.Timestamp)
            .ToList();

        var results = images
            .OrderBy(static image => image.CaptureTime)
            .Select(image => MatchImage(image, orderedTrack, effectivePolicy))
            .ToList();

        return new GeoTagWorkflowSummary(
            results.Count,
            results.Count(static result => result.State != GeoTagMatchState.Unmatched),
            results.Count(static result => result.State == GeoTagMatchState.Unmatched),
            effectivePolicy.Tolerance,
            effectivePolicy.ImageTimeOffset,
            results);
    }

    private static GeoTagMatchResult MatchImage(
        GeoTagImageDescriptor image,
        IReadOnlyList<GeoTagTrackPoint> trackPoints,
        GeoTagMatchPolicy policy)
    {
        var adjusted = image.CaptureTime + policy.ImageTimeOffset;
        if (trackPoints.Count == 0)
        {
            return Unmatched(image, adjusted, "No log track points available");
        }

        GeoTagTrackPoint? best = null;
        TimeSpan? bestDelta = null;
        foreach (var point in trackPoints)
        {
            var delta = Abs(point.Timestamp - adjusted);
            if (bestDelta is null || delta < bestDelta.Value)
            {
                best = point;
                bestDelta = delta;
            }
        }

        if (best is null || bestDelta is null || bestDelta.Value > policy.Tolerance)
        {
            return Unmatched(image, adjusted, $"No track point within {policy.Tolerance.TotalSeconds:G}s");
        }

        var state = bestDelta.Value == TimeSpan.Zero ? GeoTagMatchState.Exact : GeoTagMatchState.Nearest;
        return new GeoTagMatchResult(
            image,
            state,
            adjusted,
            best,
            bestDelta.Value,
            state == GeoTagMatchState.Exact
                ? "Exact timestamp match"
                : $"Nearest timestamp match {bestDelta.Value.TotalSeconds:G}s");
    }

    private static GeoTagMatchResult Unmatched(GeoTagImageDescriptor image, DateTimeOffset adjusted, string status)
    {
        return new GeoTagMatchResult(image, GeoTagMatchState.Unmatched, adjusted, null, null, status);
    }

    private static TimeSpan Abs(TimeSpan value)
    {
        return value < TimeSpan.Zero ? value.Negate() : value;
    }
}
