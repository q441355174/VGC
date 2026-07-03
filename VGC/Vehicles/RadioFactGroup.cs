using ReactiveUI;
using VGC.Facts;

namespace VGC.Vehicles;

public sealed class RadioFactGroup : ReactiveObject
{
    private Fact? _rssi;
    private Fact? _remoteRssi;
    private Fact? _noise;
    private Fact? _errors;

    public RadioFactGroup()
    {
        Rssi = CreateFact("RADIO_RSSI", "RSSI", FactValueType.Int32, "dBm");
        RemoteRssi = CreateFact("RADIO_REM_RSSI", "Remote RSSI", FactValueType.Int32, "dBm");
        Noise = CreateFact("RADIO_NOISE", "Noise", FactValueType.Int32, "dBm");
        Errors = CreateFact("RADIO_ERRORS", "Errors", FactValueType.Int32, "count");
    }

    public Fact? Rssi { get => _rssi; private set => this.RaiseAndSetIfChanged(ref _rssi, value); }
    public Fact? RemoteRssi { get => _remoteRssi; private set => this.RaiseAndSetIfChanged(ref _remoteRssi, value); }
    public Fact? Noise { get => _noise; private set => this.RaiseAndSetIfChanged(ref _noise, value); }
    public Fact? Errors { get => _errors; private set => this.RaiseAndSetIfChanged(ref _errors, value); }

    public string Summary => Rssi is { } r && RemoteRssi is { } rr
        ? $"RSSI {r.DisplayValue} | Remote {rr.DisplayValue}"
        : "No radio data";

    private static Fact CreateFact(string name, string label, FactValueType type, string units)
    {
        return new Fact(1, name, new FactMetaData(name, type) { ShortDescription = label, Units = units }, 0);
    }
}
