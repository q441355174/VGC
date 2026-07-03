using ReactiveUI;
using VGC.Facts;

namespace VGC.Vehicles;

public sealed class BatteryFactGroup : ReactiveObject
{
    private Fact? _voltage;
    private Fact? _remainingPercent;
    private Fact? _current;
    private Fact? _temperature;
    private int _batteryCount;

    public BatteryFactGroup()
    {
        Voltage = CreateBatteryFact("BAT_VOLTAGE", "Battery Voltage", "V");
        RemainingPercent = CreateBatteryFact("BAT_REMAINING", "Battery Remaining", "%");
        Current = CreateBatteryFact("BAT_CURRENT", "Battery Current", "A");
        Temperature = CreateBatteryFact("BAT_TEMP", "Battery Temperature", "°C");
    }

    public Fact? Voltage
    {
        get => _voltage;
        private set => this.RaiseAndSetIfChanged(ref _voltage, value);
    }

    public Fact? RemainingPercent
    {
        get => _remainingPercent;
        private set => this.RaiseAndSetIfChanged(ref _remainingPercent, value);
    }

    public Fact? Current
    {
        get => _current;
        private set => this.RaiseAndSetIfChanged(ref _current, value);
    }

    public Fact? Temperature
    {
        get => _temperature;
        private set => this.RaiseAndSetIfChanged(ref _temperature, value);
    }

    public int BatteryCount
    {
        get => _batteryCount;
        private set => this.RaiseAndSetIfChanged(ref _batteryCount, value);
    }

    public string Summary => Voltage is { } v && RemainingPercent is { } r
        ? $"{v.DisplayValue} | {r.DisplayValue}"
        : "No battery data";

    public void UpdateFromVehicle(Vehicle vehicle)
    {
        if (vehicle.BatteryVoltage is { } voltage)
        {
            Voltage?.SetRawValue(voltage);
        }

        if (vehicle.BatteryRemainingPercent is { } remaining)
        {
            RemainingPercent?.SetRawValue(remaining);
        }

        BatteryCount = vehicle.BatteryVoltage is not null ? 1 : 0;
    }

    private static Fact CreateBatteryFact(string name, string label, string units)
    {
        return new Fact(
            componentId: 1,
            name,
            new FactMetaData(name, FactValueType.Float) { ShortDescription = label, Units = units },
            0);
    }
}
