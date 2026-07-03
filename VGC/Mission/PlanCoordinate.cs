using System.Text.Json;
using System.Text.Json.Serialization;

namespace VGC.Mission;

[JsonConverter(typeof(PlanCoordinateJsonConverter))]
public sealed class PlanCoordinate
{
    public PlanCoordinate()
    {
    }

    public PlanCoordinate(double latitude, double longitude, double? altitude = null)
    {
        Latitude = latitude;
        Longitude = longitude;
        Altitude = altitude;
    }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public double? Altitude { get; set; }

    public bool IsValid2D()
    {
        return IsFinite(Latitude)
            && IsFinite(Longitude)
            && Latitude is >= -90 and <= 90
            && Longitude is >= -180 and <= 180;
    }

    public bool IsValid3D()
    {
        return IsValid2D() && Altitude is { } altitude && IsFinite(altitude);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}

public sealed class PlanCoordinateJsonConverter : JsonConverter<PlanCoordinate>
{
    public override PlanCoordinate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Plan coordinate must be a JSON array.");
        }

        reader.Read();
        var latitude = ReadNumber(ref reader, "latitude");
        reader.Read();
        var longitude = ReadNumber(ref reader, "longitude");

        double? altitude = null;
        reader.Read();
        if (reader.TokenType == JsonTokenType.Number)
        {
            altitude = reader.GetDouble();
            reader.Read();
        }

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            throw new JsonException("Plan coordinate must contain latitude, longitude, and optional altitude only.");
        }

        return new PlanCoordinate(latitude, longitude, altitude);
    }

    public override void Write(Utf8JsonWriter writer, PlanCoordinate value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Latitude);
        writer.WriteNumberValue(value.Longitude);
        if (value.Altitude is { } altitude)
        {
            writer.WriteNumberValue(altitude);
        }
        writer.WriteEndArray();
    }

    private static double ReadNumber(ref Utf8JsonReader reader, string name)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException($"Plan coordinate {name} must be numeric.");
        }

        return reader.GetDouble();
    }
}
