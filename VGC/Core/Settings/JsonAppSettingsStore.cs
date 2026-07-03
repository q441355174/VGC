using System.Text.Json;

namespace VGC.Core.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true
    };

    public AppSettingsSnapshot Current { get; private set; } = new();

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var path = SettingsPath();
        if (!File.Exists(path))
        {
            Current = new AppSettingsSnapshot();
            await SaveAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var stream = File.OpenRead(path);
        Current = await JsonSerializer.DeserializeAsync<AppSettingsSnapshot>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new AppSettingsSnapshot();
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var path = SettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, Current, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string SettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        return Path.Combine(appData, "VGC", "settings.json");
    }
}
