namespace VGC.Positioning;

public enum GpsFixQuality { None, NoFix, Fix2D, Fix3D, RtkFloat, RtkFixed }

public sealed record GpsPosition(double Latitude, double Longitude, double AltitudeMeters, GpsFixQuality Fix, int Satellites);

public sealed record RtkState(bool IsActive, double BaselineMeters, int Observations);

public interface IGpsService
{
    Task<GpsPosition?> GetPositionAsync(CancellationToken cancellationToken = default);
    Task<RtkState> GetRtkStateAsync(CancellationToken cancellationToken = default);
}

public interface IPositionService
{
    Task<GpsPosition?> GetCurrentPositionAsync(CancellationToken cancellationToken = default);
}
