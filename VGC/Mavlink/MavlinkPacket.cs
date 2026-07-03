using VGC.Comms;

namespace VGC.Mavlink;

public sealed record MavlinkPacket(
    ILinkTransport Link,
    byte Version,
    byte SystemId,
    byte ComponentId,
    uint MessageId,
    byte[] Payload);
