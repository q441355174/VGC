using VGC.Vehicles;

namespace VGC.Setup;

public enum AirframeType
{
    QuadX,
    QuadH,
    QuadPlus,
    HexX,
    HexPlus,
    OctoX,
    OctoPlus,
    Y6,
    FixedWing,
    FlyingWing,
    VTOLQuadPlane,
    VTOLTailsitter,
    VTOLTiltrotor,
    Rover,
    Boat,
    Submarine
}

public sealed record AirframeDefinition(
    AirframeType Type,
    string Name,
    string Description,
    int MotorCount,
    string ImageAsset);

public sealed record AirframeSelectionSnapshot(
    IReadOnlyList<AirframeDefinition> AvailableAirframes,
    AirframeDefinition? SelectedAirframe,
    string StatusText);

public sealed class AirframeSelectionRuntime
{
    private static readonly AirframeDefinition[] AllAirframes =
    [
        new(AirframeType.QuadX, "Quad X", "Quadcopter in X configuration", 4, "airframe_quad_x.png"),
        new(AirframeType.QuadH, "Quad H", "Quadcopter in H configuration", 4, "airframe_quad_h.png"),
        new(AirframeType.QuadPlus, "Quad +", "Quadcopter in plus configuration", 4, "airframe_quad_plus.png"),
        new(AirframeType.HexX, "Hex X", "Hexacopter in X configuration", 6, "airframe_hex_x.png"),
        new(AirframeType.HexPlus, "Hex +", "Hexacopter in plus configuration", 6, "airframe_hex_plus.png"),
        new(AirframeType.OctoX, "Octo X", "Octocopter in X configuration", 8, "airframe_octo_x.png"),
        new(AirframeType.OctoPlus, "Octo +", "Octocopter in plus configuration", 8, "airframe_octo_plus.png"),
        new(AirframeType.Y6, "Y6", "Y6 coaxial hexacopter", 6, "airframe_y6.png"),
        new(AirframeType.FixedWing, "Fixed Wing", "Conventional fixed-wing airplane", 1, "airframe_fixed_wing.png"),
        new(AirframeType.FlyingWing, "Flying Wing", "Tailless flying wing", 1, "airframe_flying_wing.png"),
        new(AirframeType.VTOLQuadPlane, "VTOL QuadPlane", "Fixed wing with quad VTOL motors", 5, "airframe_vtol_quadplane.png"),
        new(AirframeType.VTOLTailsitter, "VTOL Tailsitter", "VTOL vehicle that sits on its tail for takeoff", 4, "airframe_vtol_tailsitter.png"),
        new(AirframeType.VTOLTiltrotor, "VTOL Tiltrotor", "VTOL with tilting rotors for transition", 4, "airframe_vtol_tiltrotor.png"),
        new(AirframeType.Rover, "Rover", "Ground vehicle with wheels or tracks", 2, "airframe_rover.png"),
        new(AirframeType.Boat, "Boat", "Surface watercraft", 2, "airframe_boat.png"),
        new(AirframeType.Submarine, "Submarine", "Underwater vehicle", 4, "airframe_submarine.png")
    ];

    private IReadOnlyList<AirframeDefinition> _available = [];
    private AirframeDefinition? _selected;

    public AirframeSelectionSnapshot Snapshot => BuildSnapshot();

    public IReadOnlyList<AirframeDefinition> GetAvailableAirframes(MavType vehicleType)
    {
        _available = vehicleType switch
        {
            MavType.Quadrotor or MavType.GenericMultirotor =>
                AllAirframes.Where(static a => a.Type is AirframeType.QuadX or AirframeType.QuadH or AirframeType.QuadPlus).ToArray(),
            MavType.Hexarotor =>
                AllAirframes.Where(static a => a.Type is AirframeType.HexX or AirframeType.HexPlus or AirframeType.Y6).ToArray(),
            MavType.Octorotor =>
                AllAirframes.Where(static a => a.Type is AirframeType.OctoX or AirframeType.OctoPlus).ToArray(),
            MavType.FixedWing =>
                AllAirframes.Where(static a => a.Type is AirframeType.FixedWing or AirframeType.FlyingWing).ToArray(),
            MavType.VtolQuadrotor or MavType.VtolTiltrotor or MavType.VtolDuorotor =>
                AllAirframes.Where(static a => a.Type is AirframeType.VTOLQuadPlane or AirframeType.VTOLTailsitter or AirframeType.VTOLTiltrotor).ToArray(),
            MavType.GroundRover =>
                AllAirframes.Where(static a => a.Type == AirframeType.Rover).ToArray(),
            MavType.SurfaceBoat =>
                AllAirframes.Where(static a => a.Type == AirframeType.Boat).ToArray(),
            MavType.Submarine =>
                AllAirframes.Where(static a => a.Type == AirframeType.Submarine).ToArray(),
            _ => AllAirframes
        };

        _selected = null;
        return _available;
    }

    public AirframeSelectionSnapshot Select(AirframeType type)
    {
        _selected = _available.FirstOrDefault(a => a.Type == type);
        return BuildSnapshot();
    }

    private AirframeSelectionSnapshot BuildSnapshot()
    {
        return new AirframeSelectionSnapshot(
            _available,
            _selected,
            _selected is null
                ? $"{_available.Count} airframes available"
                : $"Selected {_selected.Name} ({_selected.MotorCount} motors)");
    }
}
