namespace VGC.Validation;

public sealed record SitlCommandAuthorizationPolicy(
    bool IsAuthorized,
    string ScenarioName,
    IReadOnlyList<GuardedSitlCommandAction> AllowedActions);

public sealed class SitlCommandAuthorizationGate
{
    public bool CanExecute(SitlCommandAuthorizationPolicy policy, GuardedSitlCommandAction action) =>
        policy.IsAuthorized && policy.AllowedActions.Contains(action);

    public string Explain(SitlCommandAuthorizationPolicy policy, GuardedSitlCommandAction action) =>
        CanExecute(policy, action)
            ? $"Authorized by scenario '{policy.ScenarioName}' for {action}."
            : $"Blocked: scenario '{policy.ScenarioName}' does not authorize {action}.";
}
