namespace VGC.Comms;

public abstract class LinkConfiguration
{
    protected LinkConfiguration(string name, LinkType type)
    {
        Name = name;
        Type = type;
    }

    public string Name { get; }

    public LinkType Type { get; }
}
