namespace VGC.Analyze;

public sealed record ULogHeader(
    byte Version,
    ulong Timestamp);

public sealed record ULogField(
    string TypeName,
    string FieldName,
    int ArraySize);

public sealed record ULogMessageDefinition(
    string Name,
    IReadOnlyList<ULogField> Fields);

public sealed record ULogDataRow(
    string MessageName,
    ulong Timestamp,
    Dictionary<string, object> Values);

public sealed class ULogParser
{
    private static readonly byte[] ULogMagic = [0x55, 0x4c, 0x6f, 0x67, 0x01, 0x12, 0x35];

    private const byte HeaderSize = 16;
    private const byte MessageHeaderSize = 3;

    // ULog message types
    private const byte MsgTypeFormat = (byte)'F';
    private const byte MsgTypeData = (byte)'D';
    private const byte MsgTypeInfo = (byte)'I';
    private const byte MsgTypeInfoMulti = (byte)'M';
    private const byte MsgTypeParameter = (byte)'P';
    private const byte MsgTypeAddLoggedMessage = (byte)'A';
    private const byte MsgTypeRemoveLoggedMessage = (byte)'R';
    private const byte MsgTypeSync = (byte)'S';
    private const byte MsgTypeDropout = (byte)'O';
    private const byte MsgTypeLogging = (byte)'L';
    private const byte MsgTypeFlagBits = (byte)'B';

    private readonly Dictionary<string, ULogMessageDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly Dictionary<ushort, string> _subscriptions = [];

    public ULogHeader? ParseHeader(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[HeaderSize];
        if (stream.Read(buffer) < HeaderSize)
        {
            return null;
        }

        if (!buffer[..ULogMagic.Length].SequenceEqual(ULogMagic))
        {
            return null;
        }

        var version = buffer[7];
        var timestamp = BitConverter.ToUInt64(buffer[8..]);
        return new ULogHeader(version, timestamp);
    }

    public IReadOnlyList<ULogMessageDefinition> ParseDefinitions(Stream stream)
    {
        _definitions.Clear();
        _subscriptions.Clear();

        Span<byte> msgHeader = stackalloc byte[MessageHeaderSize];

        while (stream.Position < stream.Length)
        {
            if (stream.Read(msgHeader) < MessageHeaderSize)
            {
                break;
            }

            var msgSize = BitConverter.ToUInt16(msgHeader);
            var msgType = msgHeader[2];

            if (msgSize == 0)
            {
                break;
            }

            var payload = new byte[msgSize];
            if (stream.Read(payload) < msgSize)
            {
                break;
            }

            switch (msgType)
            {
                case MsgTypeFormat:
                    ParseFormatMessage(payload);
                    break;

                case MsgTypeAddLoggedMessage:
                    ParseAddLoggedMessage(payload);
                    break;

                case MsgTypeFlagBits:
                case MsgTypeInfo:
                case MsgTypeInfoMulti:
                case MsgTypeParameter:
                    // Skip non-format definition messages
                    break;

                case MsgTypeData:
                case MsgTypeLogging:
                case MsgTypeSync:
                case MsgTypeDropout:
                    // Data section has begun; stop parsing definitions
                    stream.Position -= msgSize + MessageHeaderSize;
                    return _definitions.Values.ToArray();
            }
        }

        return _definitions.Values.ToArray();
    }

