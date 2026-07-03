using VGC.Vehicles;

namespace VGC.Maps;

public sealed class VehicleMapOverlayProjector
{
    private const byte SafetyArmed = 0x80;

    public MapOverlayFrame Build(
        Vehicle? vehicle,
        VehicleCoordinate? home = null,
        IReadOnlyList<VehicleCoordinate>? trajectory = null)
    {
        return new MapOverlayFrame(
            BuildVehicleOverlay(vehicle),
            home is null ? null : new HomeMapOverlay(MapCoordinate.FromVehicleCoordinate(home)),
            BuildTrajectoryOverlay(vehicle, trajectory));
    }

    private static VehicleMapOverlay? BuildVehicleOverlay(Vehicle? vehicle)
    {
        if (vehicle?.Coordinate is not { } coordinate)
        {
            return null;
        }

        return new VehicleMapOverlay(
            vehicle.Id,
            MapCoordinate.FromVehicleCoordinate(coordinate),
            vehicle.FlightModeName,
            (vehicle.BaseMode & SafetyArmed) == SafetyArmed,
            $"Vehicle {vehicle.Id}");
    }

    private static TrajectoryMapOverlay? BuildTrajectoryOverlay(Vehicle? vehicle, IReadOnlyList<VehicleCoordinate>? trajectory)
    {
        if (vehicle is null || trajectory is null || trajectory.Count == 0)
        {
            return null;
        }

        return new TrajectoryMapOverlay(
            vehicle.Id,
            trajectory.Select(MapCoordinate.FromVehicleCoordinate).ToArray());
    }
}
