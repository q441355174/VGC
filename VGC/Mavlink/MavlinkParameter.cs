namespace VGC.Mavlink;

public enum MavlinkParamType : byte
{
    UInt8 = 1,
    Int8 = 2,
    UInt16 = 3,
    Int16 = 4,
    UInt32 = 5,
    Int32 = 6,
    UInt64 = 7,
    Int64 = 8,
    Real32 = 9,
    Real64 = 10
}

public sealed record MavlinkParameterRequestList(
    byte TargetSystemId,
    byte TargetComponentId);

public sealed record MavlinkParameterRequestRead(
    byte TargetSystemId,
    byte TargetComponentId,
    string Name,
    short Index)
{
    public static MavlinkParameterRequestRead ByIndex(byte targetSystemId, byte targetComponentId, short index)
    {
        return new MavlinkParameterRequestRead(targetSystemId, targetComponentId, string.Empty, index);
    }

    public static MavlinkParameterRequestRead ByName(byte targetSystemId, byte targetComponentId, string name)
    {
        return new MavlinkParameterRequestRead(targetSystemId, targetComponentId, name, -1);
    }
}

public sealed record MavlinkParameterSet(
    byte TargetSystemId,
    byte TargetComponentId,
    string Name,
    float Value,
    MavlinkParamType Type = MavlinkParamType.Real32);
