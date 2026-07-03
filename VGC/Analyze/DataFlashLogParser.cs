namespace VGC.Analyze;

public sealed record DataFlashFormat(
    byte Type,
    byte Length,
    string Name,
    string FormatString,
    string[] ColumnNames);

public sealed record DataFlashMessage(
    byte Type,
    string Name,
    Dictionary<string, object> Values);

public sealed class DataFlashLogParser
{
    private const byte HeaderByte1 = 0xa3;
    private const byte HeaderByte2 = 0x95;
    private const byte FmtMessageType = 128;
    private const int FmtMessageLength = 89;

    public IEnumerable<DataFlashMessage> Parse(Stream stream)
    {
        var formats = new Dictionary<byte, DataFlashFormat>();

        while (stream.Position < stream.Length)
        {
            // Read packet header: 0xa3 0x95 <type>
            var b1 = stream.ReadByte();
            if (b1 < 0)
            {
                yield break;
            }

            if (b1 != HeaderByte1)
            {
                continue;
            }

            var b2 = stream.ReadByte();
            if (b2 < 0)
            {
                yield break;
            }

            if (b2 != HeaderByte2)
            {
                continue;
            }

            var messageType = stream.ReadByte();
            if (messageType < 0)
            {
                yield break;
            }

            var type = (byte)messageType;

            if (type == FmtMessageType)
            {
                var format = ParseFmtMessage(stream);
                if (format is not null)
                {
                    formats[format.Type] = format;
                    yield return new DataFlashMessage(
                        FmtMessageType,
                        "FMT",
                        new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["Type"] = format.Type,
                            ["Length"] = format.Length,
                            ["Name"] = format.Name,
                            ["Format"] = format.FormatString,
                            ["Columns"] = string.Join(",", format.ColumnNames)
                        });
                }
            }
            else if (formats.TryGetValue(type, out var format))
            {
                var message = ParseMessage(stream, format);
                if (message is not null)
                {
                    yield return message;
                }
            }
            else
            {
                // Unknown format type; cannot determine length to skip.
                // Try to find next header marker.
                continue;
            }
        }
    }

    private static DataFlashFormat? ParseFmtMessage(Stream stream)
    {
        // FMT message body (after header bytes and type byte):
        // Type(1) + Length(1) + Name(4) + Format(16) + Columns(64) = 86 bytes
        // Total message is 89 bytes including the 3-byte header we already read.
        var bodyLength = FmtMessageLength - 3;
        var body = new byte[bodyLength];
        if (stream.Read(body, 0, bodyLength) < bodyLength)
        {
            return null;
        }

        var fmtType = body[0];
        var fmtLength = body[1];
        var name = System.Text.Encoding.ASCII.GetString(body, 2, 4).TrimEnd('\0');
        var formatString = System.Text.Encoding.ASCII.GetString(body, 6, 16).TrimEnd('\0');
        var columnsRaw = System.Text.Encoding.ASCII.GetString(body, 22, 64).TrimEnd('\0');
        var columnNames = columnsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return new DataFlashFormat(fmtType, fmtLength, name, formatString, columnNames);
    }

    private static DataFlashMessage? ParseMessage(Stream stream, DataFlashFormat format)
    {
        // Message body length = format.Length - 3 (for the header bytes already read)
        var bodyLength = format.Length - 3;
        if (bodyLength <= 0)
        {
            return null;
        }

        var body = new byte[bodyLength];
        if (stream.Read(body, 0, bodyLength) < bodyLength)
        {
            return null;
        }

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        var offset = 0;

        for (var i = 0; i < format.FormatString.Length && i < format.ColumnNames.Length; i++)
        {
            if (offset >= body.Length)
            {
                break;
            }

            var columnName = format.ColumnNames[i];
            var formatChar = format.FormatString[i];
            var value = ReadValue(body, ref offset, formatChar);
            if (value is not null)
            {
                values[columnName] = value;
            }
        }

        return new DataFlashMessage(format.Type, format.Name, values);
    }

    private static object? ReadValue(byte[] data, ref int offset, char formatChar)
    {
        switch (formatChar)
        {
            case 'b': // int8_t
                if (offset + 1 > data.Length) return null;
                var i8 = (sbyte)data[offset];
                offset += 1;
                return i8;

            case 'B': // uint8_t
                if (offset + 1 > data.Length) return null;
                var u8 = data[offset];
                offset += 1;
                return u8;

            case 'h': // int16_t
                if (offset + 2 > data.Length) return null;
                var i16 = BitConverter.ToInt16(data, offset);
                offset += 2;
                return i16;

            case 'H': // uint16_t
                if (offset + 2 > data.Length) return null;
                var u16 = BitConverter.ToUInt16(data, offset);
                offset += 2;
                return u16;

            case 'i': // int32_t
                if (offset + 4 > data.Length) return null;
                var i32 = BitConverter.ToInt32(data, offset);
                offset += 4;
                return i32;

            case 'I': // uint32_t
                if (offset + 4 > data.Length) return null;
                var u32 = BitConverter.ToUInt32(data, offset);
                offset += 4;
                return u32;

            case 'q': // int64_t
                if (offset + 8 > data.Length) return null;
                var i64 = BitConverter.ToInt64(data, offset);
                offset += 8;
                return i64;

            case 'Q': // uint64_t
                if (offset + 8 > data.Length) return null;
                var u64 = BitConverter.ToUInt64(data, offset);
                offset += 8;
                return u64;

            case 'f': // float
                if (offset + 4 > data.Length) return null;
                var f32 = BitConverter.ToSingle(data, offset);
                offset += 4;
                return f32;

            case 'd': // double
                if (offset + 8 > data.Length) return null;
                var f64 = BitConverter.ToDouble(data, offset);
                offset += 8;
                return f64;

            case 'c': // int16_t * 100 (centi-degrees)
                if (offset + 2 > data.Length) return null;
                var centi = BitConverter.ToInt16(data, offset);
                offset += 2;
                return centi / 100.0;

            case 'C': // uint16_t * 100
                if (offset + 2 > data.Length) return null;
                var ucenti = BitConverter.ToUInt16(data, offset);
                offset += 2;
                return ucenti / 100.0;

            case 'e': // int32_t * 100 (centi-degrees)
                if (offset + 4 > data.Length) return null;
                var ecenti = BitConverter.ToInt32(data, offset);
                offset += 4;
                return ecenti / 100.0;

            case 'E': // uint32_t * 100
                if (offset + 4 > data.Length) return null;
                var uecenti = BitConverter.ToUInt32(data, offset);
                offset += 4;
                return uecenti / 100.0;

            case 'L': // int32_t latitude/longitude (1e-7 degrees)
                if (offset + 4 > data.Length) return null;
                var latlon = BitConverter.ToInt32(data, offset);
                offset += 4;
                return latlon / 1e7;

            case 'M': // uint8_t flight mode
                if (offset + 1 > data.Length) return null;
                var mode = data[offset];
                offset += 1;
                return mode;

            case 'n': // char[4] name
                return ReadFixedString(data, ref offset, 4);

            case 'N': // char[16] name
                return ReadFixedString(data, ref offset, 16);

            case 'Z': // char[64]
                return ReadFixedString(data, ref offset, 64);

            case 'a': // int16_t[32] array
                if (offset + 64 > data.Length) return null;
                var arr = new short[32];
                for (var j = 0; j < 32; j++)
                {
                    arr[j] = BitConverter.ToInt16(data, offset);
                    offset += 2;
                }
                return arr;

            default:
                // Unknown format character; skip one byte
                if (offset < data.Length)
                {
                    offset += 1;
                }
                return null;
        }
    }

    private static string ReadFixedString(byte[] data, ref int offset, int length)
    {
        var available = Math.Min(length, data.Length - offset);
        if (available <= 0)
        {
            return string.Empty;
        }

        var value = System.Text.Encoding.ASCII.GetString(data, offset, available).TrimEnd('\0');
        offset += available;
        return value;
    }
}
