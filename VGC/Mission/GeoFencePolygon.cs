using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class GeoFencePolygon
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("inclusion")]
    public bool Inclusion { get; set; } = true;

    [JsonPropertyName("polygon")]
    public List<PlanCoordinate> Polygon { get; set; } = [];
}
