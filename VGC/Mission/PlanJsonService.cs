using System.Text.Json;

namespace VGC.Mission;

public sealed class PlanJsonService
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public string Serialize(PlanDocument document)
    {
        return JsonSerializer.Serialize(document, Options);
    }

    public PlanDocument Deserialize(string json)
    {
        return JsonSerializer.Deserialize<PlanDocument>(json, Options)
            ?? throw new InvalidOperationException("Plan JSON did not produce a document.");
    }
}
