using VGC.Mavlink;

namespace VGC.Traffic;

public sealed class AdsbService : IAdsbService
{
    private readonly List<AdsbVehicle> _traffic = [];

    public IReadOnlyList<AdsbVehicle> Traffic => _traffic;

    public Task<IReadOnlyList<AdsbVehicle>> GetTrafficAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<AdsbVehicle>>(Traffic);
    }

    public void HandleAdsbVehicle(MavlinkPacket packet)
    {
        if (packet.MessageId != 246 || packet.Payload.Length < 38)
        {
            return;
        }

        var icao = (int)BitConverter.ToUInt32(packet.Payload, 0);
        var lat = BitConverter.ToInt32(packet.Payload, 4) / 10000000.0;
        var lon = BitConverter.ToInt32(packet.Payload, 8) / 10000000.0;
        var alt = BitConverter.ToInt32(packet.Payload, 12) / 1000.0;
        var heading = BitConverter.ToUInt16(packet.Payload, 16) / 100.0;
        var speed = BitConverter.ToUInt16(packet.Payload, 26) / 100.0;
        var callsign = System.Text.Encoding.ASCII.GetString(packet.Payload, 28, 9).TrimEnd('\0');

        var existing = _traffic.FindIndex(v => v.IcaoAddress == icao);
        var vehicle = new AdsbVehicle(icao, callsign, lat, lon, alt, heading, speed);
        if (existing >= 0)
        {
            _traffic[existing] = vehicle;
        }
        else
        {
            _traffic.Add(vehicle);
        }
    }

    public void Clear() => _traffic.Clear();
}

public sealed class RemoteIdService : IRemoteIdService
{
    private RemoteIdState _state = new("unknown", false, null);

    public Task<RemoteIdState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_state);
    }

    public void HandleRemoteIdMessage(byte[] payload)
    {
        if (payload.Length < 4)
        {
            return;
        }

        _state = new RemoteIdState("remote-id", true, null);
    }
}
