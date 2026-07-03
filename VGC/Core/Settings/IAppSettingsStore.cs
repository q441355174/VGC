namespace VGC.Core.Settings;

public interface IAppSettingsStore
{
    AppSettingsSnapshot Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CancellationToken cancellationToken = default);
}
