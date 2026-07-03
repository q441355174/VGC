namespace VGC.Comms;

public sealed class LogReplayLinkConfiguration : LinkConfiguration
{
    public LogReplayLinkConfiguration(string name, string filePath, double speed = 1.0, bool loop = false)
        : base(name, LinkType.LogReplay)
    {
        FilePath = filePath;
        Speed = speed;
        Loop = loop;
    }

    public string FilePath { get; }

    public double Speed { get; }

    public bool Loop { get; }
}
