namespace VGC.Positioning;

public enum PositionSourceKind
{
    Unknown,
    NativeLocation,
    ExternalGps,
    RtkCorrection,
    FollowMe
}

public enum PositionPermissionStatus
{
    Unknown,
    Granted,
    Denied,
    Restricted
}

public sealed record PlatformPositionPermissionState(
    string Platform,
    PositionPermissionStatus ForegroundLocation,
    PositionPermissionStatus BackgroundLocation,
    PositionPermissionStatus Bluetooth,
    PositionPermissionStatus Usb,
    bool CanUseNativeLocation,
    bool CanUseExternalGps,
    string StatusText);

public sealed class PlatformPositionPermissionProjector
{
    public PlatformPositionPermissionState Project(
        string platform,
        PositionPermissionStatus foregroundLocation,
        PositionPermissionStatus backgroundLocation,
        PositionPermissionStatus bluetooth,
        PositionPermissionStatus usb)
    {
        var canUseNative = foregroundLocation == PositionPermissionStatus.Granted;
        var canUseExternal = bluetooth == PositionPermissionStatus.Granted || usb == PositionPermissionStatus.Granted;
        var status = canUseNative || canUseExternal
            ? $"Position sources available on {platform}."
            : $"Position sources blocked on {platform}: permission required.";

        return new PlatformPositionPermissionState(
            platform,
            foregroundLocation,
            backgroundLocation,
            bluetooth,
            usb,
            canUseNative,
            canUseExternal,
            status);
    }
}

public sealed record PositionSourceSnapshot(
    PositionSourceKind Kind,
    GpsPosition? Position,
    DateTimeOffset? LastUpdatedAt,
    string StatusText);

public sealed class PositionSourceSelector
{
    public PositionSourceSnapshot SelectBest(params PositionSourceSnapshot[] sources)
    {
        var validSources = sources
            .Where(static source => source.Position is not null && source.Position.Fix is not GpsFixQuality.None and not GpsFixQuality.NoFix)
            .OrderByDescending(static source => FixRank(source.Position!.Fix))
            .ThenByDescending(static source => source.LastUpdatedAt ?? DateTimeOffset.MinValue)
            .ToArray();

        return validSources.FirstOrDefault()
            ?? sources.FirstOrDefault(static source => source.Position is not null)
            ?? new PositionSourceSnapshot(PositionSourceKind.Unknown, null, null, "No position source available.");
    }

    private static int FixRank(GpsFixQuality fix)
    {
        return fix switch
        {
            GpsFixQuality.RtkFixed => 5,
            GpsFixQuality.RtkFloat => 4,
            GpsFixQuality.Fix3D => 3,
            GpsFixQuality.Fix2D => 2,
            GpsFixQuality.NoFix => 1,
            _ => 0
        };
    }
}

public sealed record NmeaPositionReading(
    GpsPosition Position,
    double? Hdop,
    DateTimeOffset ReceivedAt,
    string SentenceType);

public static class NmeaPositionParser
{
    public static bool TryParse(string sentence, DateTimeOffset receivedAt, out NmeaPositionReading reading)
    {
        reading = default!;
        if (string.IsNullOrWhiteSpace(sentence))
        {
            return false;
        }

        var trimmed = sentence.Trim();
        var star = trimmed.IndexOf('*', StringComparison.Ordinal);
        var withoutChecksum = star >= 0 ? trimmed[..star] : trimmed;
        var fields = withoutChecksum.Split(',');
        if (fields.Length == 0)
        {
            return false;
        }

        var type = fields[0].TrimStart('$');
        if (type.EndsWith("GGA", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseGga(fields, receivedAt, type, out reading);
        }

        if (type.EndsWith("RMC", StringComparison.OrdinalIgnoreCase))
        {
            return TryParseRmc(fields, receivedAt, type, out reading);
        }

        return false;
    }

    private static bool TryParseGga(string[] fields, DateTimeOffset receivedAt, string type, out NmeaPositionReading reading)
    {
        reading = default!;
        if (fields.Length < 10 ||
            !TryParseCoordinate(fields[2], fields[3], out var latitude) ||
            !TryParseCoordinate(fields[4], fields[5], out var longitude) ||
            !int.TryParse(fields[6], out var fixCode))
        {
            return false;
        }

        _ = int.TryParse(fields[7], out var satellites);
        _ = double.TryParse(fields[8], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var hdop);
        _ = double.TryParse(fields[9], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var altitude);

        reading = new NmeaPositionReading(
            new GpsPosition(latitude, longitude, altitude, MapGgaFix(fixCode), satellites),
            hdop == 0 ? null : hdop,
            receivedAt,
            type);
        return true;
    }

    private static bool TryParseRmc(string[] fields, DateTimeOffset receivedAt, string type, out NmeaPositionReading reading)
    {
        reading = default!;
        if (fields.Length < 7 ||
            !string.Equals(fields[2], "A", StringComparison.OrdinalIgnoreCase) ||
            !TryParseCoordinate(fields[3], fields[4], out var latitude) ||
            !TryParseCoordinate(fields[5], fields[6], out var longitude))
        {
            return false;
        }

        reading = new NmeaPositionReading(
            new GpsPosition(latitude, longitude, 0, GpsFixQuality.Fix2D, 0),
            null,
            receivedAt,
            type);
        return true;
    }

    private static bool TryParseCoordinate(string value, string hemisphere, out double coordinate)
    {
        coordinate = 0;
        if (string.IsNullOrWhiteSpace(value) ||
            !double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var raw))
        {
            return false;
        }

        var degrees = Math.Floor(raw / 100);
        var minutes = raw - degrees * 100;
        coordinate = degrees + minutes / 60.0;
        if (hemisphere is "S" or "W")
        {
            coordinate = -coordinate;
        }

        return true;
    }

