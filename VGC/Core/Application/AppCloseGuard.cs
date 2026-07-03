namespace VGC.Core.Application;

public sealed record AppCloseIssue(string Source, string Reason);

public interface IAppCloseGuard
{
    string Name { get; }

    Task<AppCloseIssue?> CheckAsync(CancellationToken cancellationToken = default);
}

public sealed class DelegateAppCloseGuard : IAppCloseGuard
{
    private readonly Func<CancellationToken, Task<AppCloseIssue?>> _check;

    public DelegateAppCloseGuard(string name, Func<CancellationToken, Task<AppCloseIssue?>> check)
    {
        Name = name;
        _check = check;
    }

    public string Name { get; }

    public Task<AppCloseIssue?> CheckAsync(CancellationToken cancellationToken = default)
    {
        return _check(cancellationToken);
    }
}
