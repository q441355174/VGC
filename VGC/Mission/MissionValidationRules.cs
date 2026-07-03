namespace VGC.Mission;

public sealed record MissionValidationIssue(
    string ItemId,
    string Field,
    string Message,
    bool IsError);

public static class MissionValidationRules
{
    public static IReadOnlyList<MissionValidationIssue> Validate(MissionPlan mission)
    {
        var issues = new List<MissionValidationIssue>();

        if (mission.Items.Count == 0)
        {
            issues.Add(new MissionValidationIssue("mission", "items", "Mission has no items.", false));
            return issues;
        }

        var firstItem = mission.Items[0];
        if (firstItem.Command != 22) // MAV_CMD_NAV_TAKEOFF
        {
            issues.Add(new MissionValidationIssue(firstItem.DoJumpId.ToString(), "command",
                "First mission item should be a takeoff command.", false));
        }

        for (var i = 0; i < mission.Items.Count; i++)
        {
            var item = mission.Items[i];
            var id = item.DoJumpId.ToString();

            if (item.Command <= 0)
            {
                issues.Add(new MissionValidationIssue(id, "command",
                    $"Item {item.DoJumpId} has invalid command {item.Command}.", true));
            }

            if (item.Frame != 0 && item.Frame != 2 && item.Frame != 3 && item.Frame != 6)
            {
                issues.Add(new MissionValidationIssue(id, "frame",
                    $"Item {item.DoJumpId} has invalid frame {item.Frame}.", true));
            }

            if (!item.AutoContinue && i < mission.Items.Count - 1)
            {
                issues.Add(new MissionValidationIssue(id, "autoContinue",
                    $"Item {item.DoJumpId} has autoContinue disabled mid-mission.", false));
            }

            if (item.HasCoordinate)
            {
                if (item.CoordinateLat is < -90 or > 90)
                {
                    issues.Add(new MissionValidationIssue(id, "coordinate",
                        $"Item {item.DoJumpId} latitude out of range.", true));
                }

                if (item.CoordinateLon is < -180 or > 180)
                {
                    issues.Add(new MissionValidationIssue(id, "coordinate",
                        $"Item {item.DoJumpId} longitude out of range.", true));
                }
            }
        }

        return issues;
    }
}
