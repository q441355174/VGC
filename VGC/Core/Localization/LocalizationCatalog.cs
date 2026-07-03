namespace VGC.Core.Localization;

public sealed record LocalizationKey(string Key, string DefaultText, string Context);

public sealed class LocalizationCatalog
{
    private readonly Dictionary<string, LocalizationKey> _keys = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, LocalizationKey> Keys => _keys;

    public void Register(string key, string defaultText, string context)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Localization key is required.", nameof(key));
        }

        _keys[key] = new LocalizationKey(key, defaultText, context);
    }

    public string Resolve(string key)
    {
        return _keys.TryGetValue(key, out var value) ? value.DefaultText : key;
    }

    public static LocalizationCatalog CreateDefaultBoundary()
    {
        var catalog = new LocalizationCatalog();
        catalog.Register("settings.general.theme", "Theme", "Settings");
        catalog.Register("settings.general.language", "Language", "Settings");
        catalog.Register("settings.units.distance", "Distance Unit", "Settings");
        catalog.Register("settings.map.provider", "Map Provider", "Settings");
        catalog.Register("settings.links.autoconnect", "Auto Connect On Startup", "Settings");
        catalog.Register("logs.viewer.title", "Logs", "Analyze");
        return catalog;
    }
}
