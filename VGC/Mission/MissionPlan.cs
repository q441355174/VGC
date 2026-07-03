using System.Text.Json.Serialization;

namespace VGC.Mission;

public sealed class MissionPlan
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 2;

    [JsonPropertyName("firmwareType")]
    public int FirmwareType { get; set; } = 12;

    [JsonPropertyName("vehicleType")]
    public int VehicleType { get; set; } = 2;

    [JsonPropertyName("cruiseSpeed")]
    public double CruiseSpeed { get; set; } = 15;

    [JsonPropertyName("hoverSpeed")]
    public double HoverSpeed { get; set; } = 5;

    [JsonPropertyName("globalPlanAltitudeMode")]
    public int GlobalPlanAltitudeMode { get; set; } = 1;

    [JsonPropertyName("plannedHomePosition")]
    public double[] PlannedHomePosition { get; set; } = [0, 0, 0];

    [JsonPropertyName("items")]
    public List<MissionPlanItem> Items { get; set; } = [];
}
