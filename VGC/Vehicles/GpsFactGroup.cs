using ReactiveUI;
using VGC.Facts;

namespace VGC.Vehicles;

public sealed class GpsFactGroup : ReactiveObject
{
    private Fact? _fixType;
    private Fact? _satellites;
    private Fact? _hdop;
    private Fact? _vdop;

    public GpsFactGroup()
    {
        FixType = CreateFact("GPS_FIX", "GPS Fix", FactValueType.Int32, "type");
        Satellites = CreateFact("GPS_SATS", "Satellites", FactValueType.Int32, "count");
        Hdop = CreateFact("GPS_HDOP", "HDOP", FactValueType.Float, "m");
        Vdop = CreateFact("GPS_VDOP", "VDOP", FactValueType.Float, "m");
    }

    public Fact? FixType { get => _fixType; private set => this.RaiseAndSetIfChanged(ref _fixType, value); }
    public Fact? Satellites { get => _satellites; private set => this.RaiseAndSetIfChanged(ref _satellites, value); }
    public Fact? Hdop { get => _hdop; private set => this.RaiseAndSetIfChanged(ref _hdop, value); }
    public Fact? Vdop { get => _vdop; private set => this.RaiseAndSetIfChanged(ref _vdop, value); }

    public string Summary => FixType is { } f && Satellites is { } s
        ? $"GPS {f.DisplayValue} | {s.DisplayValue} sats"
        : "No GPS";

    public void UpdateFromVehicle(Vehicle vehicle)
    {
        FixType?.SetRawValue(vehicle.GpsFixType ?? -1);
        Satellites?.SetRawValue(vehicle.SatelliteCount ?? 0);
    }

    private static Fact CreateFact(string name, string label, FactValueType type, string units)
    {
        return new Fact(1, name, new FactMetaData(name, type) { ShortDescription = label, Units = units }, 0);
    }
}
