namespace VGC.Mission;

public sealed record GeoFenceValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static GeoFenceValidationResult Success { get; } = new(true, []);
}

public static class GeoFenceValidation
{
    public static GeoFenceValidationResult Validate(GeoFencePlan plan)
    {
        var errors = new List<string>();

        if (plan.Version != 2)
        {
            errors.Add("GeoFence plan version must be 2.");
        }

        for (var polygonIndex = 0; polygonIndex < plan.Polygons.Count; polygonIndex++)
        {
            var polygon = plan.Polygons[polygonIndex];
            if (polygon.Version != 1)
            {
                errors.Add($"GeoFence polygon {polygonIndex} version must be 1.");
            }

            if (polygon.Polygon.Count < 3)
            {
                errors.Add($"GeoFence polygon {polygonIndex} must contain at least three coordinates.");
            }

            for (var coordinateIndex = 0; coordinateIndex < polygon.Polygon.Count; coordinateIndex++)
            {
                if (!polygon.Polygon[coordinateIndex].IsValid2D())
                {
                    errors.Add($"GeoFence polygon {polygonIndex} coordinate {coordinateIndex} is invalid.");
                }
            }
        }

        for (var circleIndex = 0; circleIndex < plan.Circles.Count; circleIndex++)
        {
            var circle = plan.Circles[circleIndex];
            if (circle.Version != 1)
            {
                errors.Add($"GeoFence circle {circleIndex} version must be 1.");
            }

            if (!circle.Circle.Center.IsValid2D())
            {
                errors.Add($"GeoFence circle {circleIndex} center is invalid.");
            }

            if (double.IsNaN(circle.Circle.Radius) || double.IsInfinity(circle.Circle.Radius) || circle.Circle.Radius <= 0)
            {
                errors.Add($"GeoFence circle {circleIndex} radius must be greater than zero.");
            }
        }

        if (plan.BreachReturn is { } breachReturn && !breachReturn.IsValid3D())
        {
            errors.Add("GeoFence breach return coordinate is invalid.");
        }

        return errors.Count == 0
            ? GeoFenceValidationResult.Success
            : new GeoFenceValidationResult(false, errors);
    }
}
