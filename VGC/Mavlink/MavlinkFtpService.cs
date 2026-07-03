using System.Text;
using VGC.Comms;

namespace VGC.Mavlink;

public sealed record FtpDirectoryEntry(
    string Name,
    bool IsDirectory,
    long SizeBytes);

public enum FtpTransferState
{
    Idle,
    Reading,
    Writing,
    Listing,
    Failed
}

public sealed class MavlinkFtpService : IAsyncDisposable
{
    public const uint FileTransferProtocolMessageId = 110;
    public const byte FileTransferProtocolCrcExtra = 84;

    private readonly MavlinkFrameWriter _frameWriter;
    private readonly MavlinkOutboundRouter _outboundRouter;
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly byte _targetSystemId;
    private readonly byte _targetComponentId;
    private readonly byte _targetNetwork;
    private readonly int _maxRetryCount;
    private readonly TimeSpan _retryTimeout;
    private readonly object _syncRoot = new();
    private ushort _nextSequence;
    private byte _currentSession;
    private TaskCompletionSource<MavlinkFtpPacket>? _pendingResponse;
    private CancellationTokenSource? _operationCancellation;

    public MavlinkFtpService(
        byte systemId = 255,
        byte componentId = 190,
        byte targetSystemId = 1,
        byte targetComponentId = 1,
        byte targetNetwork = 0,
        int maxRetryCount = 3,
        TimeSpan? retryTimeout = null,
        MavlinkFrameWriter? frameWriter = null,
        MavlinkOutboundRouter? outboundRouter = null)
    {
        _systemId = systemId;
        _componentId = componentId;
        _targetSystemId = targetSystemId;
        _targetComponentId = targetComponentId;
        _targetNetwork = targetNetwork;
        _maxRetryCount = maxRetryCount;
        _retryTimeout = retryTimeout ?? TimeSpan.FromSeconds(3);
        _outboundRouter = outboundRouter ?? new MavlinkOutboundRouter(frameWriter);
        _frameWriter = _outboundRouter.FrameWriter;
    }

    public event EventHandler<FtpTransferState>? TransferStateChanged;

    public event EventHandler<double>? TransferProgress;

    public FtpTransferState State { get; private set; } = FtpTransferState.Idle;

    public string? LastError { get; private set; }

    public uint BytesTransferred { get; private set; }

    public void HandlePacket(MavlinkPacket packet)
    {
        if (packet.MessageId != FileTransferProtocolMessageId)
        {
            return;
        }

        if (!MavlinkFtpPacket.TryRead(packet.Payload, out var ftpPacket))
        {
            return;
        }

        lock (_syncRoot)
        {
            _pendingResponse?.TrySetResult(ftpPacket);
        }
    }

    public async Task<IReadOnlyList<FtpDirectoryEntry>> ListDirectoryAsync(
        ILinkTransport link,
        string path,
        CancellationToken cancellationToken = default)
    {
        SetState(FtpTransferState.Listing);
        BytesTransferred = 0;
        LastError = null;

        var entries = new List<FtpDirectoryEntry>();
        uint offset = 0;

        try
        {
            while (true)
            {
                var pathBytes = Encoding.UTF8.GetBytes(path);
                var requestPacket = CreatePacket(MavlinkFtpOpcode.ListDirectory, session: 0, offset, pathBytes);
                var response = await SendWithRetryAsync(link, requestPacket, cancellationToken).ConfigureAwait(false);

                if (response.Opcode == MavlinkFtpOpcode.Nak)
                {
                    var error = response.Data.Length > 0 ? (MavlinkFtpNakError)response.Data[0] : MavlinkFtpNakError.Fail;
                    if (error == MavlinkFtpNakError.EndOfFile)
                    {
                        break;
                    }

                    throw new IOException($"FTP ListDirectory NAK: {error}");
                }

                var parsed = ParseDirectoryEntries(response.Data);
                entries.AddRange(parsed);
                offset += (uint)parsed.Count;
            }

            SetState(FtpTransferState.Idle);
            return entries;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(FtpTransferState.Failed);
            throw;
        }
    }

