using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using VGC.Terrain;

namespace VGC.Mission;

public abstract class ComplexMissionItemBase
{
    private string _name = "Complex Item";
    private bool _isDirty;

    protected ComplexMissionItemBase(ComplexMissionItemKind kind)
    {
        Kind = kind;
    }

    [JsonPropertyName("kind")]
    public ComplexMissionItemKind Kind { get; }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set
        {
            if (!string.Equals(_name, value, StringComparison.Ordinal))
            {
                _name = value;
                MarkDirty();
            }
        }
    }

    [JsonIgnore]
    public bool IsDirty => _isDirty;

    public void MarkClean() => _isDirty = false;

    protected void MarkDirty() => _isDirty = true;
}

public sealed class EditableComplexMissionItem : ComplexMissionItemBase
{
    private readonly List<PlanCoordinate> _geometry = [];

    public EditableComplexMissionItem(ComplexMissionItemKind kind)
        : base(kind)
    {
    }

    [JsonPropertyName("geometry")]
    public IReadOnlyList<PlanCoordinate> Geometry => _geometry;

    public void SetGeometry(IEnumerable<PlanCoordinate> geometry)
    {
        _geometry.Clear();
        _geometry.AddRange(geometry);
        MarkDirty();
    }

    public string Serialize()
    {
        return JsonSerializer.Serialize(this, new JsonSerializerOptions(JsonSerializerDefaults.General));
    }
}

public sealed record SurveyPlanningSettings(
    IReadOnlyList<PlanCoordinate> Polygon,
    double GridAngleDegrees,
    double CameraFootprintWidthMeters,
    double ForwardOverlapPercent,
    double SideOverlapPercent,
    double TurnaroundDistanceMeters,
    double AltitudeMeters);

public sealed record SurveyPlanningResult(
    IReadOnlyList<ComplexMissionPreviewSegment> Transects,
    double CameraSpacingMeters,
    double TransectSpacingMeters,
    string Summary);

public sealed class SurveyPlanningService
{
    private readonly BasicComplexMissionCalculator _calculator = new();

    public SurveyPlanningResult Plan(SurveyPlanningSettings settings)
    {
        var cameraSpacing = CalculateSpacing(settings.CameraFootprintWidthMeters, settings.ForwardOverlapPercent);
        var transectSpacing = CalculateSpacing(settings.CameraFootprintWidthMeters, settings.SideOverlapPercent);
        var result = _calculator.Calculate(new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.Survey,
            settings.Polygon,
            SpacingMeters: transectSpacing,
            AltitudeMeters: settings.AltitudeMeters,
            AngleDegrees: settings.GridAngleDegrees));

        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.Error);
        }

        var extended = result.PreviewSegments
            .Select(segment => Extend(segment, settings.TurnaroundDistanceMeters))
            .ToArray();
        return new SurveyPlanningResult(
            extended,
            cameraSpacing,
            transectSpacing,
            $"Survey transects {extended.Length}, camera spacing {cameraSpacing:0.##} m");
    }

    private static double CalculateSpacing(double footprint, double overlapPercent)
    {
        if (footprint <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(footprint), "Camera footprint must be positive.");
        }

        return footprint * (1 - Math.Clamp(overlapPercent, 0, 95) / 100.0);
    }

    private static ComplexMissionPreviewSegment Extend(ComplexMissionPreviewSegment segment, double meters)
    {
        if (meters <= 0)
        {
            return segment;
        }

        var offset = meters / 111_320.0;
        var start = new PlanCoordinate(segment.Start.Latitude, segment.Start.Longitude - offset, segment.Start.Altitude);
        var end = new PlanCoordinate(segment.End.Latitude, segment.End.Longitude + offset, segment.End.Altitude);
        return new ComplexMissionPreviewSegment(start, end);
    }
}

public sealed record CorridorScanResult(
    IReadOnlyList<PlanCoordinate> ExpandedPolygon,
    IReadOnlyList<ComplexMissionPreviewSegment> PreviewSegments,
    string Summary);

public sealed class CorridorScanPlanner
{
    private readonly BasicComplexMissionCalculator _calculator = new();

    public CorridorScanResult Plan(IReadOnlyList<PlanCoordinate> path, double widthMeters, double altitudeMeters)
    {
        if (path.Count < 2)
        {
            throw new InvalidOperationException("Corridor scan requires at least two path points.");
        }

        var offset = widthMeters / 2 / 111_320.0;
        var left = path.Select(point => new PlanCoordinate(point.Latitude + offset, point.Longitude, altitudeMeters)).ToArray();
        var right = path.Reverse().Select(point => new PlanCoordinate(point.Latitude - offset, point.Longitude, altitudeMeters)).ToArray();
        var polygon = left.Concat(right).ToArray();
        var preview = _calculator.Calculate(new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.Corridor,
            path,
            AltitudeMeters: altitudeMeters,
            CorridorWidthMeters: widthMeters));

