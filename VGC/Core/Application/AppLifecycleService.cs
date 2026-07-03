using VGC.Core.Logging;
using VGC.Core.Settings;

namespace VGC.Core.Application;

public sealed class AppLifecycleService : IAppLifecycleService
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IAppLogger _logger;

    public AppLifecycleService(IAppSettingsStore settingsStore, IAppLogger logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;
    }

    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized)
        {
            return;
        }

        await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _logger.Info("VGC application services initialized.");
        IsInitialized = true;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
        {
            return Task.CompletedTask;
        }

        _logger.Info("VGC application services shutting down.");
        IsInitialized = false;
        return Task.CompletedTask;
    }
}
