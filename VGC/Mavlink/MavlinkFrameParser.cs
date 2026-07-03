namespace VGC.Mavlink;

public sealed class MavlinkFrameParser
{
    private readonly List<byte> _buffer = new();

    public int BufferedByteCount => _buffer.Count;

    public IReadOnlyList<(byte Version, byte Sequence, byte SystemId, byte ComponentId, uint MessageId, byte[] Payload)> Parse(ReadOnlySpan<byte> bytes)
    {
        var frames = new List<(byte Version, byte Sequence, byte SystemId, byte ComponentId, uint MessageId, byte[] Payload)>();
        for (var i = 0; i < bytes.Length; i++)
        {
            _buffer.Add(bytes[i]);
        }

        while (_buffer.Count > 0)
        {
            var startIndex = _buffer.FindIndex(static b => b is 0xFE or 0xFD);
            if (startIndex < 0)
            {
                _buffer.Clear();
                return frames;
            }

            if (startIndex > 0)
            {
                _buffer.RemoveRange(0, startIndex);
            }

            var magic = _buffer[0];
            if (magic == 0xFE)
            {
                if (_buffer.Count < 6)
                {
                    return frames;
                }

                var payloadLength = _buffer[1];
                var frameLength = 6 + payloadLength + 2;
                if (_buffer.Count < frameLength)
                {
                    return frames;
                }

                var systemId = _buffer[3];
                var componentId = _buffer[4];
                var messageId = _buffer[5];
                var sequence = _buffer[2];
                var payload = _buffer.Skip(6).Take(payloadLength).ToArray();
                if (MavlinkCrcExtraRegistry.TryGet(messageId, out var crcExtra))
                {
                    var expectedCrc = (ushort)(_buffer[6 + payloadLength] | (_buffer[7 + payloadLength] << 8));
                    var checksumInput = _buffer.Skip(1).Take(5 + payloadLength).ToArray();
                    if (!MavlinkCrc.Matches(checksumInput, crcExtra, expectedCrc))
                    {
                        _buffer.RemoveRange(0, frameLength);
                        continue;
                    }
                }

                _buffer.RemoveRange(0, frameLength);
                frames.Add((1, sequence, systemId, componentId, messageId, payload));
            }
            else
            {
                if (_buffer.Count < 10)
                {
                    return frames;
                }

                var payloadLength = _buffer[1];
                var incompatFlags = _buffer[2];
                var signatureLength = (incompatFlags & 0x01) == 0x01 ? 13 : 0;
                var frameLength = 10 + payloadLength + 2 + signatureLength;
                if (_buffer.Count < frameLength)
                {
                    return frames;
                }

                var systemId = _buffer[5];
                var componentId = _buffer[6];
                var messageId = (uint)(_buffer[7] | (_buffer[8] << 8) | (_buffer[9] << 16));
                var sequence = _buffer[4];
                var payload = _buffer.Skip(10).Take(payloadLength).ToArray();
                _buffer.RemoveRange(0, frameLength);
                frames.Add((2, sequence, systemId, componentId, messageId, payload));
            }
        }

        return frames;
    }
}
