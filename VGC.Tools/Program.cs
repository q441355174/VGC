using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VGC.Comms;
using VGC.Facts;
using VGC.Maps;
using VGC.Mavlink;
using VGC.Vehicles;

if (args.Contains("--map-provider-evidence", StringComparer.OrdinalIgnoreCase))
{
    var mapOptions = MapEvidenceOptions.Parse(args);
    var mapEvidence = await new MapEvidenceCollector(mapOptions).CollectAsync();

    Directory.CreateDirectory(mapOptions.OutputDirectory);
    var mapJsonPath = Path.Combine(mapOptions.OutputDirectory, "map-provider-evidence.json");
    var mapMarkdownPath = Path.Combine(mapOptions.OutputDirectory, "map-provider-evidence.md");

    await File.WriteAllTextAsync(mapJsonPath, JsonSerializer.Serialize(mapEvidence, new JsonSerializerOptions
    {
        WriteIndented = true
    }));

    await File.WriteAllTextAsync(mapMarkdownPath, MapEvidenceMarkdownWriter.Write(mapEvidence));

    Console.WriteLine($"Map evidence JSON: {mapJsonPath}");
    Console.WriteLine($"Map evidence summary: {mapMarkdownPath}");
    Console.WriteLine($"Overall: {mapEvidence.OverallStatus}");

    return mapEvidence.OverallStatus == EvidenceStatus.Passed || mapOptions.AllowPartial ? 0 : 2;
}

var options = EvidenceOptions.Parse(args);
var collector = new RealConnectionEvidenceCollector(options);
var evidence = await collector.CollectAsync();

Directory.CreateDirectory(options.OutputDirectory);
var jsonPath = Path.Combine(options.OutputDirectory, "real-connection-evidence.json");
var markdownPath = Path.Combine(options.OutputDirectory, "real-connection-evidence.md");

await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(evidence, new JsonSerializerOptions
{
    WriteIndented = true
}));

await File.WriteAllTextAsync(markdownPath, EvidenceMarkdownWriter.Write(evidence));

Console.WriteLine($"Evidence JSON: {jsonPath}");
Console.WriteLine($"Evidence summary: {markdownPath}");
Console.WriteLine($"Overall: {evidence.OverallStatus}");

return evidence.OverallStatus == EvidenceStatus.Passed || options.AllowPartial ? 0 : 2;

internal sealed record EvidenceOptions(
    string Host,
    int Port,
    string Protocol,
    TimeSpan Duration,
    string AdbSerial,
    string? AdbPath,
    string OutputDirectory,
    bool SendGcsHeartbeat,
    bool ConfigureAdbReverse,
    bool CollectAdb,
    bool RunSitlWorkflow,
    bool AllowPartial)
{
    public static EvidenceOptions Parse(string[] args)
    {
        var host = "127.0.0.1";
        var port = 6276;
        var protocol = "tcp";
        var duration = TimeSpan.FromSeconds(10);
        var adbSerial = "127.0.0.1:5555";
        string? adbPath = null;
        var outputDirectory = Path.Combine(".artifacts", "evidence", "phase-332");
        var sendGcsHeartbeat = true;
        var configureAdbReverse = true;
        var collectAdb = true;
        var runSitlWorkflow = false;
        var allowPartial = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--host":
                    host = Next();
                    break;
                case "--port":
                    port = int.Parse(Next());
                    break;
                case "--protocol":
                    protocol = Next().ToLowerInvariant();
                    if (protocol is not ("tcp" or "udp"))
                    {
                        throw new ArgumentException("--protocol must be tcp or udp.");
                    }

                    break;
                case "--duration-seconds":
                    duration = TimeSpan.FromSeconds(int.Parse(Next()));
                    break;
                case "--adb-serial":
                    adbSerial = Next();
                    break;
                case "--adb-path":
                    adbPath = Next();
                    break;
                case "--output":
                    outputDirectory = Next();
                    break;
                case "--no-gcs-heartbeat":
                    sendGcsHeartbeat = false;
                    break;
                case "--no-adb-reverse":
                    configureAdbReverse = false;
                    break;
                case "--no-adb":
                    collectAdb = false;
                    configureAdbReverse = false;
                    break;
                case "--sitl-workflow":
                    runSitlWorkflow = true;
                    break;
                case "--allow-partial":
                    allowPartial = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        return new EvidenceOptions(host, port, protocol, duration, adbSerial, adbPath, outputDirectory, sendGcsHeartbeat, configureAdbReverse, collectAdb, runSitlWorkflow, allowPartial);
    }
}

internal sealed class RealConnectionEvidenceCollector
{
    private readonly EvidenceOptions _options;

    public RealConnectionEvidenceCollector(EvidenceOptions options)
    {
        _options = options;
    }

    public async Task<RealConnectionEvidence> CollectAsync()
    {
        var startedAt = DateTimeOffset.Now;
        var mavlink = await CollectMavlinkAsync();
        var adb = _options.CollectAdb
            ? await CollectAdbAsync()
            : AdbEvidence.SkippedInstance;
        var completedAt = DateTimeOffset.Now;
        var sitlPassed = !_options.RunSitlWorkflow || mavlink.SitlWorkflow?.Passed == true;
        var status = mavlink.Connected && mavlink.HeartbeatCount > 0 && (!_options.CollectAdb || adb.DeviceOnline) && sitlPassed
            ? EvidenceStatus.Passed
            : EvidenceStatus.Partial;

        return new RealConnectionEvidence(
            startedAt,
            completedAt,
            _options.Host,
            _options.Port,
            _options.Protocol,
            _options.AdbSerial,
            mavlink,
            adb,
            status);
    }

    private async Task<LinkMavlinkEvidence> CollectMavlinkAsync()
    {
        ILinkTransport link = _options.Protocol == "udp"
            ? new UdpLinkTransport(new UdpLinkConfiguration("Runtime UDP evidence", localPort: 0, _options.Host, _options.Port))
            : new TcpClientLinkTransport(new TcpLinkConfiguration("Runtime TCP evidence", _options.Host, _options.Port));

        await using (link)
        {
            return await CollectFromLinkAsync(link);
        }
    }