    public async Task<byte[]> ReadFileAsync(
        ILinkTransport link,
        string path,
        CancellationToken cancellationToken = default)
    {
        SetState(FtpTransferState.Reading);
        BytesTransferred = 0;
        LastError = null;

        try
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var openPacket = CreatePacket(MavlinkFtpOpcode.OpenFileReadOnly, session: 0, offset: 0, pathBytes);
            var openResponse = await SendWithRetryAsync(link, openPacket, cancellationToken).ConfigureAwait(false);

            if (openResponse.Opcode == MavlinkFtpOpcode.Nak)
            {
                var error = openResponse.Data.Length > 0 ? (MavlinkFtpNakError)openResponse.Data[0] : MavlinkFtpNakError.Fail;
                throw new IOException($"FTP OpenFileRO NAK: {error}");
            }

            _currentSession = openResponse.Session;
            var fileSize = openResponse.Size > 0 ? BitConverter.ToUInt32(openResponse.Data, 0) : 0u;
            var downloadedBytes = new List<byte>();
            uint readOffset = 0;

            while (true)
            {
                var readPacket = CreatePacket(MavlinkFtpOpcode.ReadFile, _currentSession, readOffset, []);
                var readResponse = await SendWithRetryAsync(link, readPacket, cancellationToken).ConfigureAwait(false);

                if (readResponse.Opcode == MavlinkFtpOpcode.Nak)
                {
                    var error = readResponse.Data.Length > 0 ? (MavlinkFtpNakError)readResponse.Data[0] : MavlinkFtpNakError.Fail;
                    if (error == MavlinkFtpNakError.EndOfFile)
                    {
                        break;
                    }

                    throw new IOException($"FTP ReadFile NAK: {error}");
                }

                downloadedBytes.AddRange(readResponse.Data);
                readOffset += readResponse.Size;
                BytesTransferred = readOffset;

                if (fileSize > 0)
                {
                    TransferProgress?.Invoke(this, Math.Min(1.0, (double)readOffset / fileSize));
                }
            }

            await TerminateSessionAsync(link, _currentSession, cancellationToken).ConfigureAwait(false);
            SetState(FtpTransferState.Idle);
            return downloadedBytes.ToArray();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(FtpTransferState.Failed);
            throw;
        }
    }

    public async Task WriteFileAsync(
        ILinkTransport link,
        string path,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        SetState(FtpTransferState.Writing);
        BytesTransferred = 0;
        LastError = null;

        try
        {
            var pathBytes = Encoding.UTF8.GetBytes(path);
            var createPacket = CreatePacket(MavlinkFtpOpcode.CreateFile, session: 0, offset: 0, pathBytes);
            var createResponse = await SendWithRetryAsync(link, createPacket, cancellationToken).ConfigureAwait(false);

            if (createResponse.Opcode == MavlinkFtpOpcode.Nak)
            {
                var error = createResponse.Data.Length > 0 ? (MavlinkFtpNakError)createResponse.Data[0] : MavlinkFtpNakError.Fail;
                throw new IOException($"FTP CreateFile NAK: {error}");
            }

            _currentSession = createResponse.Session;
            uint writeOffset = 0;
            var totalSize = (uint)data.Length;

            while (writeOffset < totalSize)
            {
                var chunkSize = (int)Math.Min(MavlinkFtpPacket.MaxDataLength, totalSize - writeOffset);
                var chunk = new byte[chunkSize];
                Array.Copy(data, writeOffset, chunk, 0, chunkSize);

                var writePacket = CreatePacket(MavlinkFtpOpcode.WriteFile, _currentSession, writeOffset, chunk);
                var writeResponse = await SendWithRetryAsync(link, writePacket, cancellationToken).ConfigureAwait(false);

                if (writeResponse.Opcode == MavlinkFtpOpcode.Nak)
                {
                    var error = writeResponse.Data.Length > 0 ? (MavlinkFtpNakError)writeResponse.Data[0] : MavlinkFtpNakError.Fail;
                    throw new IOException($"FTP WriteFile NAK: {error}");
                }

                writeOffset += (uint)chunkSize;
                BytesTransferred = writeOffset;
                TransferProgress?.Invoke(this, Math.Min(1.0, (double)writeOffset / totalSize));
            }

            await TerminateSessionAsync(link, _currentSession, cancellationToken).ConfigureAwait(false);
            SetState(FtpTransferState.Idle);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(FtpTransferState.Failed);
            throw;
        }
    }

    public async Task ResetSessionsAsync(ILinkTransport link, CancellationToken cancellationToken = default)
    {
        var packet = CreatePacket(MavlinkFtpOpcode.ResetSessions, session: 0, offset: 0, []);
        await SendWithRetryAsync(link, packet, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            _operationCancellation = null;
            _pendingResponse?.TrySetCanceled();
            _pendingResponse = null;
        }

        await Task.CompletedTask;
    }

    private MavlinkFtpPacket CreatePacket(MavlinkFtpOpcode opcode, byte session, uint offset, byte[] data)
    {
        return new MavlinkFtpPacket(
            Sequence: _nextSequence++,
            Session: session,
            Opcode: opcode,
            Size: (byte)data.Length,
            RequestOpcode: 0,
            BurstComplete: false,
            Offset: offset,
            Data: data);
    }

    private async Task<MavlinkFtpPacket> SendWithRetryAsync(
        ILinkTransport link,
        MavlinkFtpPacket packet,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt <= _maxRetryCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<MavlinkFtpPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_syncRoot)
            {
                _pendingResponse = tcs;
            }

            var payload = packet.ToPayload(_targetNetwork, _targetSystemId, _targetComponentId);
            var frame = _frameWriter.CreateV2Frame(_systemId, _componentId, FileTransferProtocolMessageId, payload, FileTransferProtocolCrcExtra);
            await _outboundRouter.SendFrameAsync(link, frame, "FILE_TRANSFER_PROTOCOL", cancellationToken).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_retryTimeout);

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(_retryTimeout, timeoutCts.Token)).ConfigureAwait(false);
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Timeout, will retry
            }
        }

        throw new TimeoutException($"MAVLink FTP request timed out after {_maxRetryCount + 1} attempts.");
    }

    private async Task TerminateSessionAsync(ILinkTransport link, byte session, CancellationToken cancellationToken)
    {
        try
        {
            var packet = CreatePacket(MavlinkFtpOpcode.TerminateSession, session, offset: 0, []);
            await SendWithRetryAsync(link, packet, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Session termination failure is non-critical.
        }
    }

    private void SetState(FtpTransferState state)
    {
        State = state;
        TransferStateChanged?.Invoke(this, state);
    }

    private static IReadOnlyList<FtpDirectoryEntry> ParseDirectoryEntries(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data).TrimEnd('\0');
        var lines = text.Split(['\0', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var entries = new List<FtpDirectoryEntry>();

        foreach (var line in lines)
        {
            if (line.StartsWith("D", StringComparison.Ordinal))
            {
                var name = line.Length > 1 ? line[1..].Trim() : line;
                entries.Add(new FtpDirectoryEntry(name, IsDirectory: true, SizeBytes: 0));
            }
            else if (line.StartsWith("F", StringComparison.Ordinal))
            {
                var parts = line[1..].Split('\t', StringSplitOptions.TrimEntries);
                var name = parts.Length > 0 ? parts[0] : line;
                var size = parts.Length > 1 && long.TryParse(parts[1], out var s) ? s : 0;
                entries.Add(new FtpDirectoryEntry(name, IsDirectory: false, SizeBytes: size));
            }
            else if (line.StartsWith("S", StringComparison.Ordinal))
            {
                // Skip entry (used for free space reporting); ignore.
            }
            else
            {
                entries.Add(new FtpDirectoryEntry(line, IsDirectory: false, SizeBytes: 0));
            }
        }

        return entries;
    }
}
