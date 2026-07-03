namespace VGC.Comms;

public sealed class TcpLinkConfiguration : LinkConfiguration
{
    public TcpLinkConfiguration(string name, string host, int port, bool isServer = false)
        : base(name, LinkType.Tcp)
    {
        Host = host;
        Port = port;
        IsServer = isServer;
    }

    public string Host { get; }

    public int Port { get; }

    public bool IsServer { get; }
}
