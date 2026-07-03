namespace VGC.Comms;

public enum LinkRecoveryState
{
    Healthy,
    RetryScheduled,
    Failed,
    ManualInterventionRequired
}

public sealed record LinkErrorRecoveryPolicy(
    int MaxRetries = 3,
    TimeSpan? BaseDelay = null)
{
    public TimeSpan GetDelay(int retryAttempt)
    {
        var baseDelay = BaseDelay ?? TimeSpan.FromSeconds(1);
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Max(1, retryAttempt));
    }
}

public sealed record LinkErrorRecoverySnapshot(
    string LinkName,
    LinkRecoveryState State,
    int RetryAttempt,
    TimeSpan? RetryAfter,
    string? OperatorMessage,
    string? DiagnosticMessage);

public sealed class LinkErrorRecoveryProjector
{
    private readonly LinkErrorRecoveryPolicy _policy;
    private readonly Dictionary<string, int> _retryAttempts = new(StringComparer.Ordinal);

    public LinkErrorRecoveryProjector(LinkErrorRecoveryPolicy? policy = null)
    {
        _policy = policy ?? new LinkErrorRecoveryPolicy();
    }

    public LinkErrorRecoverySnapshot Project(LinkDiagnosticsSnapshot diagnostics)
    {
        if (diagnostics.IsConnected && string.IsNullOrWhiteSpace(diagnostics.LastError))
        {
            _retryAttempts.Remove(diagnostics.Name);
            return new LinkErrorRecoverySnapshot(diagnostics.Name, LinkRecoveryState.Healthy, 0, null, null, null);
        }

        var attempt = _retryAttempts.TryGetValue(diagnostics.Name, out var current)
            ? current + 1
            : 1;
        _retryAttempts[diagnostics.Name] = attempt;

        if (attempt > _policy.MaxRetries)
        {
            return new LinkErrorRecoverySnapshot(
                diagnostics.Name,
                LinkRecoveryState.ManualInterventionRequired,
                attempt,
                null,
                $"Link {diagnostics.Name} needs manual reconnect.",
                diagnostics.LastError);
        }

        return new LinkErrorRecoverySnapshot(
            diagnostics.Name,
            diagnostics.LastError is null ? LinkRecoveryState.RetryScheduled : LinkRecoveryState.Failed,
            attempt,
            _policy.GetDelay(attempt),
            $"Retrying {diagnostics.Name} connection.",
            diagnostics.LastError);
    }
}
