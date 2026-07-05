using System.Collections.ObjectModel;
using Avalonia.Threading;
using ReactiveUI;
using VGC.Comms;
using VGC.Core.Logging;
using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed class MultiVehicleManager : ReactiveObject
{
    private readonly ObservableCollection<Vehicle> _vehicles = new();
    private readonly IAppLogger _logger;
    private Vehicle? _activeVehicle;

    public MultiVehicleManager(MavlinkProtocol mavlinkProtocol, IAppLogger logger)
    {
        _logger = logger;
        Vehicles = new ReadOnlyObservableCollection<Vehicle>(_vehicles);
        mavlinkProtocol.HeartbeatReceived += OnHeartbeatReceived;
        mavlinkProtocol.PacketReceived += OnPacketReceived;
    }

    public event EventHandler? VehiclesChanged;

    public event EventHandler? VehicleUpdated;

    public event EventHandler<Vehicle>? CommunicationLost;

    public event EventHandler<Vehicle>? CommunicationRestored;

    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(15);

    public ReadOnlyObservableCollection<Vehicle> Vehicles { get; }

    public void CheckHeartbeatTimeouts()
    {
        var now = DateTimeOffset.Now;
        foreach (var vehicle in _vehicles.ToArray())
        {
            var timeSinceLastHeartbeat = now - vehicle.LastHeartbeatAt;
            if (timeSinceLastHeartbeat > HeartbeatTimeout && !vehicle.IsCommunicationLost)
            {
                vehicle.MarkCommunicationLost();
                CommunicationLost?.Invoke(this, vehicle);
                _logger.Warning($"Vehicle {vehicle.Id} communication lost (last heartbeat {timeSinceLastHeartbeat.TotalSeconds:F0}s ago).");
            }
            else if (timeSinceLastHeartbeat <= HeartbeatTimeout && vehicle.IsCommunicationLost)
            {
                vehicle.MarkCommunicationRestored();
                CommunicationRestored?.Invoke(this, vehicle);
                _logger.Info($"Vehicle {vehicle.Id} communication restored.");
            }
        }
    }

    public Vehicle? ActiveVehicle
    {
        get => _activeVehicle;
        private set => this.RaiseAndSetIfChanged(ref _activeVehicle, value);
    }

    private void OnHeartbeatReceived(object? sender, MavlinkHeartbeat heartbeat)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyHeartbeat(heartbeat);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyHeartbeat(heartbeat));
    }

    private void ApplyHeartbeat(MavlinkHeartbeat heartbeat, ILinkTransport? link = null)
    {
        if (!IsVehicleHeartbeat(heartbeat))
        {
            return;
        }

        var vehicle = _vehicles.FirstOrDefault(v => v.Id == heartbeat.SystemId);
        if (vehicle is null)
        {
            vehicle = new Vehicle(heartbeat.SystemId, heartbeat.ComponentId, heartbeat.Autopilot, heartbeat.VehicleType);
            if (link is not null)
            {
                vehicle.LinkManager.ActiveLink = link;
            }

            vehicle.MarkHeartbeat(heartbeat.SystemStatus, heartbeat.BaseMode, heartbeat.CustomMode);
            vehicle.InitialConnect.MarkHeartbeatReceived();
            _vehicles.Add(vehicle);
            ActiveVehicle ??= vehicle;
            VehiclesChanged?.Invoke(this, EventArgs.Empty);
            _logger.Info($"Vehicle {vehicle.Id} detected: {vehicle.Autopilot}/{vehicle.VehicleType}.");
            BeginInitialConnect(vehicle, link);
            return;
        }

        if (link is not null && !ReferenceEquals(vehicle.LinkManager.ActiveLink, link))
        {
            vehicle.LinkManager.ActiveLink = link;
        }

        vehicle.MarkHeartbeat(heartbeat.SystemStatus, heartbeat.BaseMode, heartbeat.CustomMode);
        vehicle.InitialConnect.MarkHeartbeatReceived();
        VehicleUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void OnPacketReceived(object? sender, MavlinkPacket packet)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyPacket(packet);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplyPacket(packet));
    }

    private void ApplyPacket(MavlinkPacket packet)
    {
        if (TryReadHeartbeat(packet, out var heartbeat))
        {
            ApplyHeartbeat(heartbeat, packet.Link);
            return;
        }

        var vehicle = _vehicles.FirstOrDefault(v => v.Id == packet.SystemId);
        if (vehicle is null)
        {
            return;
        }

        if (vehicle.ApplyPacket(packet))
        {
            VehicleUpdated?.Invoke(this, EventArgs.Empty);
        }
    }

    private async void BeginInitialConnect(Vehicle vehicle, ILinkTransport? link)
    {
        if (link is not { IsConnected: true, CanSend: true }
            || vehicle.InitialConnect.State != InitialConnectState.WaitingForHeartbeat)
        {
            return;
        }

        try
        {
            await vehicle.MessageIntervalManager.SetDefaultRatesAsync(link).ConfigureAwait(false);
            vehicle.InitialConnect.BeginParameterRequest(expectedCount: 0);
            var parameterService = new MavlinkParameterService();
            await parameterService.SendParamRequestListAsync(
                link,
                new MavlinkParameterRequestList(vehicle.Id, vehicle.ComponentId)).ConfigureAwait(false);
            _logger.Info($"Vehicle {vehicle.Id} initial connect requested stream rates and parameters.");
        }
        catch (Exception ex)
        {
            vehicle.InitialConnect.MarkFailed(ex.Message);
            _logger.Error($"Vehicle {vehicle.Id} initial connect failed: {ex.Message}");
        }
    }

    private static bool IsVehicleHeartbeat(MavlinkHeartbeat heartbeat)
    {
        if (heartbeat.SystemId == 0 || heartbeat.ComponentId != 1)
        {
            return false;
        }

        return heartbeat.VehicleType is not (MavType.Gcs or MavType.OnboardController or MavType.Gimbal or MavType.Adsb);
    }

    private static bool TryReadHeartbeat(MavlinkPacket packet, out MavlinkHeartbeat heartbeat)
    {
        heartbeat = default!;
        if (packet.MessageId != MavlinkMessageIds.Heartbeat || packet.Payload.Length < 9)
        {
            return false;
        }

        heartbeat = new MavlinkHeartbeat(
            packet.SystemId,
            packet.ComponentId,
            (MavAutopilot)packet.Payload[5],
            (MavType)packet.Payload[4],
            packet.Payload[6],
            BitConverter.ToUInt32(packet.Payload, 0),
            packet.Payload[7]);
        return true;
    }
}
