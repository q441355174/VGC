using System.Security.Cryptography;

namespace VGC.Mavlink;

public sealed record SigningKey(
    byte[] SecretKey,
    uint LinkId,
    DateTimeOffset InitialTimestamp);

public sealed record MavlinkSignatureBlock(
    byte LinkId,
    ulong Timestamp,
    byte[] Signature);

public sealed record MavlinkSignatureValidationResult(
    bool IsValid,
    string? Error,
    MavlinkSignatureBlock? SignatureBlock);

public sealed class SigningController
{
    public const int SignatureBlockLength = 13;
    private const byte SignedIncompatFlag = 0x01;
    private static readonly DateTimeOffset MavlinkSigningEpoch = new(2015, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Func<DateTimeOffset> _clock;
    private SigningKey? _key;
    private ulong _lastTimestamp;

    public SigningController(Func<DateTimeOffset>? clock = null)
    {
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public bool IsSigningEnabled => _key is not null;

    public void EnableSigning(SigningKey key)
    {
        if (key.SecretKey.Length == 0)
        {
            throw new ArgumentException("Signing secret key cannot be empty.", nameof(key));
        }

        if (key.LinkId > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(key), "MAVLink signing link id must fit in one byte.");
        }

        _key = key with { SecretKey = key.SecretKey.ToArray() };
        _lastTimestamp = ToSigningTimestamp(key.InitialTimestamp);
    }

    public void DisableSigning()
    {
        _key = null;
        _lastTimestamp = 0;
    }

    public byte[] SignFrame(byte[] frame)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("MAVLink signing is not enabled.");
        }

        if (!IsUnsignedV2Frame(frame))
        {
            throw new ArgumentException("Only unsigned MAVLink v2 frames can be signed.", nameof(frame));
        }

        var signed = new byte[frame.Length + SignatureBlockLength];
        Array.Copy(frame, signed, frame.Length);
        signed[2] |= SignedIncompatFlag;
        RecomputeV2Checksum(signed.AsSpan(0, frame.Length));

        var signatureOffset = frame.Length;
        var timestamp = NextTimestamp();
        signed[signatureOffset] = (byte)_key.LinkId;
        WriteUInt48LittleEndian(signed.AsSpan(signatureOffset + 1, 6), timestamp);

