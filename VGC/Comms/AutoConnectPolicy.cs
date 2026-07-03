namespace VGC.Comms;

public enum AutoConnectOutcome
{
    Connected,
    AlreadyConnected,
    Unsupported,
    Failed
}

public sealed record AutoConnectLinkResult(
    PersistedLinkConfiguration Configuration,
    AutoConnectOutcome Outcome,
    string Message,
    string? ConnectedLinkName = null)
{
    public bool IsConnected => Outcome is AutoConnectOutcome.Connected or AutoConnectOutcome.AlreadyConnected;
}

public sealed record AutoConnectResult(
    string? PreferredActiveLinkName,
    IReadOnlyList<AutoConnectLinkResult> Links)
{
    public int ConnectedCount => Links.Count(static link => link.IsConnected);

    public int FailedCount => Links.Count(static link => link.Outcome == AutoConnectOutcome.Failed);

    public int UnsupportedCount => Links.Count(static link => link.Outcome == AutoConnectOutcome.Unsupported);

    public AutoConnectLinkResult? PreferredResult => string.IsNullOrWhiteSpace(PreferredActiveLinkName)
        ? null
        : Links.FirstOrDefault(link => string.Equals(link.Configuration.Name, PreferredActiveLinkName, StringComparison.Ordinal));

    public ILinkTransport? SelectFallback(IReadOnlyList<ILinkTransport> connectedLinks)
    {
        var sendCapable = connectedLinks.Where(static link => link is { IsConnected: true, CanSend: true }).ToArray();
        if (sendCapable.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(PreferredActiveLinkName))
        {
            var preferred = sendCapable.FirstOrDefault(link => string.Equals(link.Configuration.Name, PreferredActiveLinkName, StringComparison.Ordinal));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        return sendCapable[0];
    }
}
