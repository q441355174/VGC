using ReactiveUI;
using VGC.Facts;

namespace VGC.Vehicles;

public sealed class AttitudeFactGroup : ReactiveObject
{
    private Fact? _pitch;
    private Fact? _roll;
    private Fact? _heading;

    public AttitudeFactGroup()
    {
        Pitch = CreateFact("ATT_PITCH", "Pitch", "deg");
        Roll = CreateFact("ATT_ROLL", "Roll", "deg");
        Heading = CreateFact("ATT_HEADING", "Heading", "deg");
    }

    public Fact? Pitch { get => _pitch; private set => this.RaiseAndSetIfChanged(ref _pitch, value); }

    public Fact? Roll { get => _roll; private set => this.RaiseAndSetIfChanged(ref _roll, value); }

    public Fact? Heading { get => _heading; private set => this.RaiseAndSetIfChanged(ref _heading, value); }

    public string Summary => Pitch is { } pitch && Roll is { } roll && Heading is { } heading
        ? $"Pitch {pitch.DisplayValue} | Roll {roll.DisplayValue} | Heading {heading.DisplayValue}"
        : "No attitude data";

    public void UpdateFromVehicle(Vehicle vehicle)
    {
        if (vehicle.PitchDegrees is { } pitch)
        {
            Pitch?.SetRawValue(Math.Round(pitch, 2));
        }

        if (vehicle.RollDegrees is { } roll)
        {
            Roll?.SetRawValue(Math.Round(roll, 2));
        }

        if (vehicle.HeadingDegrees is { } heading)
        {
            Heading?.SetRawValue(Math.Round(heading, 2));
        }
    }

    private static Fact CreateFact(string name, string label, string units)
    {
        return new Fact(1, name, new FactMetaData(name, FactValueType.Double) { ShortDescription = label, Units = units }, 0d);
    }
}

public sealed class EkfFactGroup : ReactiveObject
{
    private Fact? _healthy;
    private Fact? _flags;

    public EkfFactGroup()
    {
        Healthy = new Fact(1, "EKF_HEALTHY", new FactMetaData("EKF_HEALTHY", FactValueType.Boolean) { ShortDescription = "EKF Healthy" }, false);
        Flags = new Fact(1, "EKF_FLAGS", new FactMetaData("EKF_FLAGS", FactValueType.UInt32) { ShortDescription = "Estimator Flags" }, 0u);
    }

    public Fact? Healthy { get => _healthy; private set => this.RaiseAndSetIfChanged(ref _healthy, value); }

    public Fact? Flags { get => _flags; private set => this.RaiseAndSetIfChanged(ref _flags, value); }

    public string Summary => Healthy?.RawValue is true ? "EKF healthy" : "EKF unavailable";

    public void Update(bool healthy, uint flags)
    {
        Healthy?.SetRawValue(healthy);
        Flags?.SetRawValue(flags);
    }
}
