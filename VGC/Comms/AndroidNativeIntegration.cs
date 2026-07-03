namespace VGC.Comms;

public enum AndroidNativeIntegrationStatus
{
    Complete,
    SharedModelOnly,
    Blocked
}

public sealed record AndroidNativeIntegrationItem(
    string Id,
    string Area,
    AndroidNativeIntegrationStatus Status,
    string SharedOwner,
    IReadOnlyList<string> RequiredNativeArtifacts,
    string RequiredDeviceEvidence);

public sealed class AndroidNativeIntegrationCatalog
{
    public IReadOnlyList<AndroidNativeIntegrationItem> BuildPhase328()
    {
        return
        [
            new("AND328-USB", "USB serial permission and adapter", AndroidNativeIntegrationStatus.SharedModelOnly, "AndroidUsbSerialRuntime", ["platform IAndroidUsbSerialPlatform implementation", "USB permission receiver"], "device USB permission/connect transcript"),
            new("AND328-STORAGE", "Scoped storage and media output", AndroidNativeIntegrationStatus.SharedModelOnly, "PayloadMediaOutputPlanner", ["Android storage path adapter", "MediaStore/scoped storage bridge"], "snapshot/recording output on device"),
            new("AND328-LIFECYCLE", "Lifecycle and background/foreground", AndroidNativeIntegrationStatus.SharedModelOnly, "AndroidMapLifecycleCoordinator", ["Android lifecycle event bridge"], "pause/resume/rotate/background transcript"),
            new("AND328-PERMISSIONS", "Location/network/Bluetooth permissions", AndroidNativeIntegrationStatus.Blocked, "PlatformPositioningPermissionProjector", ["native permission request bridge"], "permission grant/deny transcript"),
            new("AND328-PACKAGING", "Android package validation", AndroidNativeIntegrationStatus.Blocked, "ReleasePackagePlanner", ["signed APK/AAB", "manifest/package audit"], "install and first-launch transcript")
        ];
    }
}

public sealed class AndroidNativeIntegrationAudit
{
    public IReadOnlyList<string> OpenBlockers(IReadOnlyList<AndroidNativeIntegrationItem> items)
    {
        return items
            .Where(static item => item.Status != AndroidNativeIntegrationStatus.Complete)
            .Select(static item => $"{item.Id}: {item.Area} needs {string.Join(", ", item.RequiredNativeArtifacts)} and {item.RequiredDeviceEvidence}.")
            .ToArray();
    }
}
