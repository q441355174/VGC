using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace VGC.Mavlink.Generated;

public sealed record MavlinkDialectGenerationResult(
    IReadOnlyList<MavlinkMessageDefinition> Definitions,
    IReadOnlyList<MavlinkCrcExtraEntry> CrcEntries)
{
    public bool TryGet(string name, out MavlinkMessageDefinition definition)
    {
        definition = Definitions.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase))!;
        return definition is not null;
    }
}

public static class MavlinkDialectXmlGenerator
{
    private static readonly IReadOnlyDictionary<string, int> ScalarSizes = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["char"] = 1,
        ["uint8_t"] = 1,
        ["int8_t"] = 1,
        ["uint8_t_mavlink_version"] = 1,
        ["uint16_t"] = 2,
        ["int16_t"] = 2,
        ["uint32_t"] = 4,
        ["int32_t"] = 4,
        ["float"] = 4,
        ["uint64_t"] = 8,
        ["int64_t"] = 8,
        ["double"] = 8
    };

    public static MavlinkDialectGenerationResult Generate(string dialectXml, IReadOnlyCollection<string>? requiredMessages = null)
    {
        return Generate(XDocument.Parse(dialectXml, LoadOptions.PreserveWhitespace), requiredMessages);
    }

    public static MavlinkDialectGenerationResult Generate(XDocument document, IReadOnlyCollection<string>? requiredMessages = null)
    {
        var required = requiredMessages is null
            ? null
            : new HashSet<string>(requiredMessages, StringComparer.OrdinalIgnoreCase);

        var definitions = new List<MavlinkMessageDefinition>();
        foreach (var message in document.Descendants("message"))
        {
            var name = RequireAttribute(message, "name");
            if (required is not null && !required.Contains(name))
            {
                continue;
            }

            var fields = ReadFields(message);
            var baseFields = fields.Where(static f => !f.IsExtension).ToArray();
            var allLength = fields.Sum(FieldWireLength);
            var minLength = baseFields.Sum(FieldWireLength);
            definitions.Add(new MavlinkMessageDefinition(
                MessageId: uint.Parse(RequireAttribute(message, "id"), CultureInfo.InvariantCulture),
                Name: name,
                CrcExtra: ComputeCrcExtra(name, baseFields),
                MinPayloadLength: minLength,
                MaxPayloadLength: allLength,
                ClrTypeName: "Mavlink" + ToPascalCase(name),
                Category: InferCategory(name),
                Fields: fields));
        }

        if (required is not null)
        {
            var missing = required
                .Where(name => definitions.All(d => !string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException("Dialect is missing required MAVLink messages: " + string.Join(", ", missing));
            }
        }

        var ordered = definitions.OrderBy(static d => d.MessageId).ToArray();
        var crcEntries = ordered
            .Select(static d => new MavlinkCrcExtraEntry(d.MessageId, d.CrcExtra, MavlinkCrcExtraSource.GeneratedDialectXml, d.Name))
            .ToArray();
        return new MavlinkDialectGenerationResult(ordered, crcEntries);
    }

    private static IReadOnlyList<MavlinkFieldDefinition> ReadFields(XElement message)
    {
        var fields = new List<MavlinkFieldDefinition>();
        var isExtension = false;
        foreach (var element in message.Elements())
        {
            if (element.Name.LocalName == "extensions")
            {
                isExtension = true;
                continue;
            }

            if (element.Name.LocalName != "field")
            {
                continue;
            }

            var parsedType = ParseWireType(RequireAttribute(element, "type"));
            fields.Add(new MavlinkFieldDefinition(
                RequireAttribute(element, "name"),
                parsedType.BaseType,
                parsedType.ArrayLength,
                isExtension));
        }

        return fields;
    }

    private static (string BaseType, int ArrayLength) ParseWireType(string wireType)
    {
        var bracket = wireType.IndexOf('[', StringComparison.Ordinal);
        if (bracket < 0)
        {
            return (wireType, 0);
        }

        var endBracket = wireType.IndexOf(']', bracket);
        if (endBracket <= bracket)
        {
            throw new InvalidOperationException($"Invalid MAVLink array field type '{wireType}'.");
        }

        var baseType = wireType[..bracket];
        var arrayLength = int.Parse(wireType[(bracket + 1)..endBracket], CultureInfo.InvariantCulture);
        return (baseType, arrayLength);
    }

    private static int FieldWireLength(MavlinkFieldDefinition field)
    {
        if (!ScalarSizes.TryGetValue(field.WireType, out var scalarSize))
        {
            throw new InvalidOperationException($"Unsupported MAVLink field type '{field.WireType}'.");
        }

        return scalarSize * Math.Max(field.ArrayLength, 1);
    }

    private static byte ComputeCrcExtra(string messageName, IReadOnlyList<MavlinkFieldDefinition> fields)
    {
        var crc = (ushort)0xFFFF;
        AccumulateString(messageName + " ", ref crc);
        foreach (var field in fields)
        {
            var crcType = field.WireType == "uint8_t_mavlink_version" ? "uint8_t" : field.WireType;
            AccumulateString(crcType + " ", ref crc);
            AccumulateString(field.Name + " ", ref crc);
            if (field.ArrayLength > 0)
            {
                Accumulate((byte)field.ArrayLength, ref crc);
            }
        }

        return (byte)((crc & 0xFF) ^ (crc >> 8));
    }

    private static void AccumulateString(string value, ref ushort crc)
    {
        foreach (var b in Encoding.ASCII.GetBytes(value))
        {
            Accumulate(b, ref crc);
        }
    }

    private static void Accumulate(byte value, ref ushort crc)
    {
        var tmp = (byte)(value ^ (byte)(crc & 0xFF));
        tmp ^= (byte)(tmp << 4);
        crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
    }

    private static MavlinkMessageCategory InferCategory(string name)
    {
        if (name.StartsWith("PARAM_", StringComparison.OrdinalIgnoreCase))
        {
            return MavlinkMessageCategory.Parameter;
        }

        if (name.StartsWith("MISSION_", StringComparison.OrdinalIgnoreCase))
        {
            return MavlinkMessageCategory.Mission;
        }

        if (name.StartsWith("COMMAND_", StringComparison.OrdinalIgnoreCase))
        {
            return MavlinkMessageCategory.Command;
        }

        if (name.Contains("POSITION", StringComparison.OrdinalIgnoreCase) || name == "ATTITUDE")
        {
            return MavlinkMessageCategory.Position;
        }

        if (name == "STATUSTEXT")
        {
            return MavlinkMessageCategory.Status;
        }

        return MavlinkMessageCategory.VehicleState;
    }

    private static string ToPascalCase(string name)
    {
        return string.Concat(name
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static part => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(part.ToLowerInvariant())));
    }

    private static string RequireAttribute(XElement element, string name)
    {
        return element.Attribute(name)?.Value
            ?? throw new InvalidOperationException($"MAVLink XML element '{element.Name}' is missing required '{name}' attribute.");
    }
}
