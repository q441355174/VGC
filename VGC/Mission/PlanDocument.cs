using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class PlanDocument
{
    [JsonPropertyName("fileType")]
    public string FileType { get; set; } = "Plan";

    [JsonPropertyName("groundStation")]
    public string GroundStation { get; set; } = "VGC";

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("mission")]
    public MissionPlan Mission { get; set; } = new();

    [JsonPropertyName("geoFence")]
    public GeoFencePlan GeoFence { get; set; } = new();

    [JsonPropertyName("rallyPoints")]
    public RallyPointsPlan RallyPoints { get; set; } = new();

    [JsonPropertyName("complexMission")]
    public ComplexMissionPlan ComplexMission { get; set; } = new();

    public static PlanDocument CreateBlank()
    {
        return new PlanDocument();
    }
}
