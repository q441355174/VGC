namespace VGC.Comms;

public sealed class UdpLinkConfiguration : LinkConfiguration
{
    public UdpLinkConfiguration(string name, int localPort, string? targetHost = null, int? targetPort = null)
        : base(name, LinkType.Udp)
    {
        LocalPort = localPort;
        TargetHost = targetHost;
        TargetPort = targetPort;
    }

    public int LocalPort { get; }

    public string? TargetHost { get; }

    public int? TargetPort { get; }
}
