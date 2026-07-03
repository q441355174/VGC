namespace VGC.Comms;

public sealed record LinkRuntimeEvidenceItem(
    string Name,
    string Target,
    string Verification,
    bool IsComplete);

public static class LinkRuntimeEvidenceCatalog
{
    public static IReadOnlyList<LinkRuntimeEvidenceItem> CreateV143Checklist()
    {
        return
        [
            new LinkRuntimeEvidenceItem(
                "Desktop link workflow",
                "Shared LinkManager configuration, diagnostics, auto-connect, forwarding, and recovery can be exercised by VGC.Tests.",
                "dotnet run --project VGC.Tests",
                true),
            new LinkRuntimeEvidenceItem(
                "Android USB serial boundary",
                "Shared runtime models discovery, permission, connect, disconnect, and failure states without platform API calls.",
                "AndroidUsbSerialRuntime tests plus deferred device checklist for platform adapter wiring.",
                true),
            new LinkRuntimeEvidenceItem(
                "Platform build policy",
                "Desktop/Android builds are required when AXAML or platform adapter code changes; v1.43 shared-core changes only require VGC build.",
                "dotnet build VGC",
                true)
        ];
    }
}
