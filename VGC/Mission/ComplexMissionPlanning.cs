using System.Text.Json.Serialization;

namespace VGC.Mission;

public enum ComplexMissionItemKind
{
    Survey,
    Corridor,
    StructureScan
}

public sealed class ComplexMissionPlan
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("surveyItems")]
    public List<SurveyMissionItem> SurveyItems { get; set; } = [];

    [JsonPropertyName("corridorItems")]
    public List<CorridorMissionItem> CorridorItems { get; set; } = [];

    [JsonPropertyName("structureScanItems")]
    public List<StructureScanMissionItem> StructureScanItems { get; set; } = [];
}

public sealed class SurveyMissionItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ComplexItem";

    [JsonPropertyName("complexItemType")]
    public string ComplexItemType { get; set; } = "survey";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("polygon")]
    public List<PlanCoordinate> Polygon { get; set; } = [];

    [JsonPropertyName("gridAngle")]
    public double GridAngle { get; set; }

    [JsonPropertyName("transectSpacing")]
    public double TransectSpacing { get; set; } = 25;

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; } = 50;
}

public sealed class CorridorMissionItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ComplexItem";

    [JsonPropertyName("complexItemType")]
    public string ComplexItemType { get; set; } = "corridorScan";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("polyline")]
    public List<PlanCoordinate> Polyline { get; set; } = [];

    [JsonPropertyName("corridorWidth")]
    public double CorridorWidth { get; set; } = 50;

    [JsonPropertyName("transectSpacing")]
    public double TransectSpacing { get; set; } = 25;

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; } = 50;
}

public sealed class StructureScanMissionItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "ComplexItem";

    [JsonPropertyName("complexItemType")]
    public string ComplexItemType { get; set; } = "structureScan";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("footprint")]
    public List<PlanCoordinate> Footprint { get; set; } = [];

    [JsonPropertyName("scanDistance")]
    public double ScanDistance { get; set; } = 20;

    [JsonPropertyName("layerHeight")]
    public double LayerHeight { get; set; } = 10;

    [JsonPropertyName("altitude")]
    public double Altitude { get; set; } = 50;
}

public sealed record ComplexMissionCalculationRequest(
    ComplexMissionItemKind Kind,
    IReadOnlyList<PlanCoordinate> Geometry,
    double? SpacingMeters = null,
    double? AltitudeMeters = null,
    double? AngleDegrees = null,
    double? CorridorWidthMeters = null,
    double? LayerHeightMeters = null,
    double? ScanDistanceMeters = null)
{
    public static ComplexMissionCalculationRequest FromSurvey(SurveyMissionItem item)
    {
        return new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.Survey,
            item.Polygon,
            SpacingMeters: item.TransectSpacing,
            AltitudeMeters: item.Altitude,
            AngleDegrees: item.GridAngle);
    }

    public static ComplexMissionCalculationRequest FromCorridor(CorridorMissionItem item)
    {
        return new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.Corridor,
            item.Polyline,
            SpacingMeters: item.TransectSpacing,
            AltitudeMeters: item.Altitude,
            CorridorWidthMeters: item.CorridorWidth);
    }

    public static ComplexMissionCalculationRequest FromStructureScan(StructureScanMissionItem item)
    {
        return new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.StructureScan,
            item.Footprint,
            AltitudeMeters: item.Altitude,
            LayerHeightMeters: item.LayerHeight,
            ScanDistanceMeters: item.ScanDistance);
    }
}

public sealed record ComplexMissionPreviewSegment(PlanCoordinate Start, PlanCoordinate End);

public sealed record ComplexMissionCalculationResult(
    bool IsValid,
    string? Error,
    IReadOnlyList<ComplexMissionPreviewSegment> PreviewSegments,
    int SourcePointCount)
{
    public static ComplexMissionCalculationResult Invalid(string error)
    {
        return new ComplexMissionCalculationResult(false, error, [], 0);
    }
}

public interface IComplexMissionCalculator
{
    ComplexMissionCalculationResult Calculate(ComplexMissionCalculationRequest request);
}

public sealed class BasicComplexMissionCalculator : IComplexMissionCalculator
{
    private const double MetersPerDegree = 111_320.0;
    private const int MaxGeneratedSegments = 256;

    public ComplexMissionCalculationResult Calculate(ComplexMissionCalculationRequest request)
    {
        if (request.Geometry.Count == 0)
        {
            return ComplexMissionCalculationResult.Invalid("Complex mission geometry is empty.");
        }

        if (request.Geometry.Any(static coordinate => !coordinate.IsValid2D()))
        {
            return ComplexMissionCalculationResult.Invalid("Complex mission geometry contains an invalid coordinate.");
        }

        if (request.SpacingMeters is { } spacing && spacing <= 0)
        {
            return ComplexMissionCalculationResult.Invalid("Complex mission spacing must be greater than zero.");
        }

        if (request.AltitudeMeters is { } altitude && (!IsFinite(altitude) || altitude < 0))
        {
            return ComplexMissionCalculationResult.Invalid("Complex mission altitude must be a finite non-negative value.");
        }

        if (request.CorridorWidthMeters is { } corridorWidth && corridorWidth <= 0)
        {
            return ComplexMissionCalculationResult.Invalid("Corridor width must be greater than zero.");
        }

        if (request.LayerHeightMeters is { } layerHeight && layerHeight <= 0)
        {
            return ComplexMissionCalculationResult.Invalid("Structure scan layer height must be greater than zero.");
        }

        if (request.ScanDistanceMeters is { } scanDistance && scanDistance <= 0)
        {
            return ComplexMissionCalculationResult.Invalid("Structure scan distance must be greater than zero.");
        }

        return request.Kind switch
        {
            ComplexMissionItemKind.Corridor => CalculateCorridor(request),
            ComplexMissionItemKind.Survey => CalculateSurvey(request),
            ComplexMissionItemKind.StructureScan => CalculateStructureScan(request),
            _ => ComplexMissionCalculationResult.Invalid("Complex mission kind is not supported.")
        };
    }

