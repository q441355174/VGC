namespace VGC.Validation;

public enum GuardedSitlCommandAction
{
    ParameterList,
    ParameterWrite,
    MissionUpload,
    ModeChange,
    ArmDisarm
}

public enum GuardedSitlCommandDisposition
{
    DryRunOnly,
    Allowed,
    Blocked
}

public sealed record GuardedSitlCommandTranscriptEntry(
    GuardedSitlCommandAction Action,
    GuardedSitlCommandDisposition Disposition,
    string Reason,
    bool SendsVehicleCommand);

public sealed class GuardedSitlCommandTranscriptPlan
{
    public IReadOnlyList<GuardedSitlCommandTranscriptEntry> BuildDryRunPlan() =>
    [
        new(GuardedSitlCommandAction.ParameterList, GuardedSitlCommandDisposition.DryRunOnly, "Observe or request list only when scenario authorization is present.", false),
        new(GuardedSitlCommandAction.ParameterWrite, GuardedSitlCommandDisposition.Blocked, "Parameter mutation requires explicit SITL scenario authorization.", true),
        new(GuardedSitlCommandAction.MissionUpload, GuardedSitlCommandDisposition.Blocked, "Mission mutation requires explicit SITL scenario authorization.", true),
        new(GuardedSitlCommandAction.ModeChange, GuardedSitlCommandDisposition.Blocked, "Mode change requires explicit SITL scenario authorization.", true),
        new(GuardedSitlCommandAction.ArmDisarm, GuardedSitlCommandDisposition.Blocked, "Arm/disarm is safety-sensitive and requires explicit authorization.", true)
    ];
}

public sealed class GuardedSitlCommandTranscriptAudit
{
    public bool SendsNoVehicleCommands(IReadOnlyList<GuardedSitlCommandTranscriptEntry> entries) =>
        entries.All(static entry => entry.Disposition != GuardedSitlCommandDisposition.Allowed || !entry.SendsVehicleCommand);
}
