using System.Text.Json;

namespace VGC.Mission;

public sealed record PlanValidationIssue(string Path, string Message);

public sealed record PlanImportExportResult(
    bool IsSuccess,
    PlanDocument? Document,
    string? Json,
    IReadOnlyList<PlanValidationIssue> Issues)
{
    public static PlanImportExportResult Success(PlanDocument? document, string? json)
    {
        return new PlanImportExportResult(true, document, json, []);
    }

    public static PlanImportExportResult Failure(IEnumerable<PlanValidationIssue> issues)
    {
        return new PlanImportExportResult(false, null, null, issues.ToArray());
    }
}

public sealed class PlanImportExportService
{
    private readonly PlanJsonService _jsonService;

    public PlanImportExportService(PlanJsonService? jsonService = null)
    {
        _jsonService = jsonService ?? new PlanJsonService();
    }

    public PlanImportExportResult Import(string json)
    {
        try
        {
            var document = _jsonService.Deserialize(json);
            var issues = Validate(document);
            return issues.Count == 0
                ? PlanImportExportResult.Success(document, null)
                : PlanImportExportResult.Failure(issues);
        }
        catch (JsonException ex)
        {
            return PlanImportExportResult.Failure([new PlanValidationIssue("$", ex.Message)]);
        }
        catch (InvalidOperationException ex)
        {
            return PlanImportExportResult.Failure([new PlanValidationIssue("$", ex.Message)]);
        }
    }

    public PlanImportExportResult Export(PlanDocument document)
    {
        var issues = Validate(document);
        if (issues.Count > 0)
        {
            return PlanImportExportResult.Failure(issues);
        }

        return PlanImportExportResult.Success(document, _jsonService.Serialize(document));
    }

    public IReadOnlyList<PlanValidationIssue> Validate(PlanDocument document)
    {
        var issues = new List<PlanValidationIssue>();

        if (!string.Equals(document.FileType, "Plan", StringComparison.Ordinal))
        {
            issues.Add(new PlanValidationIssue("$.fileType", "Plan fileType must be 'Plan'."));
        }

        if (document.Version != 1)
        {
            issues.Add(new PlanValidationIssue("$.version", "Plan file version must be 1."));
        }

        ValidateMission(document.Mission, issues);
        AddSectionIssues("$.geoFence", GeoFenceValidation.Validate(document.GeoFence).Errors, issues);
        AddSectionIssues("$.rallyPoints", RallyPointsValidation.Validate(document.RallyPoints).Errors, issues);

        return issues;
    }

    private static void ValidateMission(MissionPlan mission, List<PlanValidationIssue> issues)
    {
        if (mission.Version != 2)
        {
            issues.Add(new PlanValidationIssue("$.mission.version", "Mission version must be 2."));
        }

        if (mission.PlannedHomePosition.Length < 3 || mission.PlannedHomePosition.Any(static value => !IsFinite(value)))
        {
            issues.Add(new PlanValidationIssue("$.mission.plannedHomePosition", "Planned home position must contain three finite numeric values."));
        }

        for (var itemIndex = 0; itemIndex < mission.Items.Count; itemIndex++)
        {
            var item = mission.Items[itemIndex];
            var path = $"$.mission.items[{itemIndex}]";
            if (!string.Equals(item.Type, "SimpleItem", StringComparison.Ordinal))
            {
                issues.Add(new PlanValidationIssue($"{path}.type", $"Mission item type '{item.Type}' is not supported yet."));
            }

            if (item.Params.Length != 7)
            {
                issues.Add(new PlanValidationIssue($"{path}.params", "Mission item params must contain seven values."));
            }
            else if (item.Params.Any(static value => double.IsInfinity(value)))
            {
                issues.Add(new PlanValidationIssue($"{path}.params", "Mission item params cannot contain infinity."));
            }
        }
    }

    private static void AddSectionIssues(string path, IReadOnlyList<string> errors, List<PlanValidationIssue> issues)
    {
        foreach (var error in errors)
        {
            issues.Add(new PlanValidationIssue(path, error));
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
