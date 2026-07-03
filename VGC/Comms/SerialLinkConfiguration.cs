namespace VGC.Comms;

public sealed class SerialLinkConfiguration : LinkConfiguration
{
    public SerialLinkConfiguration(
        string name,
        string portName,
        int baudRate,
        int dataBits = 8,
        string parity = "None",
        string stopBits = "One")
        : base(name, LinkType.Serial)
    {
        PortName = portName;
        BaudRate = baudRate;
        DataBits = dataBits;
        Parity = parity;
        StopBits = stopBits;
    }

    public string PortName { get; }

    public int BaudRate { get; }

    public int DataBits { get; }

    public string Parity { get; }

    public string StopBits { get; }
}
