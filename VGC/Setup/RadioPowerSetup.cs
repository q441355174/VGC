using VGC.Facts;

namespace VGC.Setup;

public sealed record RadioSetupStatus(
    bool IsComplete,
    IReadOnlyList<string> MissingParameters,
    string StatusText);

public sealed record PowerSetupStatus(
    bool IsComplete,
    IReadOnlyList<string> MissingParameters,
    string StatusText,
    string AndroidLifecycleRisk);

public sealed record RadioPowerSetupStatus(
    RadioSetupStatus Radio,
    PowerSetupStatus Power);

public sealed class RadioPowerSetupService
{
    private static readonly string[] RequiredRadioParameters =
    [
        "RC_MAP_ROLL",
        "RC_MAP_PITCH",
        "RC_MAP_THROTTLE",
        "RC_MAP_YAW"
    ];

    private static readonly string[] RequiredPowerParameters =
    [
        "BAT_LOW_THR",
        "BAT_CRIT_THR",
        "BAT_V_EMPTY",
        "BAT_V_CHARGED"
    ];

    public RadioPowerSetupStatus Project(ParameterManager parameterManager)
    {
        var radioMissing = MissingParameters(parameterManager, RequiredRadioParameters);
        var powerMissing = MissingParameters(parameterManager, RequiredPowerParameters);

        return new RadioPowerSetupStatus(
            new RadioSetupStatus(
                radioMissing.Count == 0,
                radioMissing,
                radioMissing.Count == 0
                    ? "Radio channel mapping parameters present"
                    : $"Missing radio parameters: {string.Join(", ", radioMissing)}"),
            new PowerSetupStatus(
                powerMissing.Count == 0,
                powerMissing,
                powerMissing.Count == 0
                    ? "Power setup parameters present"
                    : $"Missing power parameters: {string.Join(", ", powerMissing)}",
                "Android validation must cover background/restore while editing battery parameters."));
    }

    private static IReadOnlyList<string> MissingParameters(ParameterManager parameterManager, IReadOnlyList<string> required)
    {
        return required
            .Where(parameter => !parameterManager.Parameters.Any(fact => string.Equals(fact.Name, parameter, StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
