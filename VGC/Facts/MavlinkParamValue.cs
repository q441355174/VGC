namespace VGC.Facts;

public sealed record MavlinkParamValue(
    int ComponentId,
    string Name,
    float Value,
    ushort Count,
    ushort Index,
    byte MavParamType);
