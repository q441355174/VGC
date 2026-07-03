using System.Text.Json.Serialization;
using System.Text.Json;

namespace VGC.Mission;

public sealed class MissionPlanItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "SimpleItem";

    [JsonPropertyName("command")]
    public int Command { get; set; }

    [JsonPropertyName("frame")]
    public int Frame { get; set; } = 3;

    [JsonPropertyName("params")]
    [JsonConverter(typeof(MissionPlanItemParamsJsonConverter))]
    public double[] Params { get; set; } = [0, 0, 0, 0, 0, 0, 0];

    [JsonPropertyName("coordinate")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double[]? Coordinate { get; set; }

    [JsonIgnore]
    public bool HasCoordinate => Coordinate is { Length: >= 3 };

    [JsonIgnore]
    public double CoordinateLat => HasCoordinate ? Coordinate![0] : (Params.Length > 4 ? Params[4] : 0);

    [JsonIgnore]
    public double CoordinateLon => HasCoordinate ? Coordinate![1] : (Params.Length > 5 ? Params[5] : 0);

    [JsonIgnore]
    public double CoordinateAlt => HasCoordinate ? Coordinate![2] : (Params.Length > 6 ? Params[6] : 0);

    public void SyncCoordinateFromParams()
    {
        if (Params.Length >= 7)
        {
            Coordinate = [Params[4], Params[5], Params[6]];
        }
    }

    [JsonPropertyName("autoContinue")]
    public bool AutoContinue { get; set; } = true;

    [JsonPropertyName("doJumpId")]
    public int DoJumpId { get; set; } = 1;
}

public sealed class MissionPlanItemParamsJsonConverter : JsonConverter<double[]>
{
    public override double[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Mission item params must be a JSON array.");
        }

        var values = new List<double>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return values.ToArray();
            }

            values.Add(reader.TokenType switch
            {
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.Null => double.NaN,
                _ => throw new JsonException("Mission item params can only contain numbers or null.")
            });
        }

        throw new JsonException("Mission item params array was not closed.");
    }

    public override void Write(Utf8JsonWriter writer, double[] value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            if (double.IsNaN(item))
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteNumberValue(item);
            }
        }

        writer.WriteEndArray();
    }
}