        return new CorridorScanResult(polygon, preview.PreviewSegments, $"Corridor width {widthMeters:0.##} m, polygon points {polygon.Length}");
    }
}

public sealed record StructureScanLayer(double AltitudeMeters, IReadOnlyList<PlanCoordinate> Footprint);

public sealed record StructureScanPlan(
    IReadOnlyList<StructureScanLayer> Layers,
    IReadOnlyList<ComplexMissionPreviewSegment> PreviewSegments,
    string Summary);

public sealed class StructureScanPlanner
{
    private readonly BasicComplexMissionCalculator _calculator = new();

    public StructureScanPlan Plan(IReadOnlyList<PlanCoordinate> footprint, double maxAltitudeMeters, double layerHeightMeters, double scanDistanceMeters)
    {
        if (footprint.Count < 3)
        {
            throw new InvalidOperationException("Structure scan requires at least three footprint points.");
        }

        var layerCount = Math.Clamp((int)Math.Ceiling(maxAltitudeMeters / layerHeightMeters), 1, 64);
        var offset = scanDistanceMeters / 111_320.0;
        var layers = Enumerable.Range(1, layerCount)
            .Select(layer =>
            {
                var altitude = Math.Min(maxAltitudeMeters, layer * layerHeightMeters);
                var expanded = footprint.Select(point => new PlanCoordinate(point.Latitude + offset, point.Longitude + offset, altitude)).ToArray();
                return new StructureScanLayer(altitude, expanded);
            })
            .ToArray();
        var preview = _calculator.Calculate(new ComplexMissionCalculationRequest(
            ComplexMissionItemKind.StructureScan,
            footprint,
            AltitudeMeters: maxAltitudeMeters,
            LayerHeightMeters: layerHeightMeters,
            ScanDistanceMeters: scanDistanceMeters));
        return new StructureScanPlan(layers, preview.PreviewSegments, $"Structure layers {layers.Length}, scan distance {scanDistanceMeters:0.##} m");
    }
}

public sealed record FixedWingLandingPattern(
    PlanCoordinate Approach,
    PlanCoordinate Loiter,
    PlanCoordinate Touchdown,
    double LoiterRadiusMeters,
    string Validation);

public sealed class FixedWingLandingPlanner
{
    public FixedWingLandingPattern Create(PlanCoordinate approach, PlanCoordinate loiter, PlanCoordinate touchdown, double loiterRadiusMeters)
    {
        var validation = loiterRadiusMeters <= 0
            ? "Loiter radius must be positive."
            : !approach.IsValid3D() || !loiter.IsValid3D() || !touchdown.IsValid3D()
                ? "Landing pattern coordinates require valid altitude."
                : "Valid fixed-wing landing pattern.";
        return new FixedWingLandingPattern(approach, loiter, touchdown, loiterRadiusMeters, validation);
    }
}

public sealed record VtolLandingItem(
    PlanCoordinate TransitionPoint,
    PlanCoordinate LandingPoint,
    double TransitionAltitudeMeters,
    bool RequiresVtolVehicle,
    string BoundaryNote);

public sealed class VtolLandingPlanner
{
    public VtolLandingItem Create(PlanCoordinate transitionPoint, PlanCoordinate landingPoint, double transitionAltitudeMeters)
    {
        return new VtolLandingItem(
            transitionPoint,
            landingPoint,
            transitionAltitudeMeters,
            true,
            "VTOL landing item must be gated by VTOL vehicle type before upload.");
    }
}

public enum PlanSectionActionType
{
    CameraTrigger,
    SpeedChange
}

public sealed record PlanSectionAction(
    PlanSectionActionType Type,
    int StartIndex,
    int? EndIndex,
    double Value,
    string Units);

public sealed class CameraSpeedSectionModel
{
    private readonly List<PlanSectionAction> _actions = [];

    public IReadOnlyList<PlanSectionAction> Actions => _actions;

    public void AddCameraTrigger(int startIndex, int? endIndex, double distanceMeters)
    {
        _actions.Add(new PlanSectionAction(PlanSectionActionType.CameraTrigger, startIndex, endIndex, distanceMeters, "m"));
    }

    public void AddSpeedChange(int startIndex, double speedMetersPerSecond)
    {
        _actions.Add(new PlanSectionAction(PlanSectionActionType.SpeedChange, startIndex, null, speedMetersPerSecond, "m/s"));
    }
}

public sealed class KmlShapeService
{
    public string ExportKml(IEnumerable<PlanCoordinate> coordinates, string name = "VGC Plan Shape")
    {
        var coordinateText = string.Join(" ", coordinates.Select(static coordinate =>
            string.Create(CultureInfo.InvariantCulture, $"{coordinate.Longitude},{coordinate.Latitude},{coordinate.Altitude ?? 0}")));
        var document = new XDocument(
            new XElement("kml",
                new XElement("Document",
                    new XElement("name", name),
                    new XElement("Placemark",
                        new XElement("LineString",
                            new XElement("coordinates", coordinateText))))));
        return document.ToString(SaveOptions.DisableFormatting);
    }

