using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class GeoFenceCircle
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("inclusion")]
    public bool Inclusion { get; set; } = true;

    [JsonPropertyName("circle")]
    public GeoFenceCircleShape Circle { get; set; } = new();
}

public sealed class GeoFenceCircleShape
{
    [JsonPropertyName("center")]
    public PlanCoordinate Center { get; set; } = new();

    [JsonPropertyName("radius")]
    public double Radius { get; set; }
}
