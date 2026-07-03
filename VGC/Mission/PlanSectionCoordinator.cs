namespace VGC.Mission;

public sealed class PlanSectionCoordinator
{
    private readonly PlanJsonService _jsonService;

    public PlanSectionCoordinator(PlanDocument? document = null, PlanJsonService? jsonService = null)
    {
        Document = document ?? PlanDocument.CreateBlank();
        _jsonService = jsonService ?? new PlanJsonService();
    }

    public PlanDocument Document { get; private set; }

    public MissionPlan Mission => Document.Mission;

    public GeoFencePlan GeoFence => Document.GeoFence;

    public RallyPointsPlan RallyPoints => Document.RallyPoints;

    public bool HasMissionItems => Mission.Items.Count > 0;

    public bool HasGeoFenceItems => GeoFence.Polygons.Count > 0 || GeoFence.Circles.Count > 0 || GeoFence.BreachReturn is not null;

    public bool HasRallyPoints => RallyPoints.Points.Count > 0;

    public PlanSectionValidationResult ValidateSections()
    {
        var errors = new List<string>();

        var geoFence = GeoFenceValidation.Validate(GeoFence);
        if (!geoFence.IsValid)
        {
            errors.AddRange(geoFence.Errors.Select(static error => $"GeoFence: {error}"));
        }

        var rallyPoints = RallyPointsValidation.Validate(RallyPoints);
        if (!rallyPoints.IsValid)
        {
            errors.AddRange(rallyPoints.Errors.Select(static error => $"Rally: {error}"));
        }

        return errors.Count == 0
            ? PlanSectionValidationResult.Success
            : new PlanSectionValidationResult(false, errors);
    }

    public string Save()
    {
        return _jsonService.Serialize(Document);
    }

    public void Load(string json)
    {
        Document = _jsonService.Deserialize(json);
    }
}

public sealed record PlanSectionValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static PlanSectionValidationResult Success { get; } = new(true, []);
}
