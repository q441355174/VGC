namespace VGC.Core;

public sealed record FirstRunPrompt(
    string Id,
    string Title,
    string Description,
    bool Completed);

public sealed class FirstRunPromptService
{
    private readonly Dictionary<string, FirstRunPrompt> _prompts;

    public FirstRunPromptService()
    {
        var defaults = CreateStandardPrompts();
        _prompts = defaults.ToDictionary(static p => p.Id, static p => p);
    }

    public IReadOnlyList<FirstRunPrompt> GetPendingPrompts()
    {
        return _prompts.Values
            .Where(static p => !p.Completed)
            .OrderBy(static p => p.Id)
            .ToArray();
    }

    public void MarkCompleted(string id)
    {
        if (_prompts.TryGetValue(id, out var prompt))
        {
            _prompts[id] = prompt with { Completed = true };
        }
    }

    public bool HasPendingPrompts => _prompts.Values.Any(static p => !p.Completed);

    private static IReadOnlyList<FirstRunPrompt> CreateStandardPrompts()
    {
        return
        [
            new FirstRunPrompt(
                "units",
                "Unit Preferences",
                "Select your preferred measurement units (metric or imperial) for altitude, speed, and distance display.",
                Completed: false),

            new FirstRunPrompt(
                "data-upload-consent",
                "Data Upload Consent",
                "Choose whether to allow anonymous usage data collection to help improve the application.",
                Completed: false),

            new FirstRunPrompt(
                "firmware-update-reminder",
                "Firmware Update Reminder",
                "Enable automatic checks for vehicle firmware updates on startup.",
                Completed: false)
        ];
    }
}
