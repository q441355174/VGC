using System.Collections.ObjectModel;
using ReactiveUI;
using VGC.Comms;
using VGC.Facts;
using VGC.Firmware;
using VGC.Mavlink;
using VGC.Mission;

namespace VGC.Vehicles;

public sealed class Vehicle : ReactiveObject
{
    private const int MaxStatusMessageCount = 200;
    private readonly ObservableCollection<VehicleStatusMessage> _statusMessages = new();
    private readonly FirmwarePluginManager _firmwarePluginManager = new();
    private readonly FirmwareFlightModeResolver _flightModeResolver = new();
    private DateTimeOffset _lastHeartbeatAt;
    private DateTimeOffset _lastPacketAt;
    private VehicleCoordinate? _coordinate;
    private double? _relativeAltitudeMeters;
    private double? _batteryVoltage;
    private int? _batteryRemainingPercent;
    private int? _gpsFixType;
    private int? _satelliteCount;
    private byte? _systemStatus;
    private VehicleFlightModeState _flightModeState = VehicleFlightModeState.FromHeartbeat(0, 0);
    private double? _pitchDegrees;
    private double? _rollDegrees;
    private double? _headingDegrees;
    private double? _groundSpeedMs;
    private double? _airspeedMs;
    private double? _throttlePercent;
    private ushort? _communicationDropRatePermille;
    private ushort? _communicationErrors;
    private bool _estimatorOk;

    public Vehicle(byte id, byte componentId, MavAutopilot autopilot, MavType vehicleType)
    {
        Id = id;
        ComponentId = componentId;
        Autopilot = autopilot;
        VehicleType = vehicleType;
        ParameterManager = new ParameterManager();
        MissionTransferManager = new MissionTransferManager();
        GeoFenceTransferManager = new GeoFenceTransferManager();
        RallyPointTransferManager = new RallyPointTransferManager();
        MessageIntervalManager = new MessageIntervalManager(id, componentId);
        MessageCoordinator = new RequestMessageCoordinator(id, componentId);
        StatusMessages = new ReadOnlyObservableCollection<VehicleStatusMessage>(_statusMessages);
        _lastHeartbeatAt = DateTimeOffset.Now;
        _lastPacketAt = _lastHeartbeatAt;
    }

    public byte Id { get; }

    public byte ComponentId { get; }

    public MavAutopilot Autopilot { get; }

    public MavType VehicleType { get; }

    public ParameterManager ParameterManager { get; }

    public MissionTransferManager MissionTransferManager { get; }

    public VehicleLinkManager LinkManager { get; } = new();

    public VehicleCapabilitiesService Capabilities { get; } = new();

    public MessageIntervalManager MessageIntervalManager { get; }

    public InitialConnectService InitialConnect { get; } = new();

    public RequestMessageCoordinator MessageCoordinator { get; }

    public BatteryFactGroup Battery { get; } = new();

    public GpsFactGroup Gps { get; } = new();

    public RadioFactGroup Radio { get; } = new();

    public AttitudeFactGroup Attitude { get; } = new();

    public EkfFactGroup Ekf { get; } = new();

    public VibrationFactGroup Vibration { get; } = new();

    public WindFactGroup Wind { get; } = new();

    public MissionTransferService? MissionTransferService { get; private set; }

    public GeoFenceTransferManager GeoFenceTransferManager { get; }

    public RallyPointTransferManager RallyPointTransferManager { get; }

    public GeoFenceTransferService? GeoFenceTransferService { get; private set; }

    public RallyPointTransferService? RallyPointTransferService { get; private set; }

    public GeoFencePlan LastGeoFencePlan => GeoFenceTransferService?.LastReadPlan ?? new GeoFencePlan();

    public RallyPointsPlan LastRallyPointsPlan => RallyPointTransferService?.LastReadPlan ?? new RallyPointsPlan();

    public ReadOnlyObservableCollection<VehicleStatusMessage> StatusMessages { get; }

    public DateTimeOffset LastHeartbeatAt
    {
        get => _lastHeartbeatAt;
        private set => this.RaiseAndSetIfChanged(ref _lastHeartbeatAt, value);
    }

    public DateTimeOffset LastPacketAt
    {
        get => _lastPacketAt;
        private set => this.RaiseAndSetIfChanged(ref _lastPacketAt, value);
    }

    public VehicleCoordinate? Coordinate
    {
        get => _coordinate;
        private set => this.RaiseAndSetIfChanged(ref _coordinate, value);
    }

    public double? RelativeAltitudeMeters
    {
        get => _relativeAltitudeMeters;
        private set => this.RaiseAndSetIfChanged(ref _relativeAltitudeMeters, value);
    }

