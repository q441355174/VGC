namespace VGC.Core.Settings;

public sealed record SettingsLifecycleEvidenceItem(
    string Name,
    string Target,
    string Verification,
    bool IsComplete);

public static class SettingsLifecycleEvidenceCatalog
{
    public static IReadOnlyList<SettingsLifecycleEvidenceItem> CreateV144Checklist()
    {
        return
        [
            new("Settings roundtrip", "SettingsManager defaults can load from and save to IAppSettingsStore.", "VGC.Tests", true),
            new("Settings UI contract", "Shared SettingsViewModel exposes grouped facts without AXAML coupling.", "VGC.Tests", true),
            new("Close coordinator", "Registered close guards block unsaved plan, pending write, and active link scenarios.", "VGC.Tests", true),
            new("File logging", "FileLogService writes categories, rotates files, and projects viewer rows.", "VGC.Tests", true),
            new("Localization boundary", "Localization keys use VGC-owned default text and do not copy QGC translations.", "VGC.Tests", true)
        ];
    }
}