    private static ComplexMissionCalculationResult CalculateCorridor(ComplexMissionCalculationRequest request)
    {
        var geometry = request.Geometry;
        if (geometry.Count < 2)
        {
            return ComplexMissionCalculationResult.Invalid("Corridor planning requires at least two path points.");
        }

        var segments = new List<ComplexMissionPreviewSegment>();
        AddOpenPath(segments, geometry, request.AltitudeMeters);

        if (request.CorridorWidthMeters is { } width)
        {
            var halfWidthDegrees = width / 2.0 / MetersPerDegree;
            AddOpenPath(segments, OffsetLatitude(geometry, halfWidthDegrees), request.AltitudeMeters);
            AddOpenPath(segments, OffsetLatitude(geometry, -halfWidthDegrees), request.AltitudeMeters);
        }

        return new ComplexMissionCalculationResult(true, null, segments, geometry.Count);
    }

    private static ComplexMissionCalculationResult CalculateSurvey(ComplexMissionCalculationRequest request)
    {
        var geometry = request.Geometry;
        if (geometry.Count < 3)
        {
            return ComplexMissionCalculationResult.Invalid("Survey planning requires at least three boundary points.");
        }

        if (request.SpacingMeters is not { } spacing)
        {
            return CalculateClosedBoundary(geometry, "Survey", request.AltitudeMeters);
        }

        var segments = new List<ComplexMissionPreviewSegment>();
        var minLat = geometry.Min(static point => point.Latitude);
        var maxLat = geometry.Max(static point => point.Latitude);
        var minLon = geometry.Min(static point => point.Longitude);
        var maxLon = geometry.Max(static point => point.Longitude);
        var spacingDegrees = spacing / MetersPerDegree;
        var lineCount = Math.Clamp((int)Math.Floor((maxLat - minLat) / spacingDegrees) + 1, 1, MaxGeneratedSegments);

        for (var index = 0; index < lineCount; index++)
        {
            var latitude = Math.Min(maxLat, minLat + index * spacingDegrees);
            var start = new PlanCoordinate(latitude, index % 2 == 0 ? minLon : maxLon, request.AltitudeMeters);
            var end = new PlanCoordinate(latitude, index % 2 == 0 ? maxLon : minLon, request.AltitudeMeters);
            segments.Add(new ComplexMissionPreviewSegment(start, end));
        }

        return new ComplexMissionCalculationResult(true, null, segments, geometry.Count);
    }

    private static ComplexMissionCalculationResult CalculateStructureScan(ComplexMissionCalculationRequest request)
    {
        var geometry = request.Geometry;
        if (geometry.Count < 3)
        {
            return ComplexMissionCalculationResult.Invalid("Structure scan planning requires at least three boundary points.");
        }

        if (request.LayerHeightMeters is not { } layerHeight || request.AltitudeMeters is not { } altitude)
        {
            return CalculateClosedBoundary(geometry, "Structure scan", request.AltitudeMeters);
        }

        var segments = new List<ComplexMissionPreviewSegment>();
        var layerCount = Math.Clamp((int)Math.Ceiling(altitude / layerHeight), 1, 32);
        for (var layer = 1; layer <= layerCount && segments.Count < MaxGeneratedSegments; layer++)
        {
            var layerAltitude = Math.Min(altitude, layer * layerHeight);
            AddClosedBoundary(segments, geometry, layerAltitude);
        }

        return new ComplexMissionCalculationResult(true, null, segments, geometry.Count);
    }

    private static ComplexMissionCalculationResult CalculateClosedBoundary(IReadOnlyList<PlanCoordinate> geometry, string label, double? altitude)
    {
        if (geometry.Count < 3)
        {
            return ComplexMissionCalculationResult.Invalid($"{label} planning requires at least three boundary points.");
        }

        var segments = new List<ComplexMissionPreviewSegment>();
        AddClosedBoundary(segments, geometry, altitude);
        return new ComplexMissionCalculationResult(true, null, segments, geometry.Count);
    }

    private static void AddOpenPath(List<ComplexMissionPreviewSegment> segments, IReadOnlyList<PlanCoordinate> geometry, double? altitude)
    {
        for (var i = 0; i < geometry.Count - 1 && segments.Count < MaxGeneratedSegments; i++)
        {
            segments.Add(new ComplexMissionPreviewSegment(
                WithAltitude(geometry[i], altitude),
                WithAltitude(geometry[i + 1], altitude)));
        }
    }

    private static void AddClosedBoundary(List<ComplexMissionPreviewSegment> segments, IReadOnlyList<PlanCoordinate> geometry, double? altitude)
    {
        for (var i = 0; i < geometry.Count && segments.Count < MaxGeneratedSegments; i++)
        {
            segments.Add(new ComplexMissionPreviewSegment(
                WithAltitude(geometry[i], altitude),
                WithAltitude(geometry[(i + 1) % geometry.Count], altitude)));
        }
    }

    private static IReadOnlyList<PlanCoordinate> OffsetLatitude(IReadOnlyList<PlanCoordinate> geometry, double latitudeOffset)
    {
        return geometry
            .Select(point => new PlanCoordinate(point.Latitude + latitudeOffset, point.Longitude, point.Altitude))
            .ToArray();
    }

    private static PlanCoordinate WithAltitude(PlanCoordinate coordinate, double? altitude)
    {
        return altitude is { } value
            ? new PlanCoordinate(coordinate.Latitude, coordinate.Longitude, value)
            : coordinate;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