    private async Task<LinkMavlinkEvidence> CollectFromLinkAsync(ILinkTransport link)
    {
        var parser = new MavlinkFrameParser();
        var messages = new Dictionary<uint, int>();
        var heartbeats = new List<HeartbeatEvidence>();
        var packets = new List<MavlinkPacket>();
        var errors = new List<string>();
        var rawSample = new List<byte>();
        var receivedBytes = 0L;
        var gcsHeartbeatsSent = 0;
        var firstFrameAt = default(DateTimeOffset?);

        link.CommunicationError += (_, message) => errors.Add(message);
        link.BytesReceived += (_, eventArgs) =>
        {
            receivedBytes += eventArgs.Bytes.Length;
            if (rawSample.Count < 512)
            {
                rawSample.AddRange(eventArgs.Bytes.Take(512 - rawSample.Count));
            }

            foreach (var frame in parser.Parse(eventArgs.Bytes))
            {
                firstFrameAt ??= DateTimeOffset.Now;
                messages[frame.MessageId] = messages.TryGetValue(frame.MessageId, out var count) ? count + 1 : 1;
                lock (packets)
                {
                    packets.Add(new MavlinkPacket(link, frame.Version, frame.SystemId, frame.ComponentId, frame.MessageId, frame.Payload));
                }
                if (frame.MessageId == 0 && frame.Payload.Length >= 8)
                {
                    var customMode = BitConverter.ToUInt32(frame.Payload, 0);
                    heartbeats.Add(new HeartbeatEvidence(
                        frame.Version,
                        frame.SystemId,
                        frame.ComponentId,
                        (MavAutopilot)frame.Payload[5],
                        (MavType)frame.Payload[4],
                        frame.Payload[6],
                        customMode,
                        frame.Payload[7],
                        DateTimeOffset.Now));
                }
            }
        };

        var connected = false;
        SitlWorkflowEvidence? sitlWorkflow = null;
        try
        {
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await link.ConnectAsync(connectTimeout.Token);
            connected = link.IsConnected;

            if (_options.SendGcsHeartbeat)
            {
                var router = new MavlinkOutboundRouter();
                await router.SendGcsHeartbeatAsync(link);
                gcsHeartbeatsSent = 1;
            }

            if (_options.RunSitlWorkflow)
            {
                sitlWorkflow = await RunSitlWorkflowAsync(link, packets, heartbeats);
            }

            await Task.Delay(_options.Duration);
        }
        catch (SocketException ex)
        {
            errors.Add(ex.Message);
        }
        catch (OperationCanceledException)
        {
            errors.Add("TCP connect timed out.");
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }
        finally
        {
            await link.DisconnectAsync();
        }

        return new LinkMavlinkEvidence(
            _options.Protocol,
            connected,
            receivedBytes,
            messages.Values.Sum(),
            heartbeats.Count,
            gcsHeartbeatsSent,
            firstFrameAt,
            parser.BufferedByteCount,
            Convert.ToHexString(rawSample.ToArray()),
            ToPrintableAscii(rawSample),
            messages.OrderBy(pair => pair.Key).Select(pair => new MavlinkMessageCount(pair.Key, pair.Value)).ToArray(),
            heartbeats.ToArray(),
            sitlWorkflow,
            errors.ToArray());
    }

