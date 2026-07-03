using VGC.Comms;
using VGC.Mavlink;

namespace VGC.Positioning;

public sealed record FollowMeTarget(
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    DateTimeOffset Timestamp);

public sealed record FollowMeSnapshot(
    bool IsActive,
    FollowMeTarget? LastTarget,
    int SentTargetCount,
    string StatusText);

public sealed class FollowMeService
{
    private readonly byte _systemId;
    private readonly byte _componentId;
    private readonly MavlinkCommandService _commandService;
    private bool _isActive;
    private FollowMeTarget? _lastTarget;
    private int _sentTargetCount;
    private string _statusText = "FollowMe stopped.";

    public FollowMeService(byte systemId, byte componentId, MavlinkCommandService? commandService = null)
    {
        _systemId = systemId;
        _componentId = componentId;
        _commandService = commandService ?? new MavlinkCommandService(systemId, componentId);
    }

    public bool IsActive => _isActive;

    public FollowMeSnapshot Snapshot => new(_isActive, _lastTarget, _sentTargetCount, _statusText);

    public async Task StartAsync(
        ILinkTransport link,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _isActive = true;
        _statusText = link.CanSend ? "FollowMe active." : "FollowMe active; vehicle link is not currently send-capable.";
        await Task.CompletedTask;
    }

    public async Task SendTargetAsync(
        ILinkTransport link,
        double latitude,
        double longitude,
        double altitude,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_isActive)
        {
            _statusText = "FollowMe target ignored because service is stopped.";
            return;
        }

        if (!IsValidTarget(latitude, longitude, altitude))
        {
            _statusText = "FollowMe target rejected: invalid coordinate.";
            return;
        }

        var command = new MavlinkCommandLong(
            TargetSystemId: _systemId,
            TargetComponentId: _componentId,
            Command: 115, // MAV_CMD_DO_FOLLOW
            Confirmation: 0,
            Param5: (float)latitude,
            Param6: (float)longitude,
            Param7: (float)altitude);

        await _commandService.SendCommandLongAsync(link, command, cancellationToken).ConfigureAwait(false);
        _lastTarget = new FollowMeTarget(latitude, longitude, altitude, DateTimeOffset.UtcNow);
        _sentTargetCount++;
        _statusText = $"FollowMe target sent: {latitude:F6},{longitude:F6}.";
    }

    public Task StopAsync()
    {
        _isActive = false;
        _statusText = "FollowMe stopped.";
        return Task.CompletedTask;
    }

    private static bool IsValidTarget(double latitude, double longitude, double altitude)
    {
        return !double.IsNaN(latitude) &&
            !double.IsNaN(longitude) &&
            !double.IsNaN(altitude) &&
            latitude is >= -90 and <= 90 &&
            longitude is >= -180 and <= 180;
    }
}
