using ReactiveUI;
using VGC.Facts;

namespace VGC.Vehicles;

public sealed class VibrationFactGroup : ReactiveObject
{
    private Fact? _vibeX;
    private Fact? _vibeY;
    private Fact? _vibeZ;

    public VibrationFactGroup()
    {
        VibeX = CreateFact("VIBE_X", "Vibe X", FactValueType.Float, "m/s²");
        VibeY = CreateFact("VIBE_Y", "Vibe Y", FactValueType.Float, "m/s²");
        VibeZ = CreateFact("VIBE_Z", "Vibe Z", FactValueType.Float, "m/s²");
    }

    public Fact? VibeX { get => _vibeX; private set => this.RaiseAndSetIfChanged(ref _vibeX, value); }
    public Fact? VibeY { get => _vibeY; private set => this.RaiseAndSetIfChanged(ref _vibeY, value); }
    public Fact? VibeZ { get => _vibeZ; private set => this.RaiseAndSetIfChanged(ref _vibeZ, value); }

    public string Summary => VibeX is { } x && VibeY is { } y && VibeZ is { } z
        ? $"Vibe {x.DisplayValue}/{y.DisplayValue}/{z.DisplayValue}"
        : "No vibration data";

    private static Fact CreateFact(string name, string label, FactValueType type, string units)
    {
        return new Fact(1, name, new FactMetaData(name, type) { ShortDescription = label, Units = units }, 0);
    }
}

public sealed class WindFactGroup : ReactiveObject
{
    private Fact? _speed;
    private Fact? _direction;

    public WindFactGroup()
    {
        Speed = CreateFact("WIND_SPEED", "Wind Speed", FactValueType.Float, "m/s");
        Direction = CreateFact("WIND_DIR", "Wind Direction", FactValueType.Float, "deg");
    }

    public Fact? Speed { get => _speed; private set => this.RaiseAndSetIfChanged(ref _speed, value); }
    public Fact? Direction { get => _direction; private set => this.RaiseAndSetIfChanged(ref _direction, value); }

    public string Summary => Speed is { } s
        ? $"Wind {s.DisplayValue}" + (Direction is { } d ? $" at {d.DisplayValue}" : "")
        : "No wind data";

    private static Fact CreateFact(string name, string label, FactValueType type, string units)
    {
        return new Fact(1, name, new FactMetaData(name, type) { ShortDescription = label, Units = units }, 0);
    }
}
