namespace VGC.Core.Application;

public sealed class AppCloseCoordinator : IAppCloseCoordinator
{
    private readonly List<IAppCloseGuard> _guards = [];

    public IReadOnlyList<IAppCloseGuard> Guards => _guards;

    public void Register(IAppCloseGuard guard)
    {
        _guards.Add(guard);
    }

    public async Task<AppCloseCheck> CanCloseAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<AppCloseIssue>();
        foreach (var guard in _guards)
        {
            var issue = await guard.CheckAsync(cancellationToken).ConfigureAwait(false);
            if (issue is not null)
            {
                issues.Add(issue);
            }
        }

        return issues.Count == 0 ? AppCloseCheck.Allowed : AppCloseCheck.Blocked(issues);
    }
}