    public IReadOnlyList<PlanCoordinate> ImportKml(string kml)
    {
        var document = XDocument.Parse(kml);
        var coordinates = document.Descendants("coordinates").FirstOrDefault()?.Value ?? string.Empty;
        return coordinates.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseCoordinate)
            .ToArray();
    }

    public string ExportGeoJson(IEnumerable<PlanCoordinate> coordinates)
    {
        var coords = coordinates.Select(static coordinate => new[] { coordinate.Longitude, coordinate.Latitude, coordinate.Altitude ?? 0 }).ToArray();
        return JsonSerializer.Serialize(new
        {
            type = "LineString",
            coordinates = coords
        });
    }

    private static PlanCoordinate ParseCoordinate(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            throw new FormatException("KML coordinate must contain longitude and latitude.");
        }

        var lon = double.Parse(parts[0], CultureInfo.InvariantCulture);
        var lat = double.Parse(parts[1], CultureInfo.InvariantCulture);
        double? alt = parts.Length > 2 ? double.Parse(parts[2], CultureInfo.InvariantCulture) : null;
        return new PlanCoordinate(lat, lon, alt);
    }
}

public sealed record TerrainPlanUiRow(
    int Index,
    PlanCoordinate Coordinate,
    double PlannedAltitudeMeters,
    string Annotation,
    bool HasTerrainData);

public sealed class TerrainPlanUiProjection
{
    private readonly MissionTerrainPreviewService _previewService;

    public TerrainPlanUiProjection(MissionTerrainPreviewService previewService)
    {
        _previewService = previewService;
    }

    public async Task<IReadOnlyList<TerrainPlanUiRow>> ProjectAsync(
        IReadOnlyList<PlanCoordinate> coordinates,
        double clearanceMeters,
        CancellationToken cancellationToken = default)
    {
        var preview = await _previewService.PreviewAsync(
            new MissionTerrainPreviewRequest(coordinates, clearanceMeters),
            cancellationToken).ConfigureAwait(false);
        return preview.Select((item, index) => new TerrainPlanUiRow(
            index,
            item.Source,
            item.PlannedAltitudeMeters,
            item.Annotation,
            item.HasTerrainData)).ToArray();
    }
}

public sealed record AdvancedPlanPanelState(
    string Id,
    string Title,
    bool IsAvailable,
    string Summary);

public sealed record AdvancedPlanUiState(
    IReadOnlyList<AdvancedPlanPanelState> Panels,
    IReadOnlyList<ComplexMissionPreviewSegment> PreviewSegments,
    string StatusText);

public sealed class AdvancedPlanUiProjector
{
    public AdvancedPlanUiState Project(ComplexMissionCalculationResult preview)
    {
        return new AdvancedPlanUiState(
            [
                new AdvancedPlanPanelState("survey", "Survey", true, "Grid survey planning"),
                new AdvancedPlanPanelState("corridor", "Corridor Scan", true, "Path corridor expansion"),
                new AdvancedPlanPanelState("structure", "Structure Scan", true, "Layered structure scan"),
                new AdvancedPlanPanelState("landing", "Landing", true, "Fixed-wing and VTOL landing")
            ],
            preview.PreviewSegments,
            preview.IsValid ? $"Preview segments {preview.PreviewSegments.Count}" : preview.Error ?? "Invalid preview");
    }
}

public sealed record PlanAdvancedEvidenceItem(string Name, string Target, string Verification, bool IsComplete);

public static class PlanAdvancedEvidenceCatalog
{
    public static IReadOnlyList<PlanAdvancedEvidenceItem> CreateV146Checklist()
    {
        return
        [
            new("Complex item framework", "Dirty state and serialization boundary", "VGC.Tests", true),
            new("Survey/Corridor/Structure", "Deterministic complex previews", "VGC.Tests", true),
            new("Landing", "Fixed-wing and VTOL boundary models", "VGC.Tests", true),
            new("Shape import/export", "KML and GeoJSON boundary", "VGC.Tests", true),
            new("Terrain UI", "Terrain-adjusted plan row projection", "VGC.Tests", true),
            new("Advanced UI", "Shared complex item panel state", "VGC.Tests", true)
        ];
    }
}

public sealed record PlanAdvancedParityGap(string Area, string Completed, string ResidualGap);

public static class PlanAdvancedParityAudit
{
    public static IReadOnlyList<PlanAdvancedParityGap> CreateV146Audit()
    {
        return
        [
            new("Complex Items", "Shared models and preview calculators exist.", "Full QGC setting parity remains future work."),
            new("Shape Files", "KML/GeoJSON boundary exists.", "SHP binary import remains future work."),
            new("Terrain", "Terrain UI projection exists.", "Interactive terrain UI and provider evidence remain future work."),
            new("Runtime Evidence", "Unit tests and VGC build pass.", "Desktop/Android screenshots remain deferred until AXAML wiring.")
        ];
    }
}
