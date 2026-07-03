namespace VGC.Mavlink;

public static class MavlinkCrc
{
    public static ushort Accumulate(ReadOnlySpan<byte> bytes, byte crcExtra)
    {
        var crc = (ushort)0xFFFF;
        foreach (var value in bytes)
        {
            Accumulate(value, ref crc);
        }

        Accumulate(crcExtra, ref crc);
        return crc;
    }

    public static bool Matches(ReadOnlySpan<byte> bytes, byte crcExtra, ushort expectedCrc)
    {
        return Accumulate(bytes, crcExtra) == expectedCrc;
    }

    private static void Accumulate(byte value, ref ushort crc)
    {
        var tmp = (byte)(value ^ (byte)(crc & 0xFF));
        tmp ^= (byte)(tmp << 4);
        crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
    }
}
