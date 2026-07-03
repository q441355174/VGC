namespace VGC.Vehicles;

public sealed record VehicleFlightModeState(byte BaseMode, uint CustomMode, string Name)
{
    private const byte CustomModeEnabled = 0x01;
    private const byte TestEnabled = 0x02;
    private const byte AutoEnabled = 0x04;
    private const byte GuidedEnabled = 0x08;
    private const byte StabilizeEnabled = 0x10;
    private const byte HilEnabled = 0x20;
    private const byte ManualInputEnabled = 0x40;
    private const byte SafetyArmed = 0x80;

    public static VehicleFlightModeState FromHeartbeat(byte baseMode, uint customMode)
    {
        return new VehicleFlightModeState(baseMode, customMode, FormatName(baseMode, customMode));
    }

    private static string FormatName(byte baseMode, uint customMode)
    {
        if (baseMode == 0)
        {
            return "PreFlight";
        }

        if ((baseMode & CustomModeEnabled) == CustomModeEnabled)
        {
            return $"Custom:0x{customMode:x}";
        }

        var names = new List<string>();
        AddIfSet(names, baseMode, ManualInputEnabled, "Manual");
        AddIfSet(names, baseMode, HilEnabled, "HIL");
        AddIfSet(names, baseMode, StabilizeEnabled, "Stabilize");
        AddIfSet(names, baseMode, GuidedEnabled, "Guided");
        AddIfSet(names, baseMode, AutoEnabled, "Auto");
        AddIfSet(names, baseMode, TestEnabled, "Test");
        AddIfSet(names, baseMode, SafetyArmed, "Armed");
        return names.Count == 0 ? $"BaseMode:0x{baseMode:x2}" : string.Join(" ", names);
    }

    private static void AddIfSet(ICollection<string> names, byte baseMode, byte flag, string name)
    {
        if ((baseMode & flag) == flag)
        {
            names.Add(name);
        }
    }
}
