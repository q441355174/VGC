namespace VGC.Core.Application;

public interface IAppLifecycleService
{
    bool IsInitialized { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}
