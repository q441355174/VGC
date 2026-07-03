namespace VGC.Comms;

public sealed record LinkDiagnosticsSnapshot(
    string Name,
    LinkType Type,
    string ConnectionState,
    bool IsConnected,
    bool CanSend,
    long BytesReceived,
    long BytesSent,
    DateTimeOffset? LastReceivedAt,
    DateTimeOffset? LastSentAt,
    string? LastError,
    string ReconnectState)
{
    public DateTimeOffset? LastPacketAt => LastReceivedAt >= LastSentAt ? LastReceivedAt : LastSentAt;
}

internal sealed class LinkDiagnosticsAccumulator
{
    public long BytesReceived { get; private set; }

    public long BytesSent { get; private set; }

    public DateTimeOffset? LastReceivedAt { get; private set; }

    public DateTimeOffset? LastSentAt { get; private set; }

    public string? LastError { get; private set; }

    public void AddReceived(int byteCount)
    {
        BytesReceived += Math.Max(0, byteCount);
        LastReceivedAt = DateTimeOffset.UtcNow;
    }

    public void AddSent(int byteCount)
    {
        BytesSent += Math.Max(0, byteCount);
        LastSentAt = DateTimeOffset.UtcNow;
    }

    public void SetError(string error)
    {
        LastError = error;
    }

    public LinkDiagnosticsSnapshot ToSnapshot(ILinkTransport link)
    {
        return new LinkDiagnosticsSnapshot(
            link.Configuration.Name,
            link.Configuration.Type,
            GetConnectionState(link),
            link.IsConnected,
            link.CanSend,
            BytesReceived,
            BytesSent,
            LastReceivedAt,
            LastSentAt,
            LastError,
            "Not configured");
    }

    private static string GetConnectionState(ILinkTransport link)
    {
        if (!link.IsConnected)
        {
            return "Disconnected";
        }

        return link.CanSend ? "Send-capable" : "Connected";
    }
}
