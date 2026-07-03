using VGC.Facts;

namespace VGC.Core.Settings;

public sealed class SettingsGroup
{
    private readonly Dictionary<string, Fact> _facts = [];

    public SettingsGroup(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public IReadOnlyDictionary<string, Fact> Facts => _facts;

    public Fact DefineSetting(string key, string label, FactValueType valueType, object? defaultValue, string? units = null, string? description = null)
    {
        var metaData = new FactMetaData(key, valueType)
        {
            ShortDescription = description ?? label,
            Units = units
        };
        var fact = new Fact(0, key, metaData, defaultValue ?? 0);
        _facts[key] = fact;
        return fact;
    }

    public bool TryGet(string key, out Fact? fact) => _facts.TryGetValue(key, out fact);

    public void ApplySnapshot(AppSettingsSnapshot snapshot)
    {
        TrySetValue("distanceUnit", snapshot.DistanceUnit);
        TrySetValue("theme", snapshot.Theme);
        TrySetValue("language", snapshot.Language);
        TrySetValue("speedUnit", snapshot.SpeedUnit);
        TrySetValue("altitudeUnit", snapshot.AltitudeUnit);
        TrySetValue("temperatureUnit", snapshot.TemperatureUnit);
        TrySetValue("mapProvider", snapshot.MapProvider);
        TrySetValue("tiandituApiKey", snapshot.TiandituApiKey);
        TrySetValue("defaultCruiseSpeed", snapshot.DefaultCruiseSpeed);
        TrySetValue("defaultHoverSpeed", snapshot.DefaultHoverSpeed);
        TrySetValue("autoConnectOnStartup", snapshot.AutoConnectOnStartup);
        TrySetValue("videoSource", snapshot.VideoSource);
        TrySetValue("fileLoggingEnabled", snapshot.FileLoggingEnabled);
        TrySetValue("logDirectory", snapshot.LogDirectory);
        TrySetValue("logRotationMaxBytes", snapshot.LogRotationMaxBytes);
    }

    public AppSettingsSnapshot ToSnapshot(AppSettingsSnapshot existing)
    {
        existing.Theme = TryGetString("theme") ?? existing.Theme;
        existing.Language = TryGetString("language") ?? existing.Language;
        existing.DistanceUnit = TryGetString("distanceUnit") ?? existing.DistanceUnit;
        existing.SpeedUnit = TryGetString("speedUnit") ?? existing.SpeedUnit;
        existing.AltitudeUnit = TryGetString("altitudeUnit") ?? existing.AltitudeUnit;
        existing.TemperatureUnit = TryGetString("temperatureUnit") ?? existing.TemperatureUnit;
        existing.MapProvider = TryGetString("mapProvider") ?? existing.MapProvider;
        existing.TiandituApiKey = TryGetString("tiandituApiKey") ?? existing.TiandituApiKey;
        existing.DefaultCruiseSpeed = TryGetDouble("defaultCruiseSpeed") ?? existing.DefaultCruiseSpeed;
        existing.DefaultHoverSpeed = TryGetDouble("defaultHoverSpeed") ?? existing.DefaultHoverSpeed;
        existing.AutoConnectOnStartup = TryGetBool("autoConnectOnStartup") ?? existing.AutoConnectOnStartup;
        existing.VideoSource = TryGetString("videoSource") ?? existing.VideoSource;
        existing.FileLoggingEnabled = TryGetBool("fileLoggingEnabled") ?? existing.FileLoggingEnabled;
        existing.LogDirectory = TryGetString("logDirectory") ?? existing.LogDirectory;
        existing.LogRotationMaxBytes = TryGetLong("logRotationMaxBytes") ?? existing.LogRotationMaxBytes;
        return existing;
    }

    private void TrySetValue(string key, object? value)
    {
        if (value is not null && _facts.TryGetValue(key, out var fact))
        {
            fact.SetRawValue(value);
        }
    }

    private string? TryGetString(string key)
    {
        return _facts.TryGetValue(key, out var fact) ? fact.RawValue?.ToString() : null;
    }

    private double? TryGetDouble(string key)
    {
        return _facts.TryGetValue(key, out var fact) && fact.RawValue is IConvertible c
            ? Convert.ToDouble(c)
            : null;
    }

    private bool? TryGetBool(string key)
    {
        if (!_facts.TryGetValue(key, out var fact) || fact.RawValue is null)
        {
            return null;
        }

        if (fact.RawValue is bool value)
        {
            return value;
        }

        return bool.TryParse(fact.RawValue.ToString(), out var parsed) ? parsed : null;
    }

    private long? TryGetLong(string key)
    {
        return _facts.TryGetValue(key, out var fact) && fact.RawValue is IConvertible c
            ? Convert.ToInt64(c)
            : null;
    }
}

public sealed record SettingsFactDefinition(
    string GroupName,
    string Key,
    string Label,
    FactValueType ValueType,
    object? DefaultValue,
    string? Units = null,
    string? Description = null);

public sealed class SettingsManager
{
    private readonly Dictionary<string, SettingsGroup> _groups = [];

