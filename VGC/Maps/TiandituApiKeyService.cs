using VGC.Core.Settings;

namespace VGC.Maps;

public sealed class TiandituApiKeyService
{
    public const string EnvironmentVariableName = "TIANDITU_TK";

    private readonly IAppSettingsStore? _settingsStore;

    public TiandituApiKeyService(IAppSettingsStore? settingsStore = null)
    {
        _settingsStore = settingsStore;
    }

    public bool HasKey => !string.IsNullOrWhiteSpace(GetKey());

    public string? GetKey()
    {
        var key = _settingsStore?.Current.TiandituApiKey;
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var envKey = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        return !string.IsNullOrWhiteSpace(envKey) ? envKey : null;
    }
}
