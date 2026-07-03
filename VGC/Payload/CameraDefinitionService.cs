namespace VGC.Payload;

public sealed record CameraDefinition(
    string Id,
    string Name,
    string? Model,
    double SensorWidthMm,
    double SensorHeightMm,
    double FocalLengthMm,
    int ImageWidthPx,
    int ImageHeightPx);

public sealed record CameraSettings(
    string CameraId,
    string Mode,
    double? IntervalSeconds,
    double? ExposureSeconds,
    double? Iso,
    double? WhiteBalance);

public sealed class CameraDefinitionService
{
    private readonly List<CameraDefinition> _definitions = [];

    public IReadOnlyList<CameraDefinition> Definitions => _definitions;

    public void Register(CameraDefinition definition)
    {
        if (_definitions.Any(d => d.Id == definition.Id))
        {
            throw new InvalidOperationException($"Camera '{definition.Id}' already registered.");
        }

        _definitions.Add(definition);
    }

    public CameraDefinition? Find(string id)
    {
        return _definitions.FirstOrDefault(d => d.Id == id);
    }

    public double CalculateGSD(CameraDefinition camera, double altitudeMeters)
    {
        if (altitudeMeters <= 0)
        {
            return 0;
        }

        var sensorWidthM = camera.SensorWidthMm / 1000.0;
        var gsd = sensorWidthM * altitudeMeters / (camera.FocalLengthMm / 1000.0) / camera.ImageWidthPx;
        return gsd;
    }
}
