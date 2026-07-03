using VGC.Comms;

namespace VGC.Core.Settings;

public sealed class AppSettingsSnapshot
{
    public string ApplicationName { get; set; } = "VGC";

    public string Theme { get; set; } = "Default";

    public string Language { get; set; } = "system";

    public string? TiandituApiKey { get; set; }

    public string DistanceUnit { get; set; } = "Meters";

    public string SpeedUnit { get; set; } = "m/s";

    public string AltitudeUnit { get; set; } = "Meters";

    public string TemperatureUnit { get; set; } = "Celsius";

    public string MapProvider { get; set; } = "LocalFallback";

    public double DefaultCruiseSpeed { get; set; } = 15;

    public double DefaultHoverSpeed { get; set; } = 5;

    public bool AutoConnectOnStartup { get; set; } = true;

    public string VideoSource { get; set; } = "Disabled";

    public bool FileLoggingEnabled { get; set; }

    public string LogDirectory { get; set; } = string.Empty;

    public long LogRotationMaxBytes { get; set; } = 1_048_576;

    public LinkConfigurationState LinkConfiguration { get; set; } = new();
}
