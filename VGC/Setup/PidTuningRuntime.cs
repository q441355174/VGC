using VGC.Facts;

namespace VGC.Setup;

public sealed record PidParameter(
    string Name,
    string Label,
    string Group,
    double Value,
    double Min,
    double Max,
    double Step,
    string Units);

public sealed record PidTuningGroup(
    string Name,
    IReadOnlyList<PidParameter> Parameters);

public sealed record PidTuningSnapshot(
    IReadOnlyList<PidTuningGroup> Groups,
    bool IsLoaded,
    string StatusText);

public sealed class PidTuningRuntime
{
    private static readonly PidParameterDefinition[] ArduCopterParameters =
    [
        // Rate Roll
        new("ATC_RAT_RLL_P", "Roll Rate P", "Rate Roll", 0.135, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_RLL_I", "Roll Rate I", "Rate Roll", 0.135, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_RLL_D", "Roll Rate D", "Rate Roll", 0.0036, 0.0, 0.02, 0.0001, ""),
        new("ATC_RAT_RLL_FF", "Roll Rate FF", "Rate Roll", 0.0, 0.0, 0.5, 0.001, ""),
        new("ATC_RAT_RLL_FLTD", "Roll Rate D Filter", "Rate Roll", 20.0, 0.0, 100.0, 1.0, "Hz"),
        new("ATC_RAT_RLL_FLTT", "Roll Rate Target Filter", "Rate Roll", 20.0, 0.0, 100.0, 1.0, "Hz"),

        // Rate Pitch
        new("ATC_RAT_PIT_P", "Pitch Rate P", "Rate Pitch", 0.135, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_PIT_I", "Pitch Rate I", "Rate Pitch", 0.135, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_PIT_D", "Pitch Rate D", "Rate Pitch", 0.0036, 0.0, 0.02, 0.0001, ""),
        new("ATC_RAT_PIT_FF", "Pitch Rate FF", "Rate Pitch", 0.0, 0.0, 0.5, 0.001, ""),
        new("ATC_RAT_PIT_FLTD", "Pitch Rate D Filter", "Rate Pitch", 20.0, 0.0, 100.0, 1.0, "Hz"),
        new("ATC_RAT_PIT_FLTT", "Pitch Rate Target Filter", "Rate Pitch", 20.0, 0.0, 100.0, 1.0, "Hz"),

        // Rate Yaw
        new("ATC_RAT_YAW_P", "Yaw Rate P", "Rate Yaw", 0.18, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_YAW_I", "Yaw Rate I", "Rate Yaw", 0.018, 0.0, 0.5, 0.005, ""),
        new("ATC_RAT_YAW_D", "Yaw Rate D", "Rate Yaw", 0.0, 0.0, 0.02, 0.0001, ""),
        new("ATC_RAT_YAW_FF", "Yaw Rate FF", "Rate Yaw", 0.0, 0.0, 0.5, 0.001, ""),
        new("ATC_RAT_YAW_FLTD", "Yaw Rate D Filter", "Rate Yaw", 20.0, 0.0, 100.0, 1.0, "Hz"),
        new("ATC_RAT_YAW_FLTT", "Yaw Rate Target Filter", "Rate Yaw", 20.0, 0.0, 100.0, 1.0, "Hz"),

        // Stabilize
        new("ATC_ANG_RLL_P", "Roll Angle P", "Stabilize Roll", 4.5, 0.0, 12.0, 0.1, ""),
        new("ATC_ANG_PIT_P", "Pitch Angle P", "Stabilize Pitch", 4.5, 0.0, 12.0, 0.1, ""),
        new("ATC_ANG_YAW_P", "Yaw Angle P", "Stabilize Yaw", 4.5, 0.0, 12.0, 0.1, "")
    ];

    private static readonly PidParameterDefinition[] Px4Parameters =
    [
        // Rate Roll
        new("MC_ROLLRATE_P", "Roll Rate P", "Rate Roll", 0.15, 0.0, 0.5, 0.01, ""),
        new("MC_ROLLRATE_I", "Roll Rate I", "Rate Roll", 0.2, 0.0, 0.5, 0.01, ""),
        new("MC_ROLLRATE_D", "Roll Rate D", "Rate Roll", 0.003, 0.0, 0.01, 0.0005, ""),
        new("MC_ROLLRATE_FF", "Roll Rate FF", "Rate Roll", 0.0, 0.0, 0.5, 0.01, ""),
        new("MC_RR_INT_LIM", "Roll Rate Integrator Limit", "Rate Roll", 0.3, 0.0, 1.0, 0.01, ""),

        // Rate Pitch
        new("MC_PITCHRATE_P", "Pitch Rate P", "Rate Pitch", 0.15, 0.0, 0.5, 0.01, ""),
        new("MC_PITCHRATE_I", "Pitch Rate I", "Rate Pitch", 0.2, 0.0, 0.5, 0.01, ""),
        new("MC_PITCHRATE_D", "Pitch Rate D", "Rate Pitch", 0.003, 0.0, 0.01, 0.0005, ""),
        new("MC_PITCHRATE_FF", "Pitch Rate FF", "Rate Pitch", 0.0, 0.0, 0.5, 0.01, ""),
        new("MC_PR_INT_LIM", "Pitch Rate Integrator Limit", "Rate Pitch", 0.3, 0.0, 1.0, 0.01, ""),

        // Rate Yaw
        new("MC_YAWRATE_P", "Yaw Rate P", "Rate Yaw", 0.2, 0.0, 0.6, 0.01, ""),
        new("MC_YAWRATE_I", "Yaw Rate I", "Rate Yaw", 0.1, 0.0, 0.5, 0.01, ""),
        new("MC_YAWRATE_D", "Yaw Rate D", "Rate Yaw", 0.0, 0.0, 0.01, 0.0005, ""),
        new("MC_YAWRATE_FF", "Yaw Rate FF", "Rate Yaw", 0.0, 0.0, 0.5, 0.01, ""),
        new("MC_YR_INT_LIM", "Yaw Rate Integrator Limit", "Rate Yaw", 0.3, 0.0, 1.0, 0.01, ""),

        // Attitude
        new("MC_ROLL_P", "Roll Angle P", "Attitude Roll", 6.5, 0.0, 14.0, 0.1, ""),
        new("MC_PITCH_P", "Pitch Angle P", "Attitude Pitch", 6.5, 0.0, 14.0, 0.1, ""),
        new("MC_YAW_P", "Yaw Angle P", "Attitude Yaw", 2.8, 0.0, 5.0, 0.1, "")
    ];

    private readonly Dictionary<string, double> _overrides = new(StringComparer.Ordinal);
    private PidParameterDefinition[] _definitions = [];
    private bool _isLoaded;

    public PidTuningSnapshot Snapshot => BuildSnapshot();

    public PidTuningSnapshot LoadFromParameters(ParameterManager parameterManager)
    {
        var isArduPilot = parameterManager.Parameters.Any(static f =>
            f.Name.StartsWith("ATC_", StringComparison.Ordinal));

        _definitions = isArduPilot ? ArduCopterParameters : Px4Parameters;
        _overrides.Clear();

        foreach (var definition in _definitions)
        {
            var fact = parameterManager.Parameters
                .FirstOrDefault(f => string.Equals(f.Name, definition.Name, StringComparison.Ordinal));

            if (fact?.RawValue is IConvertible convertible)
            {
                _overrides[definition.Name] = Convert.ToDouble(convertible);
            }
        }

        _isLoaded = true;
        return BuildSnapshot();
    }

    public PidTuningSnapshot UpdateParameter(string name, double value)
    {
        var definition = _definitions.FirstOrDefault(d =>
            string.Equals(d.Name, name, StringComparison.Ordinal));

        if (definition is not null)
        {
            _overrides[name] = Math.Clamp(value, definition.Min, definition.Max);
        }

        return BuildSnapshot();
    }

    private PidTuningSnapshot BuildSnapshot()
    {
        var groups = _definitions
            .GroupBy(static d => d.Group)
            .Select(g => new PidTuningGroup(
                g.Key,
                g.Select(d => new PidParameter(
                    d.Name,
                    d.Label,
                    d.Group,
                    _overrides.TryGetValue(d.Name, out var v) ? v : d.DefaultValue,
                    d.Min,
                    d.Max,
                    d.Step,
                    d.Units)).ToArray()))
            .ToArray();

        var parameterCount = _definitions.Length;
        var loadedCount = _overrides.Count;
        return new PidTuningSnapshot(
            groups,
            _isLoaded,
            _isLoaded
                ? $"{loadedCount}/{parameterCount} PID parameters loaded"
                : "PID parameters not loaded");
    }

    private sealed record PidParameterDefinition(
        string Name,
        string Label,
        string Group,
        double DefaultValue,
        double Min,
        double Max,
        double Step,
        string Units);
}
