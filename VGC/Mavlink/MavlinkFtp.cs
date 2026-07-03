using System.Text;

namespace VGC.Mavlink;

public enum MavlinkFtpOpcode : byte
{
    TerminateSession = 1,
    ResetSessions = 2,
    ListDirectory = 3,
    OpenFileReadOnly = 4,
    ReadFile = 5,
    CreateFile = 6,
    WriteFile = 7,
    RemoveFile = 8,
    CreateDirectory = 9,
    RemoveDirectory = 10,
    OpenFileWriteOnly = 11,
    TruncateFile = 12,
    Rename = 13,
    CalcFileCrc32 = 14,
    BurstReadFile = 15,
    Ack = 128,
    Nak = 129
}

public enum MavlinkFtpNakError : byte
{
    None = 0,
    Fail = 1,
    FailErrno = 2,
    InvalidDataSize = 3,
    InvalidSession = 4,
    NoSessionsAvailable = 5,
    EndOfFile = 6,
    UnknownCommand = 7,
    FileExists = 8,
    FileProtected = 9,
    FileNotFound = 10
}

public enum MavlinkFtpTransferState
{
    Idle,
    Listing,
    Opening,
    Downloading,
    Completed,
    Failed
}

public enum MavlinkFtpActionType
{
    None,
    SendRequest,
    RetryRequest,
    Complete,
    Fail
}

public sealed record MavlinkFtpPacket(
    ushort Sequence,
    byte Session,
    MavlinkFtpOpcode Opcode,
    byte Size,
    MavlinkFtpOpcode RequestOpcode,
    bool BurstComplete,
    uint Offset,
    byte[] Data)
{
    public const int PayloadLength = 254;
    public const int DataOffset = 15;
    public const int MaxDataLength = 239;

    public byte[] ToPayload(byte targetNetwork, byte targetSystem, byte targetComponent)
    {
        if (Data.Length > MaxDataLength)
        {
            throw new ArgumentOutOfRangeException(nameof(Data), "MAVLink FTP data exceeds payload capacity.");
        }

        if (Size > Data.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(Size), "MAVLink FTP size cannot exceed data length.");
        }

        var payload = new byte[PayloadLength];
        payload[0] = targetNetwork;
        payload[1] = targetSystem;
        payload[2] = targetComponent;
        BitConverter.GetBytes(Sequence).CopyTo(payload, 3);
        payload[5] = Session;
        payload[6] = (byte)Opcode;
        payload[7] = Size;
        payload[8] = (byte)RequestOpcode;
        payload[9] = BurstComplete ? (byte)1 : (byte)0;
        BitConverter.GetBytes(Offset).CopyTo(payload, 11);
        Array.Copy(Data, 0, payload, DataOffset, Size);
        return payload;
    }

    public static bool TryRead(ReadOnlySpan<byte> payload, out MavlinkFtpPacket packet)
    {
        packet = default!;
        if (payload.Length < PayloadLength)
        {
            return false;
        }

        var size = payload[7];
        if (size > MaxDataLength)
        {
            return false;
        }

        packet = new MavlinkFtpPacket(
            Sequence: BitConverter.ToUInt16(payload.Slice(3, 2)),
            Session: payload[5],
            Opcode: (MavlinkFtpOpcode)payload[6],
            Size: size,
            RequestOpcode: (MavlinkFtpOpcode)payload[8],
            BurstComplete: payload[9] != 0,
            Offset: BitConverter.ToUInt32(payload.Slice(11, 4)),
            Data: payload.Slice(DataOffset, size).ToArray());
        return true;
    }
}

public sealed record MavlinkFtpAction(
    MavlinkFtpActionType Type,
    MavlinkFtpPacket? Packet,
    string? Error = null);

public sealed record MavlinkFtpSnapshot(
    MavlinkFtpTransferState State,
    string? Path,
    byte Session,
    uint Offset,
    int RetryCount,
    string? Error,
    IReadOnlyList<string> DirectoryEntries,
    byte[] DownloadedBytes);

public sealed class MavlinkFtpClient
{
    private readonly int _maxRetryCount;
    private ushort _nextSequence;
    private MavlinkFtpPacket? _pendingRequest;
    private readonly List<string> _directoryEntries = [];
    private readonly List<byte> _downloadedBytes = [];

