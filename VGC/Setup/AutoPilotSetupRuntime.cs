using VGC.Facts;
using VGC.Firmware;
using VGC.Mavlink;
using VGC.Vehicles;

namespace VGC.Setup;

public enum SetupComponentRequirement
{
    Required,
    Optional
}

public sealed record AutoPilotSetupComponentState(
    string Id,
    string Title,
    SetupComponentRequirement Requirement,
    VehicleSetupReadiness Readiness,
    string StatusText,
    IReadOnlyList<string> MissingParameters)
{
    public bool IsBlocked => Readiness == VehicleSetupReadiness.Blocked;
}

public sealed class AutoPilotSetupComponentService
{
    private readonly VehicleSetupComponentCatalog _catalog;
    private readonly VehicleSetupStatusService _statusService;

    public AutoPilotSetupComponentService(
        VehicleSetupComponentCatalog? catalog = null,
        VehicleSetupStatusService? statusService = null)
    {
        _catalog = catalog ?? new VehicleSetupComponentCatalog();
        _statusService = statusService ?? new VehicleSetupStatusService();
    }

    public IReadOnlyList<AutoPilotSetupComponentState> Project(
        IFirmwarePlugin firmware,
        MavType vehicleType,
        ParameterManager parameterManager)
    {
        return _catalog.GetComponents(firmware, vehicleType)
            .Select(component => _statusService.Project(component, parameterManager))
            .Select(status => new AutoPilotSetupComponentState(
                status.Id,
                status.Title,
                status.IsRequired ? SetupComponentRequirement.Required : SetupComponentRequirement.Optional,
                status.Readiness,
                status.StatusText,
                status.MissingParameters))
            .ToArray();
    }
}

public enum CalibrationCommandKind
{
    Start,
    Cancel
}

public sealed record CalibrationCommandBoundary(
    SensorCalibrationType CalibrationType,
    CalibrationCommandKind Kind,
    MavlinkCommandLong Command,
    string SafetyWarning);

public static class CalibrationCommandFactory
{
    private const ushort MavCmdPreflightCalibration = 241;

    public static CalibrationCommandBoundary Create(
        SensorCalibrationType type,
        CalibrationCommandKind kind = CalibrationCommandKind.Start,
        byte targetSystemId = 1,
        byte targetComponentId = 1)
    {
        var (p1, p2, p3, p4, p5) = type switch
        {
            SensorCalibrationType.Gyroscope => (1f, 0f, 0f, 0f, 0f),
            SensorCalibrationType.Compass => (0f, 0f, 1f, 0f, 0f),
            SensorCalibrationType.Accelerometer => (0f, 0f, 0f, 0f, 1f),
            SensorCalibrationType.Level => (0f, 0f, 0f, 2f, 0f),
            _ => (0f, 0f, 0f, 0f, 0f)
        };

        if (kind == CalibrationCommandKind.Cancel)
        {
            p1 = -1f;
            p2 = p3 = p4 = p5 = 0f;
        }

        return new CalibrationCommandBoundary(
            type,
            kind,
            new MavlinkCommandLong(targetSystemId, targetComponentId, MavCmdPreflightCalibration, Param1: p1, Param2: p2, Param3: p3, Param4: p4, Param5: p5),
            kind == CalibrationCommandKind.Cancel
                ? "Cancelling calibration stops the active sensor workflow."
                : $"{type} calibration requires a stationary, safe vehicle setup.");
    }
}

public sealed record RadioChannelCalibration(
    int Channel,
    int Min,
    int Max,
    int Trim,
    string Function);

public sealed record ManualControlBoundary(
    short X,
    short Y,
    short Z,
    short R,
    ushort Buttons,
    string StatusText);

public sealed class RadioCalibrationService
{
    public IReadOnlyList<RadioChannelCalibration> BuildChannelMap(ParameterManager parameterManager)
    {
        return
        [
            CreateChannel(parameterManager, 1, "Roll", "RC1_MIN", "RC1_MAX", "RC1_TRIM"),
            CreateChannel(parameterManager, 2, "Pitch", "RC2_MIN", "RC2_MAX", "RC2_TRIM"),
            CreateChannel(parameterManager, 3, "Throttle", "RC3_MIN", "RC3_MAX", "RC3_TRIM"),
            CreateChannel(parameterManager, 4, "Yaw", "RC4_MIN", "RC4_MAX", "RC4_TRIM")
        ];
    }