        var signature = ComputeSignature(signed.AsSpan(0, signatureOffset + 7), _key.SecretKey);
        signature.CopyTo(signed.AsSpan(signatureOffset + 7, 6));
        return signed;
    }

    public bool ValidateSignature(byte[] frame, int signatureOffset)
    {
        return ValidateSignedFrame(frame, signatureOffset).IsValid;
    }

    public MavlinkSignatureValidationResult ValidateSignedFrame(byte[] frame)
    {
        if (frame.Length < SignatureBlockLength)
        {
            return new MavlinkSignatureValidationResult(false, "Frame is shorter than a MAVLink signature block.", null);
        }

        return ValidateSignedFrame(frame, frame.Length - SignatureBlockLength);
    }

    public MavlinkSignatureValidationResult ValidateSignedFrame(byte[] frame, int signatureOffset)
    {
        if (_key is null)
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink signing is not enabled.", null);
        }

        if (signatureOffset < 12 || signatureOffset + SignatureBlockLength != frame.Length)
        {
            return new MavlinkSignatureValidationResult(false, "Invalid MAVLink signature offset.", null);
        }

        if (frame[0] != 0xFD)
        {
            return new MavlinkSignatureValidationResult(false, "Only MAVLink v2 frames can be signed.", null);
        }

        if ((frame[2] & SignedIncompatFlag) != SignedIncompatFlag)
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink v2 signed incompat flag is missing.", null);
        }

        var payloadLength = frame[1];
        var expectedSignatureOffset = 10 + payloadLength + 2;
        if (signatureOffset != expectedSignatureOffset)
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink signature offset does not match payload length.", null);
        }

        if (!ChecksumMatches(frame.AsSpan(0, signatureOffset)))
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink v2 checksum failed before signature validation.", null);
        }

        var linkId = frame[signatureOffset];
        var timestamp = ReadUInt48LittleEndian(frame.AsSpan(signatureOffset + 1, 6));
        var actualSignature = frame.AsSpan(signatureOffset + 7, 6).ToArray();
        var block = new MavlinkSignatureBlock(linkId, timestamp, actualSignature);

        if (linkId != (byte)_key.LinkId)
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink signing link id does not match.", block);
        }

        var expectedSignature = ComputeSignature(frame.AsSpan(0, signatureOffset + 7), _key.SecretKey);
        if (!CryptographicOperations.FixedTimeEquals(actualSignature, expectedSignature))
        {
            return new MavlinkSignatureValidationResult(false, "MAVLink signature does not match.", block);
        }

        return new MavlinkSignatureValidationResult(true, null, block);
    }

    private ulong NextTimestamp()
    {
        var now = ToSigningTimestamp(_clock());
        if (now <= _lastTimestamp)
        {
            now = _lastTimestamp + 1;
        }

        _lastTimestamp = now;
        return now;
    }

    private static bool IsUnsignedV2Frame(byte[] frame)
    {
        if (frame.Length < 12 || frame[0] != 0xFD || (frame[2] & SignedIncompatFlag) == SignedIncompatFlag)
        {
            return false;
        }

        var payloadLength = frame[1];
        return frame.Length == 10 + payloadLength + 2;
    }

    private static void RecomputeV2Checksum(Span<byte> frame)
    {
        var payloadLength = frame[1];
        var messageId = (uint)(frame[7] | (frame[8] << 8) | (frame[9] << 16));
        if (!MavlinkCrcExtraRegistry.TryGet(messageId, out var crcExtra))
        {
            throw new InvalidOperationException($"No MAVLink CRC extra registered for message {messageId}.");
        }

        var crc = MavlinkCrc.Accumulate(frame.Slice(1, 9 + payloadLength), crcExtra);
        frame[10 + payloadLength] = (byte)(crc & 0xFF);
        frame[11 + payloadLength] = (byte)(crc >> 8);
    }

    private static bool ChecksumMatches(ReadOnlySpan<byte> frame)
    {
        var payloadLength = frame[1];
        var messageId = (uint)(frame[7] | (frame[8] << 8) | (frame[9] << 16));
        if (!MavlinkCrcExtraRegistry.TryGet(messageId, out var crcExtra))
        {
            return false;
        }

        var expectedCrc = (ushort)(frame[10 + payloadLength] | (frame[11 + payloadLength] << 8));
        return MavlinkCrc.Matches(frame.Slice(1, 9 + payloadLength), crcExtra, expectedCrc);
    }

    private static byte[] ComputeSignature(ReadOnlySpan<byte> frameAndSignatureHeader, byte[] secretKey)
    {
        using var sha = SHA256.Create();
        var data = new byte[secretKey.Length + frameAndSignatureHeader.Length];
        secretKey.CopyTo(data, 0);
        frameAndSignatureHeader.CopyTo(data.AsSpan(secretKey.Length));
        return sha.ComputeHash(data).AsSpan(0, 6).ToArray();
    }

    private static ulong ToSigningTimestamp(DateTimeOffset timestamp)
    {
        var elapsed = timestamp.ToUniversalTime() - MavlinkSigningEpoch;
        return elapsed <= TimeSpan.Zero ? 0 : (ulong)(elapsed.Ticks / 100);
    }

    private static void WriteUInt48LittleEndian(Span<byte> destination, ulong value)
    {
        for (var i = 0; i < 6; i++)
        {
            destination[i] = (byte)((value >> (8 * i)) & 0xFF);
        }
    }

    private static ulong ReadUInt48LittleEndian(ReadOnlySpan<byte> source)
    {
        ulong value = 0;
        for (var i = 0; i < 6; i++)
        {
            value |= (ulong)source[i] << (8 * i);
        }

        return value;
    }
}