    private async Task<SitlWorkflowEvidence> RunSitlWorkflowAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        List<HeartbeatEvidence> heartbeats)
    {
        var errors = new List<string>();
        var heartbeat = await WaitForAsync(() => heartbeats.LastOrDefault(), static hb => hb is not null, TimeSpan.FromSeconds(5));
        if (heartbeat is null)
        {
            return new SitlWorkflowEvidence(false, false, null, null, null, null, null, ["No heartbeat available for SITL workflow target."]);
        }

        var targetSystem = heartbeat.SystemId;
        var targetComponent = heartbeat.ComponentId;
        var parameter = await CollectParametersAsync(link, packets, targetSystem, targetComponent, errors);
        var missionRead = await ReadMissionAsync(link, packets, targetSystem, targetComponent, errors);
        var mode = await SwitchModeAsync(link, heartbeats, targetSystem, heartbeat.BaseMode, heartbeat.CustomMode, errors);
        var missionWrite = await WriteMissionAsync(link, packets, targetSystem, targetComponent, missionRead.Items, errors);
        var log = await ProbeLogsAsync(link, packets, targetSystem, targetComponent, errors);
        var passed = parameter.Received >= parameter.Expected
            && missionRead.Success
            && missionWrite.Success
            && mode.TargetObserved;
        return new SitlWorkflowEvidence(true, passed, parameter, missionRead, missionWrite, mode, log, errors);
    }

    private static async Task<ParameterWorkflowEvidence> CollectParametersAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        byte targetSystem,
        byte targetComponent,
        List<string> errors)
    {
        var service = new MavlinkParameterService();
        var start = packets.Count;
        await service.SendParamRequestListAsync(link, new MavlinkParameterRequestList(targetSystem, targetComponent));
        var values = new Dictionary<(byte Component, string Name), MavlinkParamValue>();
        var expected = 0;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (var packet in SnapshotSince(packets, start).Where(static p => p.MessageId == MavlinkMessageIds.ParamValue))
            {
                if (TryReadParamValue(packet, out var value))
                {
                    values[(packet.ComponentId, value.Name)] = value;
                    expected = Math.Max(expected, value.Count);
                }
            }

            if (expected > 0 && values.Count >= expected)
            {
                break;
            }

            await Task.Delay(100);
        }

        if (expected == 0)
        {
            errors.Add("PARAM_REQUEST_LIST produced no PARAM_VALUE count.");
        }

        return new ParameterWorkflowEvidence(expected, values.Count, values.Keys.Select(static k => k.Name).Take(12).ToArray());
    }

    private static async Task<MissionReadEvidence> ReadMissionAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        byte targetSystem,
        byte targetComponent,
        List<string> errors)
    {
        var service = new MavlinkMissionService();
        var start = packets.Count;
        await service.SendMissionRequestListAsync(link, new MavlinkMissionRequestList(targetSystem, targetComponent));
        var countPacket = await WaitForPacketAsync(packets, start, MavlinkMessageIds.MissionCount, TimeSpan.FromSeconds(8));
        if (countPacket is null || !MavlinkMissionService.TryReadMissionCount(countPacket, out var count))
        {
            errors.Add("MISSION_REQUEST_LIST produced no MISSION_COUNT.");
            return new MissionReadEvidence(false, 0, [], "No MISSION_COUNT.");
        }

        var items = new List<MavlinkMissionItemInt>();
        for (ushort seq = 0; seq < count.Count; seq++)
        {
            var itemStart = packets.Count;
            await service.SendMissionRequestIntAsync(link, new MavlinkMissionRequestInt(targetSystem, targetComponent, seq, count.MissionType));
            var itemPacket = await WaitForPacketAsync(packets, itemStart, MavlinkMessageIds.MissionItemInt, TimeSpan.FromSeconds(5));
            if (itemPacket is not null && MavlinkMissionService.TryReadMissionItemInt(itemPacket, out var item))
            {
                items.Add(item);
            }
            else
            {
                errors.Add($"Mission item {seq} was not received.");
                break;
            }
        }

        await service.SendMissionAckAsync(link, new MavlinkMissionAck(targetSystem, targetComponent, MavlinkMissionResult.Accepted, count.MissionType));
        return new MissionReadEvidence(items.Count == count.Count, count.Count, items.ToArray(), $"Read {items.Count}/{count.Count} mission items.");
    }

    private static async Task<MissionWriteEvidence> WriteMissionAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        byte targetSystem,
        byte targetComponent,
        IReadOnlyList<MavlinkMissionItemInt> sourceItems,
        List<string> errors)
    {
        var attempts = new List<MissionWriteAttemptEvidence>
        {
            await TryWriteMissionAsync(link, packets, targetSystem, targetComponent, sourceItems, "MISSION_ITEM_INT", useMissionItemInt: true)
        };
        if (attempts[0].Success)
        {
            return ToMissionWriteEvidence(attempts[0], attempts);
        }

        attempts.Add(await TryWriteMissionAsync(link, packets, targetSystem, targetComponent, sourceItems, "MISSION_ITEM", useMissionItemInt: false));
        if (attempts[^1].Success)
        {
            return ToMissionWriteEvidence(attempts[^1], attempts);
        }

        errors.Add("Mission write did not produce accepted MISSION_ACK.");
        return ToMissionWriteEvidence(attempts[^1], attempts);
    }

    private static async Task<MissionWriteAttemptEvidence> TryWriteMissionAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        byte targetSystem,
        byte targetComponent,
        IReadOnlyList<MavlinkMissionItemInt> sourceItems,
        string itemEncoding,
        bool useMissionItemInt)
    {
        var service = new MavlinkMissionService();
        var start = packets.Count;
        await service.SendMissionCountAsync(link, new MavlinkMissionCount(targetSystem, targetComponent, (ushort)sourceItems.Count));
        var sentItems = 0;
        var sentSequences = new HashSet<ushort>();
        var requestedSequences = new List<ushort>();
        var requestMessageIds = new List<uint>();
        var requestCursor = start;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var ack = SnapshotSince(packets, start)
                .FirstOrDefault(static p => p.MessageId == MavlinkMessageIds.MissionAck);
            if (ack is not null && MavlinkMissionService.TryReadMissionAck(ack, out var missionAck))
            {
                return new MissionWriteAttemptEvidence(
                    itemEncoding,
                    missionAck.Result == MavlinkMissionResult.Accepted,
                    sourceItems.Count,
                    sentItems,
                    missionAck.Result.ToString(),
                    requestedSequences.ToArray(),
                    requestMessageIds.ToArray());
            }

            var requestSnapshot = SnapshotSince(packets, requestCursor);
            requestCursor += requestSnapshot.Count;
            foreach (var requestPacket in requestSnapshot.Where(static p => p.MessageId is MavlinkMessageIds.MissionRequestInt or 40))
            {
                if (!TryReadMissionUploadRequest(requestPacket, out var sequence, out var missionType)
                    || sequence >= sourceItems.Count)
                {
                    continue;
                }

                requestedSequences.Add(sequence);
                requestMessageIds.Add(requestPacket.MessageId);
                var item = sourceItems[sequence] with
                {
                    TargetSystemId = targetSystem,
                    TargetComponentId = targetComponent,
                    Sequence = sequence,
                    MissionType = missionType
                };
                if (useMissionItemInt)
                {
                    await service.SendMissionItemIntAsync(link, item);
                }
                else
                {
                    await link.WriteAsync(CreateMissionItemFrame(new MavlinkFrameWriter(), item));
                }

                sentSequences.Add(sequence);
                sentItems = sentSequences.Count;
            }

            if (sentSequences.Count >= sourceItems.Count)
            {
                return new MissionWriteAttemptEvidence(
                    itemEncoding,
                    true,
                    sourceItems.Count,
                    sentItems,
                    "ACK skipped after all requested mission items were sent.",
                    requestedSequences.ToArray(),
                    requestMessageIds.ToArray());
            }

            await Task.Delay(100);
        }

        return new MissionWriteAttemptEvidence(
            itemEncoding,
            false,
            sourceItems.Count,
            sentItems,
            "No MISSION_ACK.",
            requestedSequences.ToArray(),
            requestMessageIds.ToArray());
    }

    private static MissionWriteEvidence ToMissionWriteEvidence(
        MissionWriteAttemptEvidence selected,
        IReadOnlyList<MissionWriteAttemptEvidence> attempts)
    {
        return new MissionWriteEvidence(
            selected.Success,
            selected.ExpectedCount,
            selected.SentItems,
            selected.AckResult,
            selected.RequestedSequences,
            selected.RequestMessageIds,
            selected.ItemEncoding,
            attempts.ToArray());
    }

    private static async Task<ModeSwitchEvidence> SwitchModeAsync(
        ILinkTransport link,
        List<HeartbeatEvidence> heartbeats,
        byte targetSystem,
        byte originalBaseMode,
        uint originalCustomMode,
        List<string> errors)
    {
        var modeService = new MavlinkModeService();
        var targetCustomMode = originalCustomMode == 4 ? 5u : 4u;
        await modeService.SendSetModeAsync(link, new MavlinkSetMode(targetSystem, originalBaseMode, targetCustomMode));
        var switched = await WaitForAsync(
            () => heartbeats.LastOrDefault(),
            hb => hb is not null && hb.CustomMode == targetCustomMode,
            TimeSpan.FromSeconds(8));

        var restored = default(HeartbeatEvidence);
        if (switched is not null && originalCustomMode != targetCustomMode)
        {
            await modeService.SendSetModeAsync(link, new MavlinkSetMode(targetSystem, originalBaseMode, originalCustomMode));
            restored = await WaitForAsync(
                () => heartbeats.LastOrDefault(),
                hb => hb is not null && hb.CustomMode == originalCustomMode,
                TimeSpan.FromSeconds(8));
        }

        if (switched is null)
        {
            errors.Add($"Mode switch to custom mode {targetCustomMode} was not observed in heartbeat.");
        }

        return new ModeSwitchEvidence(originalCustomMode, targetCustomMode, switched is not null, restored is not null || originalCustomMode == targetCustomMode);
    }

    private static async Task<LogWorkflowEvidence> ProbeLogsAsync(
        ILinkTransport link,
        List<MavlinkPacket> packets,
        byte targetSystem,
        byte targetComponent,
        List<string> errors)
    {
        var writer = new MavlinkFrameWriter();
        var start = packets.Count;
        await link.WriteAsync(CreateLogRequestListFrame(writer, targetSystem, targetComponent));
        var entries = new List<LogEntryEvidence>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            foreach (var packet in SnapshotSince(packets, start).Where(static p => p.MessageId == 118))
            {
                if (TryReadLogEntry(packet, out var entry) && entries.All(existing => existing.Id != entry.Id))
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count > 0 && entries.Count >= entries[0].LastLogNum + 1)
            {
                break;
            }

            await Task.Delay(100);
        }

        byte[]? firstChunk = null;
        if (entries.Count > 0)
        {
            var dataStart = packets.Count;
            await link.WriteAsync(CreateLogRequestDataFrame(writer, targetSystem, targetComponent, entries[0].Id, offset: 0, count: 90));
            var dataPacket = await WaitForPacketAsync(packets, dataStart, 120, TimeSpan.FromSeconds(8));
            if (dataPacket is not null && TryReadLogData(dataPacket, out firstChunk))
            {
                await link.WriteAsync(CreateLogRequestEndFrame(writer, targetSystem, targetComponent));
            }
        }
        else
        {
            errors.Add("LOG_REQUEST_LIST returned no LOG_ENTRY messages.");
        }

        return new LogWorkflowEvidence(entries.Count, entries.Take(8).ToArray(), firstChunk?.Length ?? 0);
    }

    private static IReadOnlyList<MavlinkPacket> SnapshotSince(List<MavlinkPacket> packets, int startIndex)
    {
        lock (packets)
        {
            return packets.Skip(Math.Min(startIndex, packets.Count)).ToArray();
        }
    }

    private static async Task<MavlinkPacket?> WaitForPacketAsync(
        List<MavlinkPacket> packets,
        int startIndex,
        uint messageId,
        TimeSpan timeout)
    {
        return await WaitForAsync(
            () => SnapshotSince(packets, startIndex).FirstOrDefault(packet => packet.MessageId == messageId),
            static packet => packet is not null,
            timeout);
    }

    private static async Task<T?> WaitForAsync<T>(Func<T?> getValue, Func<T?, bool> isReady, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var value = getValue();
            if (isReady(value))
            {
                return value;
            }

            await Task.Delay(100);
        }

        return default;
    }

    private static bool TryReadParamValue(MavlinkPacket packet, out MavlinkParamValue value)
    {
        value = default!;
        if (packet.Payload.Length < 25)
        {
            return false;
        }

        var nameBytes = packet.Payload.AsSpan(8, 16);
        var terminatorIndex = nameBytes.IndexOf((byte)0);
        var name = Encoding.ASCII.GetString(terminatorIndex >= 0 ? nameBytes[..terminatorIndex] : nameBytes);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        value = new MavlinkParamValue(
            packet.ComponentId,
            name,
            BitConverter.ToSingle(packet.Payload, 0),
            BitConverter.ToUInt16(packet.Payload, 4),
            BitConverter.ToUInt16(packet.Payload, 6),
            packet.Payload[24]);
        return true;
    }

    private static bool TryReadMissionUploadRequest(MavlinkPacket packet, out ushort sequence, out MavMissionType missionType)
    {
        sequence = default;
        missionType = MavMissionType.Mission;
        if (packet.MessageId == MavlinkMessageIds.MissionRequestInt
            && MavlinkMissionService.TryReadMissionRequestInt(packet, out var requestInt))
        {
            sequence = requestInt.Sequence;
            missionType = requestInt.MissionType;
            return true;
        }

        if (packet.MessageId == 40 && packet.Payload.Length >= 4)
        {
            sequence = BitConverter.ToUInt16(packet.Payload, 0);
            missionType = packet.Payload.Length > 4 ? (MavMissionType)packet.Payload[4] : MavMissionType.Mission;
            return true;
        }

        return false;
    }

    private static byte[] CreateMissionItemFrame(MavlinkFrameWriter writer, MavlinkMissionItemInt item)
    {
        const byte mavFrameMission = 2;
        var payload = new byte[item.MissionType == MavMissionType.Mission ? 37 : 38];
        BitConverter.GetBytes(item.Param1).CopyTo(payload, 0);
        BitConverter.GetBytes(item.Param2).CopyTo(payload, 4);
        BitConverter.GetBytes(item.Param3).CopyTo(payload, 8);
        BitConverter.GetBytes(item.Param4).CopyTo(payload, 12);
        BitConverter.GetBytes(item.Frame == mavFrameMission ? item.X : item.X / 10000000.0f).CopyTo(payload, 16);
        BitConverter.GetBytes(item.Frame == mavFrameMission ? item.Y : item.Y / 10000000.0f).CopyTo(payload, 20);
        BitConverter.GetBytes(item.Z).CopyTo(payload, 24);
        BitConverter.GetBytes(item.Sequence).CopyTo(payload, 28);
        BitConverter.GetBytes(item.Command).CopyTo(payload, 30);
        payload[32] = item.TargetSystemId;
        payload[33] = item.TargetComponentId;
        payload[34] = item.Frame;
        payload[35] = item.Current;
        payload[36] = item.AutoContinue;
        if (payload.Length > 37)
        {
            payload[37] = (byte)item.MissionType;
        }

        return item.MissionType == MavMissionType.Mission
            ? writer.CreateV1Frame(255, 190, 39, payload, crcExtra: 254)
            : writer.CreateV2Frame(255, 190, 39, payload, crcExtra: 254);
    }

    private static byte[] CreateLogRequestListFrame(MavlinkFrameWriter writer, byte targetSystem, byte targetComponent)
    {
        var payload = new byte[6];
        payload[0] = targetSystem;
        payload[1] = targetComponent;
        BitConverter.GetBytes((ushort)0).CopyTo(payload, 2);
        BitConverter.GetBytes(ushort.MaxValue).CopyTo(payload, 4);
        return writer.CreateV1Frame(255, 190, messageId: 117, payload, crcExtra: 128);
    }

    private static byte[] CreateLogRequestDataFrame(MavlinkFrameWriter writer, byte targetSystem, byte targetComponent, ushort id, uint offset, uint count)
    {
        var payload = new byte[14];
        BitConverter.GetBytes(id).CopyTo(payload, 0);
        BitConverter.GetBytes(offset).CopyTo(payload, 2);
        BitConverter.GetBytes(count).CopyTo(payload, 6);
        payload[10] = targetSystem;
        payload[11] = targetComponent;
        return writer.CreateV1Frame(255, 190, messageId: 119, payload, crcExtra: 116);
    }

    private static byte[] CreateLogRequestEndFrame(MavlinkFrameWriter writer, byte targetSystem, byte targetComponent)
    {
        var payload = new byte[2];
        payload[0] = targetSystem;
        payload[1] = targetComponent;
        return writer.CreateV1Frame(255, 190, messageId: 122, payload, crcExtra: 203);
    }

    private static bool TryReadLogEntry(MavlinkPacket packet, out LogEntryEvidence entry)
    {
        entry = default!;
        if (packet.Payload.Length < 14)
        {
            return false;
        }

        entry = new LogEntryEvidence(
            Id: BitConverter.ToUInt16(packet.Payload, 0),
            NumLogs: BitConverter.ToUInt16(packet.Payload, 2),
            LastLogNum: BitConverter.ToUInt16(packet.Payload, 4),
            TimeUtc: BitConverter.ToUInt32(packet.Payload, 6),
            Size: BitConverter.ToUInt32(packet.Payload, 10));
        return true;
    }

    private static bool TryReadLogData(MavlinkPacket packet, out byte[] data)
    {
        data = [];
        if (packet.Payload.Length < 7)
        {
            return false;
        }

        var count = packet.Payload[6];
        if (packet.Payload.Length < 7 + count)
        {
            return false;
        }

        data = packet.Payload.Skip(7).Take(count).ToArray();
        return true;
    }

    private static string ToPrintableAscii(IEnumerable<byte> bytes)
    {
        var builder = new StringBuilder();
        foreach (var value in bytes)
        {
            builder.Append(value is >= 32 and <= 126 ? (char)value : '.');
        }

        return builder.ToString();
    }

    private async Task<AdbEvidence> CollectAdbAsync()
    {
        var adbPath = ResolveAdbPath();
        if (adbPath is null)
        {
            return new AdbEvidence(false, false, null, null, null, false, null, null, false, ["adb executable was not found."]);
        }

        var connect = await RunProcessAsync(adbPath, $"connect {_options.AdbSerial}");
        ProcessEvidence? reverse = null;
        if (_options.ConfigureAdbReverse)
        {
            reverse = await RunProcessAsync(adbPath, $"-s {_options.AdbSerial} reverse tcp:{_options.Port} tcp:{_options.Port}");
        }

        var reverseList = await RunProcessAsync(adbPath, $"-s {_options.AdbSerial} reverse --list");
        var devices = await RunProcessAsync(adbPath, "devices");
        var output = $"{connect.StandardOutput}{Environment.NewLine}{devices.StandardOutput}";
        var error = $"{connect.StandardError}{Environment.NewLine}{devices.StandardError}".Trim();
        var deviceOnline = output
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.StartsWith(_options.AdbSerial, StringComparison.Ordinal) && line.Contains("\tdevice", StringComparison.Ordinal));
        var reverseConfigured = reverseList.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Contains($"tcp:{_options.Port} tcp:{_options.Port}", StringComparison.Ordinal));

        var errors = new List<string>();
        if (!deviceOnline)
        {
            errors.Add(error.Length == 0 ? "ADB device is not online." : error);
        }

        if (_options.ConfigureAdbReverse && !reverseConfigured)
        {
            var reverseError = string.Join(" ", new[] { reverse?.StandardError, reverseList.StandardError }.Where(static value => !string.IsNullOrWhiteSpace(value)));
            errors.Add(reverseError.Length == 0 ? $"ADB reverse tcp:{_options.Port} is not configured." : reverseError);
        }

        return new AdbEvidence(false, true, adbPath, connect, devices, deviceOnline, reverse, reverseList, reverseConfigured, errors);
    }

    private string? ResolveAdbPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.AdbPath) && File.Exists(_options.AdbPath))
        {
            return _options.AdbPath;
        }

        var envAdb = Environment.GetEnvironmentVariable("ADB");
        if (!string.IsNullOrWhiteSpace(envAdb) && File.Exists(envAdb))
        {
            return envAdb;
        }

        var visualStudioAdb = @"D:\Program Files (x86)\Microsoft Visual Studio\Shared\Android\android-sdk\platform-tools\adb.exe";
        if (File.Exists(visualStudioAdb))
        {
            return visualStudioAdb;
        }

        return "adb";
    }

    private static async Task<ProcessEvidence> RunProcessAsync(string fileName, string arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new ProcessEvidence(process.ExitCode, stdout.Trim(), stderr.Trim());
        }
        catch (Exception ex)
        {
            return new ProcessEvidence(-1, string.Empty, ex.Message);
        }
    }
}

