namespace VGC.Mavlink;

public sealed record MavlinkSysStatus(
    uint OnboardControlSensorsPresent,
    uint OnboardControlSensorsEnabled,
    uint OnboardControlSensorsHealth,
    ushort Load,
    ushort VoltageBattery,
    short CurrentBattery,
    byte BatteryRemaining,
    ushort DropRateComm,
    ushort ErrorsComm,
    ushort ErrorsCount1,
    ushort ErrorsCount2,
    ushort ErrorsCount3,
    ushort ErrorsCount4);

public sealed record MavlinkGlobalPositionInt(
    uint TimeBootMs,
    int LatitudeE7,
    int LongitudeE7,
    int AltitudeMillimeters,
    int RelativeAltitudeMillimeters,
    short VelocityNorthCms,
    short VelocityEastCms,
    short VelocityDownCms,
    ushort HeadingCentidegrees);

public sealed record MavlinkAttitude(
    uint TimeBootMs,
    float Roll,
    float Pitch,
    float Yaw,
    float RollSpeed,
    float PitchSpeed,
    float YawSpeed);

public sealed record MavlinkParameterValue(
    string Name,
    float Value,
    MavlinkParamType Type,
    ushort Count,
    ushort Index);
