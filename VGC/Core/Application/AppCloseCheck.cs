namespace VGC.Core.Application;

public sealed record AppCloseCheck(bool CanClose, string? Reason, IReadOnlyList<AppCloseIssue>? Issues = null)
{
    public static AppCloseCheck Allowed { get; } = new(true, null, []);

    public static AppCloseCheck Blocked(string reason) => new(false, reason, [new AppCloseIssue("Application", reason)]);

    public static AppCloseCheck Blocked(IReadOnlyList<AppCloseIssue> issues)
    {
        var reason = string.Join("; ", issues.Select(static issue => $"{issue.Source}: {issue.Reason}"));
        return new AppCloseCheck(false, reason, issues);
    }
}