    private static GpsFixQuality MapGgaFix(int fixCode)
    {
        return fixCode switch
        {
            4 => GpsFixQuality.RtkFixed,
            5 => GpsFixQuality.RtkFloat,
            1 or 2 => GpsFixQuality.Fix3D,
            _ => GpsFixQuality.NoFix
        };
    }
}

public sealed class ExternalGpsRuntime : IGpsService, IPositionService
{
    private NmeaPositionReading? _lastReading;

    public PositionSourceSnapshot Snapshot => _lastReading is null
        ? new PositionSourceSnapshot(PositionSourceKind.ExternalGps, null, null, "External GPS has no fix.")
        : new PositionSourceSnapshot(PositionSourceKind.ExternalGps, _lastReading.Position, _lastReading.ReceivedAt, $"External GPS {_lastReading.Position.Fix} from {_lastReading.SentenceType}.");

    public bool IngestNmea(string sentence, DateTimeOffset receivedAt)
    {
        if (!NmeaPositionParser.TryParse(sentence, receivedAt, out var reading))
        {
            return false;
        }

        _lastReading = reading;
        return true;
    }

    public Task<GpsPosition?> GetPositionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_lastReading?.Position);
    }

    public Task<GpsPosition?> GetCurrentPositionAsync(CancellationToken cancellationToken = default)
    {
        return GetPositionAsync(cancellationToken);
    }

    public Task<RtkState> GetRtkStateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var position = _lastReading?.Position;
        var isRtk = position?.Fix is GpsFixQuality.RtkFloat or GpsFixQuality.RtkFixed;
        return Task.FromResult(new RtkState(isRtk, BaselineMeters: 0, Observations: position?.Satellites ?? 0));
    }
}

public enum RtkCorrectionState
{
    Disabled,
    Configured,
    Connecting,
    Streaming,
    Failed,
    Stopped
}

public sealed record NtripCasterConfiguration(
    string Host,
    int Port,
    string MountPoint,
    string? Username,
    string? Password,
    bool UseTls);

public sealed record RtkCorrectionSnapshot(
    NtripCasterConfiguration? Configuration,
    RtkCorrectionState State,
    long BytesReceived,
    int RtcmPacketsForwarded,
    string? Error,
    string StatusText);

public sealed class RtkCorrectionRuntime
{
    private NtripCasterConfiguration? _configuration;
    private RtkCorrectionState _state = RtkCorrectionState.Disabled;
    private long _bytesReceived;
    private int _packetsForwarded;
    private string? _error;

    public RtkCorrectionSnapshot Snapshot => BuildSnapshot();