    public MavlinkFtpClient(int maxRetryCount = 3)
    {
        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount));
        }

        _maxRetryCount = maxRetryCount;
    }

    public MavlinkFtpTransferState State { get; private set; } = MavlinkFtpTransferState.Idle;

    public string? Path { get; private set; }

    public byte Session { get; private set; }

    public uint Offset { get; private set; }

    public int RetryCount { get; private set; }

    public string? Error { get; private set; }

    public MavlinkFtpAction RequestListDirectory(string path)
    {
        ResetForRequest(MavlinkFtpTransferState.Listing, path);
        return Send(MavlinkFtpOpcode.ListDirectory, session: 0, offset: 0, Encoding.UTF8.GetBytes(path));
    }

    public MavlinkFtpAction RequestDownload(string path)
    {
        ResetForRequest(MavlinkFtpTransferState.Opening, path);
        return Send(MavlinkFtpOpcode.OpenFileReadOnly, session: 0, offset: 0, Encoding.UTF8.GetBytes(path));
    }

    public MavlinkFtpAction HandlePacket(MavlinkFtpPacket packet)
    {
        if (_pendingRequest is null || packet.Sequence != _pendingRequest.Sequence)
        {
            return new MavlinkFtpAction(MavlinkFtpActionType.None, null);
        }

        if (packet.Opcode == MavlinkFtpOpcode.Nak)
        {
            return HandleNak(packet);
        }

        if (packet.Opcode != MavlinkFtpOpcode.Ack)
        {
            return new MavlinkFtpAction(MavlinkFtpActionType.None, null);
        }

        RetryCount = 0;
        var requestOpcode = packet.RequestOpcode;
        _pendingRequest = null;

        if (requestOpcode == MavlinkFtpOpcode.ListDirectory)
        {
            _directoryEntries.Clear();
            _directoryEntries.AddRange(ParseDirectoryEntries(packet.Data));
            State = MavlinkFtpTransferState.Completed;
            return new MavlinkFtpAction(MavlinkFtpActionType.Complete, null);
        }

        if (requestOpcode == MavlinkFtpOpcode.OpenFileReadOnly)
        {
            Session = packet.Session;
            State = MavlinkFtpTransferState.Downloading;
            return Send(MavlinkFtpOpcode.ReadFile, Session, offset: 0, []);
        }

        if (requestOpcode == MavlinkFtpOpcode.ReadFile)
        {
            _downloadedBytes.AddRange(packet.Data);
            Offset += packet.Size;
            if (packet.Size == 0 || packet.BurstComplete)
            {
                State = MavlinkFtpTransferState.Completed;
                return new MavlinkFtpAction(MavlinkFtpActionType.Complete, null);
            }

            return Send(MavlinkFtpOpcode.ReadFile, Session, Offset, []);
        }

        return new MavlinkFtpAction(MavlinkFtpActionType.None, null);
    }

    public MavlinkFtpAction RetryPending()
    {
        if (_pendingRequest is null)
        {
            return new MavlinkFtpAction(MavlinkFtpActionType.None, null);
        }

        if (RetryCount >= _maxRetryCount)
        {
            State = MavlinkFtpTransferState.Failed;
            Error = $"MAVLink FTP retry limit ({_maxRetryCount}) exceeded.";
            return new MavlinkFtpAction(MavlinkFtpActionType.Fail, null, Error);
        }

        RetryCount++;
        return new MavlinkFtpAction(MavlinkFtpActionType.RetryRequest, _pendingRequest);
    }

    public MavlinkFtpSnapshot Snapshot()
    {
        return new MavlinkFtpSnapshot(
            State,
            Path,
            Session,
            Offset,
            RetryCount,
            Error,
            _directoryEntries.ToArray(),
            _downloadedBytes.ToArray());
    }

    private MavlinkFtpAction Send(MavlinkFtpOpcode opcode, byte session, uint offset, byte[] data)
    {
        var packet = new MavlinkFtpPacket(
            Sequence: _nextSequence++,
            Session: session,
            Opcode: opcode,
            Size: (byte)data.Length,
            RequestOpcode: 0,
            BurstComplete: false,
            Offset: offset,
            Data: data);
        _pendingRequest = packet;
        return new MavlinkFtpAction(MavlinkFtpActionType.SendRequest, packet);
    }

    private MavlinkFtpAction HandleNak(MavlinkFtpPacket packet)
    {
        var error = packet.Data.Length == 0 ? MavlinkFtpNakError.Fail : (MavlinkFtpNakError)packet.Data[0];
        if (_pendingRequest?.Opcode == MavlinkFtpOpcode.ReadFile && error == MavlinkFtpNakError.EndOfFile)
        {
            RetryCount = 0;
            _pendingRequest = null;
            State = MavlinkFtpTransferState.Completed;
            return new MavlinkFtpAction(MavlinkFtpActionType.Complete, null);
        }

        State = MavlinkFtpTransferState.Failed;
        Error = $"MAVLink FTP NAK: {error}.";
        _pendingRequest = null;
        return new MavlinkFtpAction(MavlinkFtpActionType.Fail, null, Error);
    }

    private void ResetForRequest(MavlinkFtpTransferState state, string path)
    {
        State = state;
        Path = path;
        Session = 0;
        Offset = 0;
        RetryCount = 0;
        Error = null;
        _pendingRequest = null;
        _directoryEntries.Clear();
        _downloadedBytes.Clear();
    }

    private static IReadOnlyList<string> ParseDirectoryEntries(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data).TrimEnd('\0');
        return text
            .Split(['\0', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }
}
