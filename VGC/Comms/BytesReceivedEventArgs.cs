namespace VGC.Comms;

public sealed class BytesReceivedEventArgs : EventArgs
{
    public BytesReceivedEventArgs(ILinkTransport link, byte[] bytes)
    {
        Link = link;
        Bytes = bytes;
    }

    public ILinkTransport Link { get; }

    public byte[] Bytes { get; }
}
