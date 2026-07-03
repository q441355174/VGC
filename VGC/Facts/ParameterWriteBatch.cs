namespace VGC.Facts;

public sealed record ParameterWriteBatchResult(
    int Accepted,
    int Rejected,
    IReadOnlyList<string> Errors);

public sealed class ParameterWriteBatch
{
    private readonly ParameterEditService _editService = new();
    private readonly List<ParameterEditCommitResult> _results = [];

    public IReadOnlyList<ParameterEditCommitResult> Results => _results;

    public ParameterWriteBatchResult Commit(
        ParameterManager manager,
        IReadOnlyList<(int ComponentId, string Name, string Value)> edits,
        IParameterMetadataCatalog? metadataCatalog = null)
    {
        _results.Clear();
        var accepted = 0;
        var rejected = 0;
        var errors = new List<string>();

        foreach (var (componentId, name, value) in edits)
        {
            var result = _editService.Commit(manager, componentId, name, value, metadataCatalog);
            _results.Add(result);
            if (result.Accepted)
            {
                accepted++;
            }
            else
            {
                rejected++;
                errors.Add(result.StatusText);
            }
        }

        return new ParameterWriteBatchResult(accepted, rejected, errors);
    }
}
