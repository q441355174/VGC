namespace VGC.Mission;

public sealed record RallyPointsValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static RallyPointsValidationResult Success { get; } = new(true, []);
}

public static class RallyPointsValidation
{
    public static RallyPointsValidationResult Validate(RallyPointsPlan plan)
    {
        var errors = new List<string>();

        if (plan.Version != 2)
        {
            errors.Add("Rally points plan version must be 2.");
        }

        for (var pointIndex = 0; pointIndex < plan.Points.Count; pointIndex++)
        {
            if (!plan.Points[pointIndex].IsValid3D())
            {
                errors.Add($"Rally point {pointIndex} must contain valid latitude, longitude, and altitude.");
            }
        }

        return errors.Count == 0
            ? RallyPointsValidationResult.Success
            : new RallyPointsValidationResult(false, errors);
    }
}
