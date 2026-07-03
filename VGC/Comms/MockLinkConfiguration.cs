namespace VGC.Comms;

public sealed class MockLinkConfiguration : LinkConfiguration
{
    public MockLinkConfiguration(string name)
        : base(name, LinkType.Mock)
    {
    }
}