    public double? BatteryVoltage
    {
        get => _batteryVoltage;
        private set => this.RaiseAndSetIfChanged(ref _batteryVoltage, value);
    }

    public int? BatteryRemainingPercent
    {
        get => _batteryRemainingPercent;
        private set => this.RaiseAndSetIfChanged(ref _batteryRemainingPercent, value);
    }

    public int? GpsFixType
    {
        get => _gpsFixType;
        private set => this.RaiseAndSetIfChanged(ref _gpsFixType, value);
    }

    public int? SatelliteCount
    {
        get => _satelliteCount;
        private set => this.RaiseAndSetIfChanged(ref _satelliteCount, value);
    }

    public byte? SystemStatus
    {
        get => _systemStatus;
        private set => this.RaiseAndSetIfChanged(ref _systemStatus, value);
    }

    public byte BaseMode => FlightModeState.BaseMode;

    public uint CustomMode => FlightModeState.CustomMode;

    public string FlightModeName => FlightModeState.Name;

    public VehicleFlightModeState FlightModeState
    {
        get => _flightModeState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _flightModeState, value);
            this.RaisePropertyChanged(nameof(BaseMode));
            this.RaisePropertyChanged(nameof(CustomMode));
            this.RaisePropertyChanged(nameof(FlightModeName));
        }
    }

    public void MarkHeartbeat(byte systemStatus, byte baseMode = 0, uint customMode = 0)
    {
        LastHeartbeatAt = DateTimeOffset.Now;
        LastPacketAt = LastHeartbeatAt;
        SystemStatus = systemStatus;
        FlightModeState = _flightModeResolver.Resolve(_firmwarePluginManager.GetPlugin(Autopilot), baseMode, customMode);
    }

    public VehicleCommandService CreateCommandService(ILinkTransport link, MavlinkCommandService? commandService = null, VehicleCommandQueue? commandQueue = null)
    {
        return new VehicleCommandService(this, link, commandService, commandQueue);
    }

    public void AttachPlanTransferLink(ILinkTransport link)
    {
        MissionTransferService = new MissionTransferService(
            link,
            Id,
            ComponentId,
            MissionTransferManager);
        GeoFenceTransferService = new GeoFenceTransferService(
            link,
            Id,
            ComponentId,
            GeoFenceTransferManager);
        RallyPointTransferService = new RallyPointTransferService(
            link,
            Id,
            ComponentId,
            RallyPointTransferManager);
    }

    public bool ApplyPacket(Mavlink.MavlinkPacket packet)
    {
        LastPacketAt = DateTimeOffset.Now;
        return packet.MessageId switch
        {
            1 => ApplySysStatus(packet.Payload),
            22 => ApplyParamValue(packet.ComponentId, packet.Payload),
            24 => ApplyGpsRawInt(packet.Payload),
            30 => ApplyAttitude(packet.Payload),
            33 => ApplyGlobalPositionInt(packet.Payload),
            44 or 47 or 51 or 73 => ApplyMissionPacket(packet),
            74 => ApplyVfrHud(packet.Payload),
            110 => ApplyHomePosition(packet.Payload),
            147 => ApplyBatteryStatus(packet.Payload),
            253 => ApplyStatusText(packet),
            256 => ApplyEstimatorStatus(packet.Payload),
            266 => ApplyWindCov(packet.Payload),
            331 => ApplyOdometry(packet.Payload),
            340 => ApplyExtendedSysState(packet.Payload),
            395 => ApplyComponentInformation(packet.Payload),
            _ => false
        };
    }

    private bool ApplyMissionPacket(MavlinkPacket packet)
    {
        var missionType = ReadMissionType(packet);
        return missionType switch
        {
            MavMissionType.Fence => ApplySectionPacket(GeoFenceTransferService, packet),
            MavMissionType.Rally => ApplySectionPacket(RallyPointTransferService, packet),
            _ => ApplyMissionPacket(MissionTransferService, packet)
        };
    }

    private static MavMissionType ReadMissionType(MavlinkPacket packet)
    {
        if (MavlinkMissionService.TryReadMissionCount(packet, out var count))
        {
            return count.MissionType;
        }

        if (MavlinkMissionService.TryReadMissionAck(packet, out var ack))
        {
            return ack.MissionType;
        }

        if (MavlinkMissionService.TryReadMissionRequestInt(packet, out var request))
        {
            return request.MissionType;
        }

        if (MavlinkMissionService.TryReadMissionItemInt(packet, out var item))
        {
            return item.MissionType;
        }

        return MavMissionType.Mission;
    }

    private static bool ApplySectionPacket(GeoFenceTransferService? service, MavlinkPacket packet)
    {
        if (service is null)
        {
            return false;
        }

        service.HandlePacketAsync(packet).GetAwaiter().GetResult();
        return true;
    }

    private static bool ApplySectionPacket(RallyPointTransferService? service, MavlinkPacket packet)
    {
        if (service is null)
        {
            return false;
        }

        service.HandlePacketAsync(packet).GetAwaiter().GetResult();
        return true;
    }

    private bool ApplyMissionPacket(MissionTransferService? service, MavlinkPacket packet)
    {
        if (service is null)
        {
            return MissionTransferManager.ApplyPacket(packet);
        }

        service.HandlePacketAsync(packet).GetAwaiter().GetResult();
        return true;
    }

    private bool ApplyStatusText(MavlinkPacket packet)
    {
        if (!MavlinkStatusTextParser.TryRead(packet, out var statusText))
        {
            return false;
        }

        _statusMessages.Add(new VehicleStatusMessage(statusText.ComponentId, statusText.Severity, statusText.Text, DateTimeOffset.Now));
        while (_statusMessages.Count > MaxStatusMessageCount)
        {
            _statusMessages.RemoveAt(0);
        }

        return true;
    }

    private bool ApplySysStatus(byte[] payload)
    {
        if (payload.Length < 31)
        {
            return false;
        }

        var voltageMillivolts = BitConverter.ToUInt16(payload, 14);
        var batteryRemaining = unchecked((sbyte)payload[30]);
        BatteryVoltage = voltageMillivolts == ushort.MaxValue ? null : voltageMillivolts / 1000.0;
        BatteryRemainingPercent = batteryRemaining < 0 ? null : batteryRemaining;
        CommunicationDropRatePermille = BitConverter.ToUInt16(payload, 24);
        CommunicationErrors = BitConverter.ToUInt16(payload, 26);
        Battery.UpdateFromVehicle(this);
        return true;
    }

    private bool ApplyParamValue(int componentId, byte[] payload)
    {
        if (!ParameterManager.ApplyParamValuePayload(componentId, payload))
        {
            return false;
        }

        InitialConnect.MarkParameterReceived();
        return true;
    }

    private bool ApplyGpsRawInt(byte[] payload)
    {
        if (payload.Length < 30)
        {
            return false;
        }

        GpsFixType = payload[8];
        SatelliteCount = payload[29] == byte.MaxValue ? null : payload[29];
        Gps.UpdateFromVehicle(this);
        return true;
    }

    private bool ApplyGlobalPositionInt(byte[] payload)
    {
        if (payload.Length < 28)
        {
            return false;
        }

        var latitude = BitConverter.ToInt32(payload, 4) / 10000000.0;
        var longitude = BitConverter.ToInt32(payload, 8) / 10000000.0;
        var altitudeMeters = BitConverter.ToInt32(payload, 12) / 1000.0;
        Coordinate = new VehicleCoordinate(latitude, longitude, altitudeMeters);
        RelativeAltitudeMeters = BitConverter.ToInt32(payload, 16) / 1000.0;
        return true;
    }

    public double? PitchDegrees
    {
        get => _pitchDegrees;
        private set => this.RaiseAndSetIfChanged(ref _pitchDegrees, value);
    }

    public double? RollDegrees
    {
        get => _rollDegrees;
        private set => this.RaiseAndSetIfChanged(ref _rollDegrees, value);
    }

    public double? HeadingDegrees
    {
        get => _headingDegrees;
        private set => this.RaiseAndSetIfChanged(ref _headingDegrees, value);
    }

    public double? GroundSpeedMs
    {
        get => _groundSpeedMs;
        private set => this.RaiseAndSetIfChanged(ref _groundSpeedMs, value);
    }

    public double? AirspeedMs
    {
        get => _airspeedMs;
        private set => this.RaiseAndSetIfChanged(ref _airspeedMs, value);
    }

    public double? ThrottlePercent
    {
        get => _throttlePercent;
        private set => this.RaiseAndSetIfChanged(ref _throttlePercent, value);
    }

    public ushort? CommunicationDropRatePermille
    {
        get => _communicationDropRatePermille;
        private set => this.RaiseAndSetIfChanged(ref _communicationDropRatePermille, value);
    }

    public ushort? CommunicationErrors
    {
        get => _communicationErrors;
        private set => this.RaiseAndSetIfChanged(ref _communicationErrors, value);
    }

    public bool EstimatorOk
    {
        get => _estimatorOk;
        private set => this.RaiseAndSetIfChanged(ref _estimatorOk, value);
    }

    private bool _isCommunicationLost;
    private VehicleCoordinate? _homePosition;

    public VehicleCoordinate? HomePosition
    {
        get => _homePosition;
        private set => this.RaiseAndSetIfChanged(ref _homePosition, value);
    }

    public bool IsCommunicationLost
    {
        get => _isCommunicationLost;
        private set => this.RaiseAndSetIfChanged(ref _isCommunicationLost, value);
    }

    public void MarkCommunicationLost()
    {
        IsCommunicationLost = true;
    }

    public void MarkCommunicationRestored()
    {
        IsCommunicationLost = false;
    }

    private bool ApplyAttitude(byte[] payload)
    {
        if (payload.Length < 28)
        {
            return false;
        }

        PitchDegrees = BitConverter.ToSingle(payload, 4) * (180.0 / Math.PI);
        RollDegrees = BitConverter.ToSingle(payload, 0) * (180.0 / Math.PI);
        HeadingDegrees = BitConverter.ToSingle(payload, 8) * (180.0 / Math.PI);
        Attitude.UpdateFromVehicle(this);
        return true;
    }

    private bool ApplyVfrHud(byte[] payload)
    {
        if (payload.Length < 20)
        {
            return false;
        }

        AirspeedMs = BitConverter.ToSingle(payload, 0);
        GroundSpeedMs = BitConverter.ToSingle(payload, 4);
        HeadingDegrees = BitConverter.ToInt16(payload, 12);
        ThrottlePercent = BitConverter.ToSingle(payload, 16);
        return true;
    }

    private bool ApplyBatteryStatus(byte[] payload)
    {
        if (payload.Length < 36)
        {
            return false;
        }

        var voltages = new ushort[10];
        for (var i = 0; i < 10; i++)
        {
            voltages[i] = BitConverter.ToUInt16(payload, 10 + i * 2);
        }

        if (voltages[0] != ushort.MaxValue)
        {
            BatteryVoltage = voltages[0] / 1000.0;
        }

        var batteryRemaining = unchecked((sbyte)payload[35]);
        if (batteryRemaining >= 0)
        {
            BatteryRemainingPercent = batteryRemaining;
        }

        return true;
    }

    private bool ApplyEstimatorStatus(byte[] payload)
    {
        if (payload.Length < 40)
        {
            return false;
        }

        var flags = BitConverter.ToUInt16(payload, 8);
        EstimatorOk = flags > 0;
        Ekf.Update(EstimatorOk, flags);
        return true;
    }

    public string VehicleName { get; private set; } = "Unknown";

    public string FlightStackVersion { get; private set; } = "Unknown";

    private bool ApplyComponentInformation(byte[] payload)
    {
        if (payload.Length < 40)
        {
            return false;
        }

        var componentType = payload[0];
        if (componentType != 1) // MAV_COMPONENT_TYPE_AUTOPILOT
        {
            return true;
        }

        var firmwareVersion = BitConverter.ToUInt32(payload, 4);
        var hardwareVersion = BitConverter.ToUInt32(payload, 8);
        FlightStackVersion = $"v{firmwareVersion >> 24}.{(firmwareVersion >> 16) & 0xFF}.{firmwareVersion & 0xFFFF}";
        return true;
    }

    private bool ApplyHomePosition(byte[] payload)
    {
        if (payload.Length < 28)
        {
            return false;
        }

        var latitude = BitConverter.ToInt32(payload, 0) / 10000000.0;
        var longitude = BitConverter.ToInt32(payload, 4) / 10000000.0;
        var altitude = BitConverter.ToInt32(payload, 8) / 1000.0;
        HomePosition = new VehicleCoordinate(latitude, longitude, altitude);
        return true;
    }

    private bool ApplyWindCov(byte[] payload)
    {
        if (payload.Length < 16)
        {
            return false;
        }

        var windSpeed = BitConverter.ToSingle(payload, 4);
        var windDir = BitConverter.ToSingle(payload, 12);
        Wind.Speed?.SetRawValue(windSpeed);
        Wind.Direction?.SetRawValue(windDir);
        return true;
    }

    private bool ApplyOdometry(byte[] payload)
    {
        if (payload.Length < 20)
        {
            return false;
        }

        var vx = BitConverter.ToSingle(payload, 8);
        var vy = BitConverter.ToSingle(payload, 12);
        var vz = BitConverter.ToSingle(payload, 16);
        GroundSpeedMs = Math.Sqrt(vx * vx + vy * vy);
        return true;
    }

    private bool ApplyExtendedSysState(byte[] payload)
    {
        if (payload.Length < 2)
        {
            return false;
        }

        var landedState = payload[0];
        var vtolState = payload[1];
        return true;
    }
}
