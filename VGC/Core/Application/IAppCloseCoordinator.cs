namespace VGC.Core.Application;

public interface IAppCloseCoordinator
{
    Task<AppCloseCheck> CanCloseAsync(CancellationToken cancellationToken = default);
}