    public IEnumerable<ULogDataRow> ParseData(Stream stream, string messageName)
    {
        var msgHeader = new byte[MessageHeaderSize];

        while (stream.Position < stream.Length)
        {
            if (stream.Read(msgHeader, 0, MessageHeaderSize) < MessageHeaderSize)
            {
                yield break;
            }

            var msgSize = BitConverter.ToUInt16(msgHeader, 0);
            var msgType = msgHeader[2];

            if (msgSize == 0)
            {
                yield break;
            }

            var payload = new byte[msgSize];
            if (stream.Read(payload) < msgSize)
            {
                yield break;
            }

            if (msgType != MsgTypeData || payload.Length < 4)
            {
                continue;
            }

            var msgId = BitConverter.ToUInt16(payload, 0);
            if (!_subscriptions.TryGetValue(msgId, out var name) ||
                !string.Equals(name, messageName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!_definitions.TryGetValue(messageName, out var definition))
            {
                continue;
            }

            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var offset = 2; // skip msg_id

            // First 8 bytes of data payload after msg_id is the timestamp
            if (payload.Length < offset + 8)
            {
                continue;
            }

            var timestamp = BitConverter.ToUInt64(payload, offset);
            offset += 8;

            foreach (var field in definition.Fields)
            {
                if (offset >= payload.Length)
                {
                    break;
                }

                var value = ReadFieldValue(payload, ref offset, field);
                if (value is not null)
                {
                    values[field.FieldName] = value;
                }
            }

            yield return new ULogDataRow(messageName, timestamp, values);
        }
    }

    private void ParseFormatMessage(byte[] payload)
    {
        var text = System.Text.Encoding.ASCII.GetString(payload).TrimEnd('\0');
        var colonIndex = text.IndexOf(':');
        if (colonIndex < 0)
        {
            return;
        }

        var messageName = text[..colonIndex];
        var fieldsText = text[(colonIndex + 1)..];
        var fields = new List<ULogField>();

        foreach (var fieldDef in fieldsText.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = fieldDef.Trim();
            var spaceIndex = trimmed.LastIndexOf(' ');
            if (spaceIndex < 0)
            {
                continue;
            }

            var typeName = trimmed[..spaceIndex].Trim();
            var fieldName = trimmed[(spaceIndex + 1)..].Trim();
            var arraySize = 1;

            var bracketIndex = fieldName.IndexOf('[');
            if (bracketIndex >= 0)
            {
                var bracketEnd = fieldName.IndexOf(']', bracketIndex);
                if (bracketEnd > bracketIndex + 1 &&
                    int.TryParse(fieldName[(bracketIndex + 1)..bracketEnd], out var size))
                {
                    arraySize = size;
                }

                fieldName = fieldName[..bracketIndex];
            }

            fields.Add(new ULogField(typeName, fieldName, arraySize));
        }

        _definitions[messageName] = new ULogMessageDefinition(messageName, fields);
    }

    private void ParseAddLoggedMessage(byte[] payload)
    {
        if (payload.Length < 3)
        {
            return;
        }

        // multi_id (1 byte), msg_id (2 bytes), message_name (remaining)
        var msgId = BitConverter.ToUInt16(payload, 1);
        var name = System.Text.Encoding.ASCII.GetString(payload, 3, payload.Length - 3).TrimEnd('\0');
        _subscriptions[msgId] = name;
    }

    private static object? ReadFieldValue(byte[] payload, ref int offset, ULogField field)
    {
        if (offset >= payload.Length)
        {
            return null;
        }

        return field.TypeName switch
        {
            "float" when offset + 4 <= payload.Length => ReadFloat(payload, ref offset),
            "double" when offset + 8 <= payload.Length => ReadDouble(payload, ref offset),
            "int8_t" when offset + 1 <= payload.Length => ReadInt8(payload, ref offset),
            "uint8_t" when offset + 1 <= payload.Length => ReadUInt8(payload, ref offset),
            "int16_t" when offset + 2 <= payload.Length => ReadInt16(payload, ref offset),
            "uint16_t" when offset + 2 <= payload.Length => ReadUInt16(payload, ref offset),
            "int32_t" when offset + 4 <= payload.Length => ReadInt32(payload, ref offset),
            "uint32_t" when offset + 4 <= payload.Length => ReadUInt32(payload, ref offset),
            "int64_t" when offset + 8 <= payload.Length => ReadInt64(payload, ref offset),
            "uint64_t" when offset + 8 <= payload.Length => ReadUInt64(payload, ref offset),
            "bool" when offset + 1 <= payload.Length => ReadBool(payload, ref offset),
            "char" when field.ArraySize > 1 => ReadCharArray(payload, ref offset, field.ArraySize),
            _ => SkipField(payload, ref offset, field)
        };
    }

    private static object ReadFloat(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToSingle(payload, offset);
        offset += 4;
        return value;
    }

    private static object ReadDouble(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToDouble(payload, offset);
        offset += 8;
        return value;
    }

    private static object ReadInt8(byte[] payload, ref int offset)
    {
        var value = (sbyte)payload[offset];
        offset += 1;
        return value;
    }

    private static object ReadUInt8(byte[] payload, ref int offset)
    {
        var value = payload[offset];
        offset += 1;
        return value;
    }

    private static object ReadInt16(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToInt16(payload, offset);
        offset += 2;
        return value;
    }

    private static object ReadUInt16(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToUInt16(payload, offset);
        offset += 2;
        return value;
    }

    private static object ReadInt32(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToInt32(payload, offset);
        offset += 4;
        return value;
    }

    private static object ReadUInt32(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToUInt32(payload, offset);
        offset += 4;
        return value;
    }

    private static object ReadInt64(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToInt64(payload, offset);
        offset += 8;
        return value;
    }

    private static object ReadUInt64(byte[] payload, ref int offset)
    {
        var value = BitConverter.ToUInt64(payload, offset);
        offset += 8;
        return value;
    }

    private static object ReadBool(byte[] payload, ref int offset)
    {
        var value = payload[offset] != 0;
        offset += 1;
        return value;
    }

    private static object ReadCharArray(byte[] payload, ref int offset, int arraySize)
    {
        var length = Math.Min(arraySize, payload.Length - offset);
        var value = System.Text.Encoding.ASCII.GetString(payload, offset, length).TrimEnd('\0');
        offset += length;
        return value;
    }

    private static object? SkipField(byte[] payload, ref int offset, ULogField field)
    {
        var size = GetFieldSize(field.TypeName) * field.ArraySize;
        offset += Math.Min(size, payload.Length - offset);
        return null;
    }

    private static int GetFieldSize(string typeName)
    {
        return typeName switch
        {
            "float" => 4,
            "double" => 8,
            "int8_t" or "uint8_t" or "bool" or "char" => 1,
            "int16_t" or "uint16_t" => 2,
            "int32_t" or "uint32_t" => 4,
            "int64_t" or "uint64_t" => 8,
            _ => 1
        };
    }
}