    public RtkCorrectionSnapshot Configure(NtripCasterConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Host) ||
            configuration.Port <= 0 ||
            string.IsNullOrWhiteSpace(configuration.MountPoint))
        {
            _state = RtkCorrectionState.Failed;
            _error = "NTRIP host, port, and mount point are required.";
            return BuildSnapshot();
        }

        _configuration = configuration;
        _state = RtkCorrectionState.Configured;
        _error = null;
        return BuildSnapshot();
    }

    public RtkCorrectionSnapshot Start()
    {
        if (_configuration is null)
        {
            _state = RtkCorrectionState.Failed;
            _error = "NTRIP configuration is missing.";
            return BuildSnapshot();
        }

        _state = RtkCorrectionState.Connecting;
        _error = null;
        return BuildSnapshot();
    }

    public RtkCorrectionSnapshot MarkStreaming()
    {
        _state = _configuration is null ? RtkCorrectionState.Failed : RtkCorrectionState.Streaming;
        _error = _configuration is null ? "NTRIP configuration is missing." : null;
        return BuildSnapshot();
    }

    public RtkCorrectionSnapshot AcceptRtcmPacket(ReadOnlySpan<byte> packet)
    {
        if (_state != RtkCorrectionState.Streaming)
        {
            _state = RtkCorrectionState.Failed;
            _error = "RTCM packet received before NTRIP stream was active.";
            return BuildSnapshot();
        }

        _bytesReceived += packet.Length;
        _packetsForwarded++;
        return BuildSnapshot();
    }

    public RtkCorrectionSnapshot Stop()
    {
        _state = RtkCorrectionState.Stopped;
        return BuildSnapshot();
    }

    public string BuildNtripRequest()
    {
        if (_configuration is null)
        {
            throw new InvalidOperationException("NTRIP configuration is missing.");
        }

        return $"GET /{_configuration.MountPoint.TrimStart('/')} HTTP/1.1\r\nHost: {_configuration.Host}:{_configuration.Port}\r\nNtrip-Version: Ntrip/2.0\r\nUser-Agent: VGC\r\n\r\n";
    }

    private RtkCorrectionSnapshot BuildSnapshot()
    {
        var status = _state switch
        {
            RtkCorrectionState.Disabled => "RTK corrections disabled.",
            RtkCorrectionState.Configured => $"RTK corrections configured for {_configuration?.MountPoint}.",
            RtkCorrectionState.Connecting => $"Connecting to NTRIP caster {_configuration?.Host}.",
            RtkCorrectionState.Streaming => $"RTCM streaming: {_packetsForwarded} packets.",
            RtkCorrectionState.Failed => _error ?? "RTK correction stream failed.",
            RtkCorrectionState.Stopped => "RTK correction stream stopped.",
            _ => _state.ToString()
        };

        return new RtkCorrectionSnapshot(_configuration, _state, _bytesReceived, _packetsForwarded, _error, status);
    }
}

public sealed record PositioningRuntimeEvidenceItem(
    string Id,
    string EvidenceLevel,
    string Description,
    bool Complete);

public sealed class PositioningRuntimeEvidenceCatalog
{
    public IReadOnlyList<PositioningRuntimeEvidenceItem> Build()
    {
        return
        [
            new("POSITION-263", "L1/L4", "Native location permission and background-location constraints are modeled in shared code.", true),
            new("POSITION-264", "L1/L5", "External GPS NMEA GGA/RMC parsing projects fix, coordinate, altitude, and satellite count.", true),
            new("POSITION-265", "L1/L5", "Position source selector chooses the highest-quality current fix across native/external/RTK sources.", true),
            new("POSITION-266", "L1/L6", "RTK/NTRIP/RTCM session state, request boundary, and packet forwarding counters are modeled.", true),
            new("POSITION-267", "L1/L5", "FollowMe service validates target coordinates and sends MAVLink command targets while active.", true),
            new("POSITION-268", "L0/L4", "Desktop/Android permission and hardware validation remains a platform evidence checklist.", false),
            new("POSITION-269", "L0/L6", "Real external GPS, NTRIP caster, and mobile FollowMe field evidence remains deferred.", false)
        ];
    }
}

public sealed record PositioningRuntimeAuditResult(
    int CompleteItems,
    int DeferredItems,
    IReadOnlyList<string> DeferredGaps,
    string Summary);

public sealed class PositioningRuntimeParityAudit
{
    public PositioningRuntimeAuditResult Audit(IReadOnlyList<PositioningRuntimeEvidenceItem> evidence)
    {
        var complete = evidence.Count(static item => item.Complete);
        var deferred = evidence.Where(static item => !item.Complete).Select(static item => item.Description).ToArray();
        return new PositioningRuntimeAuditResult(
            complete,
            deferred.Length,
            deferred,
            $"{complete} positioning evidence items complete; {deferred.Length} platform/field evidence gaps remain.");
    }
}
