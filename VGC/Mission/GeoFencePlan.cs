using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class GeoFencePlan
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("polygons")]
    public List<GeoFencePolygon> Polygons { get; set; } = [];

    [JsonPropertyName("circles")]
    public List<GeoFenceCircle> Circles { get; set; } = [];

    [JsonPropertyName("breachReturn")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PlanCoordinate? BreachReturn { get; set; }
}
