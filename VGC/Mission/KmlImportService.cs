using System.Xml.Linq;
using VGC.Maps;

namespace VGC.Mission;

public enum KmlPlacemarkType
{
    Point,
    LineString,
    Polygon
}

public sealed record KmlPlacemark(
    string Name,
    IReadOnlyList<MapCoordinate> Coordinates,
    KmlPlacemarkType Type);

public sealed record KmlImportResult(
    IReadOnlyList<KmlPlacemark> Placemarks,
    int WaypointCount,
    string StatusText,
    IReadOnlyList<string> Errors);

public sealed class KmlImportService
{
    private static readonly XNamespace KmlNs = "http://www.opengis.net/kml/2.2";

    public KmlImportResult ParseKml(Stream stream)
    {
        var placemarks = new List<KmlPlacemark>();
        var errors = new List<string>();

        XDocument doc;
        try
        {
            doc = XDocument.Load(stream);
        }
        catch (Exception ex)
        {
            return new KmlImportResult([], 0, "Failed to parse KML.", [ex.Message]);
        }

        var ns = doc.Root?.Name.Namespace ?? KmlNs;
        var placemarkElements = doc.Descendants(ns + "Placemark");

        foreach (var element in placemarkElements)
        {
            try
            {
                var name = element.Element(ns + "name")?.Value ?? "Unnamed";

                var pointElement = element.Element(ns + "Point");
                if (pointElement is not null)
                {
                    var coords = ParseCoordinateString(pointElement.Element(ns + "coordinates")?.Value);
                    if (coords.Count > 0)
                    {
                        placemarks.Add(new KmlPlacemark(name, coords, KmlPlacemarkType.Point));
                    }

                    continue;
                }

                var lineElement = element.Element(ns + "LineString");
                if (lineElement is not null)
                {
                    var coords = ParseCoordinateString(lineElement.Element(ns + "coordinates")?.Value);
                    if (coords.Count > 0)
                    {
                        placemarks.Add(new KmlPlacemark(name, coords, KmlPlacemarkType.LineString));
                    }

                    continue;
                }

                var polygonElement = element.Element(ns + "Polygon");
                if (polygonElement is not null)
                {
                    var outerBoundary = polygonElement
                        .Element(ns + "outerBoundaryIs")?
                        .Element(ns + "LinearRing")?
                        .Element(ns + "coordinates")?.Value;
                    var coords = ParseCoordinateString(outerBoundary);
                    if (coords.Count > 0)
                    {
                        placemarks.Add(new KmlPlacemark(name, coords, KmlPlacemarkType.Polygon));
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Error parsing placemark: {ex.Message}");
            }
        }

        var waypointCount = placemarks.Sum(static p => p.Coordinates.Count);
        var statusText = errors.Count > 0
            ? $"Imported {placemarks.Count} placemarks with {errors.Count} error(s)."
            : $"Imported {placemarks.Count} placemarks, {waypointCount} coordinates.";

        return new KmlImportResult(placemarks, waypointCount, statusText, errors);
    }

    public MissionPlan ConvertToMissionPlan(KmlImportResult importResult)
    {
        var plan = new MissionPlan();
        var doJumpId = 1;

        foreach (var placemark in importResult.Placemarks)
        {
            foreach (var coordinate in placemark.Coordinates)
            {
                var item = new MissionPlanItem
                {
                    Command = MavlinkMissionCommandIds.NavWaypoint,
                    DoJumpId = doJumpId++,
                    Coordinate = [coordinate.Latitude, coordinate.Longitude, coordinate.AltitudeMeters ?? 0]
                };

                item.Params =
                [
                    0, // hold time
                    0, // acceptance radius
                    0, // pass radius
                    double.NaN, // yaw
                    coordinate.Latitude,
                    coordinate.Longitude,
                    coordinate.AltitudeMeters ?? 0
                ];

                plan.Items.Add(item);
            }
        }

        return plan;
    }

    private static IReadOnlyList<MapCoordinate> ParseCoordinateString(string? coordinatesText)
    {
        if (string.IsNullOrWhiteSpace(coordinatesText))
        {
            return [];
        }

        var result = new List<MapCoordinate>();
        var tuples = coordinatesText.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

        foreach (var tuple in tuples)
        {
            var parts = tuple.Split(',');
            if (parts.Length < 2)
            {
                continue;
            }

            if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lon))
            {
                continue;
            }

            if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var lat))
            {
                continue;
            }

            double? alt = null;
            if (parts.Length >= 3 && double.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var altitude))
            {
                alt = altitude;
            }

            result.Add(new MapCoordinate(lat, lon, alt));
        }

        return result;
    }
}
