namespace VGC.Payload;

public enum PayloadMediaKind
{
    Snapshot,
    Recording
}

public enum PayloadStoragePlatform
{
    Desktop,
    Android
}

public enum PayloadStorageLocationKind
{
    AppData,
    UserSelectedFolder,
    MediaStore,
    Temporary
}

public sealed record PayloadStorageRequest(
    PayloadMediaKind Kind,
    string SuggestedName,
    string Extension,
    long? ExpectedBytes = null);

public sealed record PayloadStoragePolicy(
    PayloadStoragePlatform Platform,
    PayloadStorageLocationKind LocationKind,
    string RootPath,
    bool RequiresUserConsent,
    bool AllowsOverwrite,
    bool RequiresScopedStorage);

public sealed record PayloadStoragePlan(
    PayloadStorageRequest Request,
    PayloadStoragePolicy Policy,
    string RelativeFileName,
    string DisplayPath,
    IReadOnlyList<string> Warnings)
{
    public bool RequiresPrompt => Policy.RequiresUserConsent || Warnings.Count > 0;
}

public sealed class PayloadStoragePlanner
{
    public PayloadStoragePlan Plan(PayloadStorageRequest request, PayloadStoragePolicy policy)
    {
        var warnings = new List<string>();
        var fileName = BuildFileName(request);

        if (policy.Platform == PayloadStoragePlatform.Android && !policy.RequiresScopedStorage)
        {
            warnings.Add("Android payload media should use scoped storage or app-owned storage.");
        }

        if (request.Kind == PayloadMediaKind.Recording && policy.LocationKind == PayloadStorageLocationKind.Temporary)
        {
            warnings.Add("Recordings should not default to temporary storage.");
        }

        if (request.ExpectedBytes is <= 0)
        {
            warnings.Add("Expected media size must be positive when provided.");
        }

        var root = string.IsNullOrWhiteSpace(policy.RootPath)
            ? policy.LocationKind.ToString()
            : policy.RootPath.TrimEnd('\\', '/');
        var displayPath = $"{root}/{fileName}";

        return new PayloadStoragePlan(request, policy, fileName, displayPath, warnings);
    }

    private static string BuildFileName(PayloadStorageRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.SuggestedName)
            ? request.Kind.ToString().ToLowerInvariant()
            : request.SuggestedName.Trim();
        var safeName = new string(name.Select(static c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        var extension = request.Extension.Trim().TrimStart('.');

        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = request.Kind == PayloadMediaKind.Snapshot ? "jpg" : "mp4";
        }

        return $"{safeName}.{extension.ToLowerInvariant()}";
    }
}
