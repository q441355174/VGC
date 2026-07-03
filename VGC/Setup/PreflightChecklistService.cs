using VGC.Firmware;
using VGC.Vehicles;

namespace VGC.Setup;

public enum ChecklistItemStatus
{
    Pending,
    Passed,
    Failed,
    Skipped
}

public sealed record PreflightChecklistItem(
    string Id,
    string Label,
    ChecklistItemStatus Status,
    string Detail,
    bool IsCritical);

public sealed record PreflightChecklist(
    string VehicleLabel,
    IReadOnlyList<PreflightChecklistItem> Items,
    bool AllPassed,
    bool HasFailures,
    string Summary);

public sealed class PreflightChecklistService
{
    public PreflightChecklist Evaluate(Vehicle vehicle)
    {
        var items = new List<PreflightChecklistItem>
        {
            EvaluateGpsFix(vehicle),
            EvaluateSatelliteCount(vehicle),
            EvaluateBattery(vehicle),
            EvaluateHomePosition(vehicle),
            EvaluateArmableState(vehicle),
            EvaluateEstimator(vehicle),
            EvaluateFirmwareKnown(vehicle)
        };

        var allPassed = items.All(i => i.Status == ChecklistItemStatus.Passed || i.Status == ChecklistItemStatus.Skipped);
        var hasFailures = items.Any(i => i.Status == ChecklistItemStatus.Failed);
        var passedCount = items.Count(i => i.Status == ChecklistItemStatus.Passed);
        var failedCount = items.Count(i => i.Status == ChecklistItemStatus.Failed);

        return new PreflightChecklist(
            $"Vehicle {vehicle.Id}",
            items,
            allPassed,
            hasFailures,
            $"Preflight: {passedCount}/{items.Count} passed, {failedCount} failed");
    }

    private static PreflightChecklistItem EvaluateGpsFix(Vehicle vehicle)
    {
        var fix = vehicle.GpsFixType;
        return fix switch
        {
            >= 3 => new PreflightChecklistItem("gps-fix", "GPS Fix", ChecklistItemStatus.Passed, $"GPS fix type {fix}", true),
            >= 2 => new PreflightChecklistItem("gps-fix", "GPS Fix", ChecklistItemStatus.Passed, $"GPS fix type {fix} (2D)", false),
            _ => new PreflightChecklistItem("gps-fix", "GPS Fix", ChecklistItemStatus.Failed, $"No GPS fix (type {fix})", true)
        };
    }

    private static PreflightChecklistItem EvaluateSatelliteCount(Vehicle vehicle)
    {
        var sats = vehicle.SatelliteCount;
        return sats switch
        {
            >= 10 => new PreflightChecklistItem("satellites", "Satellites", ChecklistItemStatus.Passed, $"{sats} satellites", false),
            >= 6 => new PreflightChecklistItem("satellites", "Satellites", ChecklistItemStatus.Passed, $"{sats} satellites (marginal)", false),
            0 => new PreflightChecklistItem("satellites", "Satellites", ChecklistItemStatus.Skipped, "No satellite data", false),
            _ => new PreflightChecklistItem("satellites", "Satellites", ChecklistItemStatus.Failed, $"{sats} satellites", true)
        };
    }

    private static PreflightChecklistItem EvaluateBattery(Vehicle vehicle)
    {
        var percent = vehicle.BatteryRemainingPercent;
        var voltage = vehicle.BatteryVoltage;
        return percent switch
        {
            >= 50 => new PreflightChecklistItem("battery", "Battery", ChecklistItemStatus.Passed, $"{percent}% | {voltage:F1}V", true),
            >= 25 => new PreflightChecklistItem("battery", "Battery", ChecklistItemStatus.Passed, $"{percent}% (low) | {voltage:F1}V", false),
            >= 0 => new PreflightChecklistItem("battery", "Battery", ChecklistItemStatus.Failed, $"{percent}% (critical) | {voltage:F1}V", true),
            _ => new PreflightChecklistItem("battery", "Battery", ChecklistItemStatus.Skipped, "No battery data", true)
        };
    }

    private static PreflightChecklistItem EvaluateHomePosition(Vehicle vehicle)
    {
        return vehicle.Coordinate is not null
            ? new PreflightChecklistItem("home", "Home Position", ChecklistItemStatus.Passed, $"Home: {vehicle.Coordinate.Latitude:F6}, {vehicle.Coordinate.Longitude:F6}", true)
            : new PreflightChecklistItem("home", "Home Position", ChecklistItemStatus.Failed, "No home position", true);
    }

    private static PreflightChecklistItem EvaluateArmableState(Vehicle vehicle)
    {
        return vehicle.SystemStatus switch
        {
            3 or 4 => new PreflightChecklistItem("armable", "Armable", ChecklistItemStatus.Passed, "Vehicle is armable", true),
            _ => new PreflightChecklistItem("armable", "Armable", ChecklistItemStatus.Failed, "Vehicle not armable", true)
        };
    }

    private static PreflightChecklistItem EvaluateEstimator(Vehicle vehicle)
    {
        return vehicle.EstimatorOk
            ? new PreflightChecklistItem("estimator", "EKF Estimator", ChecklistItemStatus.Passed, "EKF healthy", true)
            : new PreflightChecklistItem("estimator", "EKF Estimator", ChecklistItemStatus.Skipped, "No EKF data", false);
    }

    private static PreflightChecklistItem EvaluateFirmwareKnown(Vehicle vehicle)
    {
        return vehicle.Autopilot != MavAutopilot.Generic
            ? new PreflightChecklistItem("firmware", "Firmware", ChecklistItemStatus.Passed, $"{vehicle.Autopilot}", false)
            : new PreflightChecklistItem("firmware", "Firmware", ChecklistItemStatus.Failed, "Unknown firmware", true);
    }
}