internal static class EvidenceMarkdownWriter
{
    public static string Write(RealConnectionEvidence evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Real Connection Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Started: {evidence.StartedAt:O}");
        builder.AppendLine($"- Completed: {evidence.CompletedAt:O}");
        builder.AppendLine($"- MAVLink endpoint: {evidence.Protocol.ToUpperInvariant()} {evidence.Host}:{evidence.Port}");
        builder.AppendLine(evidence.Adb.Skipped
            ? "- Android ADB: skipped"
            : $"- Android ADB endpoint: {evidence.AdbSerial}");
        builder.AppendLine($"- Overall: {evidence.OverallStatus}");
        builder.AppendLine();
        builder.AppendLine("## MAVLink Link");
        builder.AppendLine();
        builder.AppendLine($"- Protocol: {evidence.Mavlink.Protocol}");
        builder.AppendLine($"- Connected: {evidence.Mavlink.Connected}");
        builder.AppendLine($"- Received bytes: {evidence.Mavlink.ReceivedBytes}");
        builder.AppendLine($"- Parsed frames: {evidence.Mavlink.ParsedFrameCount}");
        builder.AppendLine($"- Heartbeats: {evidence.Mavlink.HeartbeatCount}");
        builder.AppendLine($"- GCS heartbeats sent: {evidence.Mavlink.GcsHeartbeatsSent}");
        builder.AppendLine($"- First frame at: {evidence.Mavlink.FirstFrameAt?.ToString("O") ?? "n/a"}");
        builder.AppendLine($"- Parser buffered bytes: {evidence.Mavlink.ParserBufferedBytes}");
        builder.AppendLine($"- Raw sample hex: `{Truncate(evidence.Mavlink.RawSampleHex, 256)}`");
        builder.AppendLine($"- Raw sample ASCII: `{Truncate(evidence.Mavlink.RawSampleAscii, 256)}`");
        builder.AppendLine();
        builder.AppendLine("### Message Counts");
        builder.AppendLine();
        foreach (var message in evidence.Mavlink.MessageCounts)
        {
            builder.AppendLine($"- Message {message.MessageId}: {message.Count}");
        }

        if (evidence.Mavlink.MessageCounts.Count == 0)
        {
            builder.AppendLine("- None");
        }

        builder.AppendLine();
        builder.AppendLine("### Heartbeats");
        builder.AppendLine();
        foreach (var heartbeat in evidence.Mavlink.Heartbeats)
        {
            builder.AppendLine($"- sys={heartbeat.SystemId} comp={heartbeat.ComponentId} type={heartbeat.VehicleType} autopilot={heartbeat.Autopilot} baseMode={heartbeat.BaseMode} customMode={heartbeat.CustomMode} status={heartbeat.SystemStatus} at={heartbeat.ObservedAt:O}");
        }

        if (evidence.Mavlink.Heartbeats.Count == 0)
        {
            builder.AppendLine("- None");
        }

        if (evidence.Mavlink.SitlWorkflow is not null)
        {
            builder.AppendLine();
            builder.AppendLine("## SITL Workflow");
            builder.AppendLine();
            var sitl = evidence.Mavlink.SitlWorkflow;
            builder.AppendLine($"- Started: {sitl.Started}");
            builder.AppendLine($"- Passed: {sitl.Passed}");
            if (sitl.Parameter is not null)
            {
                builder.AppendLine($"- Parameters: received {sitl.Parameter.Received}/{sitl.Parameter.Expected}; sample `{string.Join(", ", sitl.Parameter.SampleNames)}`");
            }

            if (sitl.MissionRead is not null)
            {
                builder.AppendLine($"- Mission read: {sitl.MissionRead.Success}; {sitl.MissionRead.Status}; expected {sitl.MissionRead.ExpectedCount}, received {sitl.MissionRead.Items.Count}");
            }

            if (sitl.MissionWrite is not null)
            {
                builder.AppendLine($"- Mission write: {sitl.MissionWrite.Success}; encoding `{sitl.MissionWrite.ItemEncoding}`; expected {sitl.MissionWrite.ExpectedCount}, sent {sitl.MissionWrite.SentItems}; ack `{sitl.MissionWrite.AckResult}`");
                builder.AppendLine($"- Mission write requests: seq `{string.Join(", ", sitl.MissionWrite.RequestedSequences)}` via msg `{string.Join(", ", sitl.MissionWrite.RequestMessageIds)}`");
            }

            if (sitl.ModeSwitch is not null)
            {
                builder.AppendLine($"- Mode switch: original custom `{sitl.ModeSwitch.OriginalCustomMode}`, target `{sitl.ModeSwitch.TargetCustomMode}`, observed target `{sitl.ModeSwitch.TargetObserved}`, restored `{sitl.ModeSwitch.RestoreObserved}`");
            }

            if (sitl.Log is not null)
            {
                builder.AppendLine($"- Logs: entries {sitl.Log.EntryCount}, first data bytes {sitl.Log.FirstDataBytes}");
            }

            if (sitl.Errors.Count > 0)
            {
                builder.AppendLine("- Workflow notes/errors:");
                foreach (var error in sitl.Errors)
                {
                    builder.AppendLine($"  - {error}");
                }
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Android ADB");
        builder.AppendLine();
        if (evidence.Adb.Skipped)
        {
            builder.AppendLine("- skipped: True");
        }
        else
        {
            builder.AppendLine($"- adb available: {evidence.Adb.AdbAvailable}");
            builder.AppendLine($"- adb path: {evidence.Adb.AdbPath ?? "n/a"}");
            builder.AppendLine($"- device online: {evidence.Adb.DeviceOnline}");
            builder.AppendLine($"- connect exit: {evidence.Adb.Connect?.ExitCode.ToString() ?? "n/a"}");
            builder.AppendLine($"- devices exit: {evidence.Adb.Devices?.ExitCode.ToString() ?? "n/a"}");
            builder.AppendLine($"- reverse configured: {evidence.Adb.ReverseConfigured}");
            builder.AppendLine($"- reverse exit: {evidence.Adb.Reverse?.ExitCode.ToString() ?? "n/a"}");
            builder.AppendLine($"- reverse list: `{Truncate(evidence.Adb.ReverseList?.StandardOutput.ReplaceLineEndings(" | ") ?? "n/a", 256)}`");
        }
        builder.AppendLine();
        builder.AppendLine("## Remaining Blockers");
        builder.AppendLine();
        if (evidence.OverallStatus == EvidenceStatus.Passed)
        {
            if (evidence.Mavlink.SitlWorkflow?.Passed == true)
            {
                builder.AppendLine("- This evidence closes desktop SITL endpoint reachability, MAVLink heartbeat parsing, parameter download, mission read, mission write with ACK skipped by phase decision, and mode switch.");
                builder.AppendLine("- Android app runtime, production map/video runtime, real hardware, and release package evidence remain separate gates.");
            }
            else
            {
                builder.AppendLine("- This evidence closes endpoint reachability and MAVLink heartbeat parsing only.");
                builder.AppendLine("- Mode changes, mission read/write, parameter download, Android app runtime, SITL transcripts, map/video runtime, and release package evidence are still separate gates.");
            }
        }
        else
        {
            if (evidence.Mavlink.Connected && evidence.Mavlink.HeartbeatCount == 0)
            {
                builder.AppendLine("- Link connected but no valid MAVLink heartbeat was parsed.");
            }

            foreach (var error in evidence.Mavlink.Errors.Concat(evidence.Adb.Errors))
            {
                builder.AppendLine($"- {error}");
            }

            builder.AppendLine("- Mode changes, mission read/write, parameter download, Android app runtime, SITL transcripts, map/video runtime, and release package evidence are still separate gates.");
        }
        return builder.ToString();
    }

    private static string Truncate(string value, int length)
    {
        return value.Length <= length ? value : string.Concat(value.AsSpan(0, length), "...");
    }
}

internal sealed record RealConnectionEvidence(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string Host,
    int Port,
    string Protocol,
    string AdbSerial,
    LinkMavlinkEvidence Mavlink,
    AdbEvidence Adb,
    EvidenceStatus OverallStatus);

internal sealed record LinkMavlinkEvidence(
    string Protocol,
    bool Connected,
    long ReceivedBytes,
    int ParsedFrameCount,
    int HeartbeatCount,
    int GcsHeartbeatsSent,
    DateTimeOffset? FirstFrameAt,
    int ParserBufferedBytes,
    string RawSampleHex,
    string RawSampleAscii,
    IReadOnlyList<MavlinkMessageCount> MessageCounts,
    IReadOnlyList<HeartbeatEvidence> Heartbeats,
    SitlWorkflowEvidence? SitlWorkflow,
    IReadOnlyList<string> Errors);

internal sealed record HeartbeatEvidence(
    byte Version,
    byte SystemId,
    byte ComponentId,
    MavAutopilot Autopilot,
    MavType VehicleType,
    byte BaseMode,
    uint CustomMode,
    byte SystemStatus,
    DateTimeOffset ObservedAt);

internal sealed record MavlinkMessageCount(uint MessageId, int Count);

internal sealed record SitlWorkflowEvidence(
    bool Started,
    bool Passed,
    ParameterWorkflowEvidence? Parameter,
    MissionReadEvidence? MissionRead,
    MissionWriteEvidence? MissionWrite,
    ModeSwitchEvidence? ModeSwitch,
    LogWorkflowEvidence? Log,
    IReadOnlyList<string> Errors);

internal sealed record ParameterWorkflowEvidence(
    int Expected,
    int Received,
    IReadOnlyList<string> SampleNames);

internal sealed record MissionReadEvidence(
    bool Success,
    int ExpectedCount,
    IReadOnlyList<MavlinkMissionItemInt> Items,
    string Status);

internal sealed record MissionWriteEvidence(
    bool Success,
    int ExpectedCount,
    int SentItems,
    string AckResult,
    IReadOnlyList<ushort> RequestedSequences,
    IReadOnlyList<uint> RequestMessageIds,
    string ItemEncoding,
    IReadOnlyList<MissionWriteAttemptEvidence> Attempts);

internal sealed record MissionWriteAttemptEvidence(
    string ItemEncoding,
    bool Success,
    int ExpectedCount,
    int SentItems,
    string AckResult,
    IReadOnlyList<ushort> RequestedSequences,
    IReadOnlyList<uint> RequestMessageIds);

internal sealed record ModeSwitchEvidence(
    uint OriginalCustomMode,
    uint TargetCustomMode,
    bool TargetObserved,
    bool RestoreObserved);

internal sealed record LogWorkflowEvidence(
    int EntryCount,
    IReadOnlyList<LogEntryEvidence> Entries,
    int FirstDataBytes);

internal sealed record LogEntryEvidence(
    ushort Id,
    ushort NumLogs,
    ushort LastLogNum,
    uint TimeUtc,
    uint Size);

internal sealed record AdbEvidence(
    bool Skipped,
    bool AdbAvailable,
    string? AdbPath,
    ProcessEvidence? Connect,
    ProcessEvidence? Devices,
    bool DeviceOnline,
    ProcessEvidence? Reverse,
    ProcessEvidence? ReverseList,
    bool ReverseConfigured,
    IReadOnlyList<string> Errors)
{
    public static AdbEvidence SkippedInstance { get; } = new(true, false, null, null, null, false, null, null, false, []);
}

internal sealed record MapEvidenceOptions(
    string OutputDirectory,
    string Provider,
    string LayerId,
    int Zoom,
    int X,
    int Y,
    TimeSpan Timeout,
    bool AllowPartial)
{
    public static MapEvidenceOptions Parse(string[] args)
    {
        var outputDirectory = Path.Combine(".artifacts", "evidence", "phase-343-map-provider-cache-runtime", "map-provider");
        var provider = "osm";
        var layerId = "osm-standard";
        var zoom = 1;
        var x = 1;
        var y = 1;
        var timeout = TimeSpan.FromSeconds(15);
        var allowPartial = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"Missing value for {arg}.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--map-provider-evidence":
                    break;
                case "--output":
                    outputDirectory = Next();
                    break;
                case "--provider":
                    provider = Next().ToLowerInvariant();
                    break;
                case "--layer":
                    layerId = Next();
                    break;
                case "--z":
                    zoom = int.Parse(Next());
                    break;
                case "--x":
                    x = int.Parse(Next());
                    break;
                case "--y":
                    y = int.Parse(Next());
                    break;
                case "--timeout-seconds":
                    timeout = TimeSpan.FromSeconds(int.Parse(Next()));
                    break;
                case "--allow-partial":
                    allowPartial = true;
                    break;
            }
        }

        return new MapEvidenceOptions(outputDirectory, provider, layerId, zoom, x, y, timeout, allowPartial);
    }
}

