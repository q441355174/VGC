namespace VGC.Traffic;

public sealed record AdsbVehicle(int IcaoAddress, string Callsign, double Latitude, double Longitude, double AltitudeMeters, double Heading, double SpeedMs);

public sealed record RemoteIdState(string Id, bool IsBroadcasting, string? Error);

public interface IAdsbService
{
    Task<IReadOnlyList<AdsbVehicle>> GetTrafficAsync(CancellationToken cancellationToken = default);
}

public interface IRemoteIdService
{
    Task<RemoteIdState> GetStateAsync(CancellationToken cancellationToken = default);
}
