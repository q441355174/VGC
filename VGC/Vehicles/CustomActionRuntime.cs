namespace VGC.Vehicles;

public sealed record CustomAction(
    string Id,
    string Label,
    ushort MavlinkCommand,
    float[] Params,
    bool RequiresConfirmation)
{
    public CustomAction(string id, string label, ushort mavlinkCommand, bool requiresConfirmation = false)
        : this(id, label, mavlinkCommand, new float[7], requiresConfirmation)
    {
    }
}

public sealed record CustomActionGroup(
    string Name,
    IReadOnlyList<CustomAction> Actions);

public sealed record CustomActionExecuteResult(
    ushort MavlinkCommand,
    float[] Params,
    bool Confirmed);

public sealed record CustomActionSnapshot(
    IReadOnlyList<CustomActionGroup> Groups,
    int TotalActionCount);

public sealed class CustomActionRuntime
{
    private readonly Dictionary<string, CustomAction> _actions = [];
    private readonly Dictionary<string, List<string>> _groups = [];

    public void RegisterAction(string groupName, CustomAction action)
    {
        _actions[action.Id] = action;

        if (!_groups.TryGetValue(groupName, out var actionIds))
        {
            actionIds = [];
            _groups[groupName] = actionIds;
        }

        if (!actionIds.Contains(action.Id))
        {
            actionIds.Add(action.Id);
        }
    }

    public bool RemoveAction(string actionId)
    {
        if (!_actions.Remove(actionId))
        {
            return false;
        }

        foreach (var group in _groups.Values)
        {
            group.Remove(actionId);
        }

        // Remove empty groups
        var emptyGroups = _groups.Where(static g => g.Value.Count == 0).Select(static g => g.Key).ToArray();
        foreach (var key in emptyGroups)
        {
            _groups.Remove(key);
        }

        return true;
    }

    public CustomActionExecuteResult? ExecuteAction(string actionId, bool confirmed = false)
    {
        if (!_actions.TryGetValue(actionId, out var action))
        {
            return null;
        }

        if (action.RequiresConfirmation && !confirmed)
        {
            return new CustomActionExecuteResult(action.MavlinkCommand, action.Params, Confirmed: false);
        }

        return new CustomActionExecuteResult(action.MavlinkCommand, action.Params, Confirmed: true);
    }

    public IReadOnlyList<CustomActionGroup> GetGroups()
    {
        return _groups
            .Select(kvp => new CustomActionGroup(
                kvp.Key,
                kvp.Value
                    .Where(id => _actions.ContainsKey(id))
                    .Select(id => _actions[id])
                    .ToArray()))
            .OrderBy(static g => g.Name)
            .ToArray();
    }

    public CustomActionSnapshot Snapshot => new(GetGroups(), _actions.Count);
}