    public ManualControlBoundary ProjectManualControl(short roll, short pitch, short throttle, short yaw, ushort buttons = 0)
    {
        return new ManualControlBoundary(
            ClampManual(roll),
            ClampManual(pitch),
            ClampThrottle(throttle),
            ClampManual(yaw),
            buttons,
            "MANUAL_CONTROL boundary values normalized.");
    }

    private static RadioChannelCalibration CreateChannel(
        ParameterManager parameterManager,
        int channel,
        string function,
        string minName,
        string maxName,
        string trimName)
    {
        return new RadioChannelCalibration(
            channel,
            ReadInt(parameterManager, minName, 1000),
            ReadInt(parameterManager, maxName, 2000),
            ReadInt(parameterManager, trimName, 1500),
            function);
    }

    private static int ReadInt(ParameterManager parameterManager, string name, int fallback)
    {
        var fact = parameterManager.Parameters.FirstOrDefault(fact => string.Equals(fact.Name, name, StringComparison.Ordinal));
        return fact?.RawValue is IConvertible value ? Convert.ToInt32(value) : fallback;
    }

    private static short ClampManual(short value) => (short)Math.Clamp((int)value, -1000, 1000);

    private static short ClampThrottle(short value) => (short)Math.Clamp((int)value, 0, 1000);
}

public sealed record PowerBatteryParameter(
    string Name,
    string Label,
    string Group,
    bool IsFailsafe,
    bool IsPresent);

public sealed record PowerBatterySetupProjection(
    IReadOnlyList<PowerBatteryParameter> Parameters,
    bool IsComplete,
    string StatusText);

public sealed class PowerBatterySetupService
{
    private static readonly PowerBatteryParameter[] Required =
    [
        new("BAT_LOW_THR", "Low Battery Threshold", "Power", true, false),
        new("BAT_CRIT_THR", "Critical Battery Threshold", "Power", true, false),
        new("BAT_V_EMPTY", "Empty Voltage", "Battery", false, false),
        new("BAT_V_CHARGED", "Charged Voltage", "Battery", false, false)
    ];

    public PowerBatterySetupProjection Project(ParameterManager parameterManager, IParameterMetadataCatalog metadataCatalog)
    {
        var parameters = Required
            .Select(required =>
            {
                var metadata = metadataCatalog.Find(1, required.Name);
                var present = parameterManager.Parameters.Any(fact => string.Equals(fact.Name, required.Name, StringComparison.Ordinal));
                return required with
                {
                    Label = metadata?.Label ?? required.Label,
                    Group = metadata?.Group ?? required.Group,
                    IsPresent = present
                };
            })
            .ToArray();

        var complete = parameters.All(static parameter => parameter.IsPresent);
        return new PowerBatterySetupProjection(
            parameters,
            complete,
            complete ? "Power and battery setup parameters present." : "Power and battery setup parameters are missing.");
    }
}

public sealed record SafetyMotorCommandBoundary(
    MotorSafetyActionType ActionType,
    MavlinkCommandLong Command,
    bool RequiresExplicitConfirmation,
    string SafetyNotice);

public static class SafetyMotorCommandFactory
{
    public static SafetyMotorCommandBoundary Create(
        MotorSafetyActionType actionType,
        byte targetSystemId = 1,
        byte targetComponentId = 1)
    {
        var commandId = actionType == MotorSafetyActionType.SafetyConfirm
            ? MavlinkCommandIds.ComponentArmDisarm
            : (ushort)209;
        var param1 = actionType == MotorSafetyActionType.SafetyConfirm ? 1f : 0f;

        return new SafetyMotorCommandBoundary(
            actionType,
            new MavlinkCommandLong(targetSystemId, targetComponentId, commandId, Param1: param1),
            true,
            "Requires explicit operator confirmation and real-hardware safety validation.");
    }
}
