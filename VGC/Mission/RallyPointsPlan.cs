using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class RallyPointsPlan
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("points")]
    public List<PlanCoordinate> Points { get; set; } = [];
}