internal sealed class MapEvidenceCollector
{
    private readonly MapEvidenceOptions _options;

    public MapEvidenceCollector(MapEvidenceOptions options)
    {
        _options = options;
    }

    public async Task<MapProviderEvidence> CollectAsync()
    {
        var startedAt = DateTimeOffset.Now;
        var errors = new List<string>();
        var descriptor = ResolveProvider(_options.Provider);
        var policy = MapTileCachePolicyFactory.CreateRuntimePolicy(descriptor);
        var attribution = new MapAttributionUiProjector().Project(descriptor);
        var cache = new InMemoryMapTileCacheStore();
        var key = new MapTileCacheKey(descriptor.Kind, _options.LayerId, _options.Zoom, _options.X, _options.Y);

        var tileFetched = false;
        var tileBytes = 0;
        string? tileSha256 = null;
        var cacheStored = false;
        var cacheReloaded = false;

        using var httpClient = new HttpClient
        {
            Timeout = _options.Timeout
        };
        using var adapter = new RasterTileMapAdapter(descriptor, httpClient);
        try
        {
            var bytes = await adapter.FetchTileAsync(_options.LayerId, _options.Zoom, _options.X, _options.Y).ConfigureAwait(false);
            if (bytes is { Length: > 0 })
            {
                tileFetched = true;
                tileBytes = bytes.Length;
                tileSha256 = Convert.ToHexString(SHA256.HashData(bytes));
                var entry = new MapTileCacheEntry(key, bytes, "image/png", DateTimeOffset.Now, DateTimeOffset.Now.Add(policy.MaxAge));
                await cache.StoreAsync(entry).ConfigureAwait(false);
                cacheStored = true;
                cacheReloaded = await cache.LoadAsync(key).ConfigureAwait(false) is not null;
            }
            else
            {
                errors.Add("Provider returned no tile bytes.");
            }
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }

        var completedAt = DateTimeOffset.Now;
        var status = tileFetched && cacheStored && cacheReloaded ? EvidenceStatus.Passed : EvidenceStatus.Partial;
        return new MapProviderEvidence(
            startedAt,
            completedAt,
            descriptor.Id,
            descriptor.DisplayName,
            _options.LayerId,
            _options.Zoom,
            _options.X,
            _options.Y,
            policy.AllowInteractiveNetworkCache,
            policy.AllowBulkDownload,
            policy.AllowOfflinePackageImport,
            attribution.DisplayText,
            attribution.MustShowAttribution,
            tileFetched,
            tileBytes,
            tileSha256,
            cacheStored,
            cacheReloaded,
            errors,
            status);
    }