    public SettingsGroup GetOrCreateGroup(string name)
    {
        if (!_groups.TryGetValue(name, out var group))
        {
            group = new SettingsGroup(name);
            _groups[name] = group;
        }

        return group;
    }

    public IReadOnlyDictionary<string, SettingsGroup> Groups => _groups;

    public SettingsGroup General => GetOrCreateGroup("General");

    public static IReadOnlyList<SettingsFactDefinition> DefaultDefinitions { get; } =
    [
        new("General", "theme", "Theme", FactValueType.String, "Default"),
        new("General", "language", "Language", FactValueType.String, "system"),
        new("Units", "distanceUnit", "Distance Unit", FactValueType.String, "Meters"),
        new("Units", "speedUnit", "Speed Unit", FactValueType.String, "m/s"),
        new("Units", "altitudeUnit", "Altitude Unit", FactValueType.String, "Meters"),
        new("Units", "temperatureUnit", "Temperature Unit", FactValueType.String, "Celsius"),
        new("Units", "defaultCruiseSpeed", "Default Cruise Speed", FactValueType.Double, 15d, "m/s"),
        new("Units", "defaultHoverSpeed", "Default Hover Speed", FactValueType.Double, 5d, "m/s"),
        new("Map", "mapProvider", "Map Provider", FactValueType.String, "LocalFallback"),
        new("Map", "tiandituApiKey", "Tianditu API Key", FactValueType.String, null),
        new("Video", "videoSource", "Video Source", FactValueType.String, "Disabled"),
        new("Links", "autoConnectOnStartup", "Auto Connect On Startup", FactValueType.Boolean, true),
        new("Logging", "fileLoggingEnabled", "File Logging Enabled", FactValueType.Boolean, false),
        new("Logging", "logDirectory", "Log Directory", FactValueType.String, string.Empty),
        new("Logging", "logRotationMaxBytes", "Log Rotation Max Bytes", FactValueType.Int32, 1_048_576)
    ];

    public static SettingsManager CreateDefault()
    {
        var manager = new SettingsManager();
        manager.RegisterDefaults();
        return manager;
    }

    public void RegisterDefaults()
    {
        foreach (var definition in DefaultDefinitions)
        {
            Register(definition);
        }
    }

    public Fact Register(SettingsFactDefinition definition)
    {
        return GetOrCreateGroup(definition.GroupName).DefineSetting(
            definition.Key,
            definition.Label,
            definition.ValueType,
            definition.DefaultValue,
            definition.Units,
            definition.Description);
    }

    public void ApplySnapshot(AppSettingsSnapshot snapshot)
    {
        foreach (var group in _groups.Values)
        {
            group.ApplySnapshot(snapshot);
        }
    }

    public AppSettingsSnapshot ToSnapshot(AppSettingsSnapshot? existing = null)
    {
        var snapshot = existing ?? new AppSettingsSnapshot();
        foreach (var group in _groups.Values)
        {
            group.ToSnapshot(snapshot);
        }

        return snapshot;
    }

    public async Task LoadAsync(IAppSettingsStore store, CancellationToken cancellationToken = default)
    {
        await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        ApplySnapshot(store.Current);
    }

    public Task SaveAsync(IAppSettingsStore store, CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(ToSnapshot(store.Current), cancellationToken);
    }
}

public static class AppSettingsStoreExtensions
{
    public static async Task SaveAsync(
        this IAppSettingsStore store,
        AppSettingsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        Copy(snapshot, store.Current);
        await store.SaveAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Copy(AppSettingsSnapshot source, AppSettingsSnapshot target)
    {
        target.ApplicationName = source.ApplicationName;
        target.Theme = source.Theme;
        target.Language = source.Language;
        target.TiandituApiKey = source.TiandituApiKey;
        target.DistanceUnit = source.DistanceUnit;
        target.SpeedUnit = source.SpeedUnit;
        target.AltitudeUnit = source.AltitudeUnit;
        target.TemperatureUnit = source.TemperatureUnit;
        target.MapProvider = source.MapProvider;
        target.DefaultCruiseSpeed = source.DefaultCruiseSpeed;
        target.DefaultHoverSpeed = source.DefaultHoverSpeed;
        target.AutoConnectOnStartup = source.AutoConnectOnStartup;
        target.VideoSource = source.VideoSource;
        target.FileLoggingEnabled = source.FileLoggingEnabled;
        target.LogDirectory = source.LogDirectory;
        target.LogRotationMaxBytes = source.LogRotationMaxBytes;
        target.LinkConfiguration = source.LinkConfiguration;
    }
}
