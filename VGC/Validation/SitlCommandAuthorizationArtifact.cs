namespace VGC.Validation;

public sealed class SitlCommandAuthorizationArtifactWriter
{
    public string Write(string outputPath, SitlCommandAuthorizationPolicy policy, GuardedSitlCommandAction action)
    {
        var gate = new SitlCommandAuthorizationGate();
        var lines = new[]
        {
            "# SITL Command Authorization",
            $"Scenario: {policy.ScenarioName}",
            $"Authorized: {policy.IsAuthorized}",
            $"Action: {action}",
            $"CanExecute: {gate.CanExecute(policy, action)}",
            $"Explanation: {gate.Explain(policy, action)}"
        };
        File.WriteAllText(outputPath, string.Join(Environment.NewLine, lines));
        return outputPath;
    }
}