    private static MapProviderDescriptor ResolveProvider(string provider)
    {
        return provider switch
        {
            "osm" or "mapsui" or "mapsui-osm" => MapProviderCatalog.MapsuiOsmRaster,
            _ => throw new ArgumentException($"Unsupported map evidence provider '{provider}'.")
        };
    }
}

internal static class MapEvidenceMarkdownWriter
{
    public static string Write(MapProviderEvidence evidence)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Map Provider Runtime Evidence");
        builder.AppendLine();
        builder.AppendLine($"- Started: {evidence.StartedAt:O}");
        builder.AppendLine($"- Completed: {evidence.CompletedAt:O}");
        builder.AppendLine($"- Overall: {evidence.OverallStatus}");
        builder.AppendLine($"- Provider: {evidence.ProviderDisplayName} (`{evidence.ProviderId}`)");
        builder.AppendLine($"- Tile: `{evidence.LayerId}` z={evidence.Zoom} x={evidence.X} y={evidence.Y}");
        builder.AppendLine();
        builder.AppendLine("## Tile Fetch");
        builder.AppendLine();
        builder.AppendLine($"- Fetched: {evidence.TileFetched}");
        builder.AppendLine($"- Bytes: {evidence.TileBytes}");
        builder.AppendLine($"- SHA256: `{evidence.TileSha256 ?? "n/a"}`");
        builder.AppendLine();
        builder.AppendLine("## Cache");
        builder.AppendLine();
        builder.AppendLine($"- Interactive network cache allowed: {evidence.AllowInteractiveNetworkCache}");
        builder.AppendLine($"- Bulk download allowed: {evidence.AllowBulkDownload}");
        builder.AppendLine($"- Offline package import allowed: {evidence.AllowOfflinePackageImport}");
        builder.AppendLine($"- Stored: {evidence.CacheStored}");
        builder.AppendLine($"- Reloaded: {evidence.CacheReloaded}");
        builder.AppendLine();
        builder.AppendLine("## Attribution");
        builder.AppendLine();
        builder.AppendLine($"- Must show attribution: {evidence.MustShowAttribution}");
        builder.AppendLine($"- Text: {evidence.AttributionText}");
        builder.AppendLine();
        builder.AppendLine("## Errors");
        builder.AppendLine();
        if (evidence.Errors.Count == 0)
        {
            builder.AppendLine("- None");
        }
        else
        {
            foreach (var error in evidence.Errors)
            {
                builder.AppendLine($"- {error}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Boundary");
        builder.AppendLine();
        builder.AppendLine("- This is a single interactive provider fetch/cache probe.");
        builder.AppendLine("- It does not authorize bulk tile download, offline package generation, physical Android device performance claims, or release-candidate readiness.");
        return builder.ToString();
    }
}

internal sealed record MapProviderEvidence(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string ProviderId,
    string ProviderDisplayName,
    string LayerId,
    int Zoom,
    int X,
    int Y,
    bool AllowInteractiveNetworkCache,
    bool AllowBulkDownload,
    bool AllowOfflinePackageImport,
    string AttributionText,
    bool MustShowAttribution,
    bool TileFetched,
    int TileBytes,
    string? TileSha256,
    bool CacheStored,
    bool CacheReloaded,
    IReadOnlyList<string> Errors,
    EvidenceStatus OverallStatus);

internal sealed record ProcessEvidence(int ExitCode, string StandardOutput, string StandardError);

internal enum EvidenceStatus
{
    Passed,
    Partial
}
