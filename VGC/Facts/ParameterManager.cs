using System.Collections.ObjectModel;

namespace VGC.Facts;

public sealed class ParameterManager
{
    private readonly Dictionary<(int ComponentId, string Name), Fact> _facts = new();
    private readonly ObservableCollection<Fact> _parameters = new();
    private readonly Dictionary<int, ushort> _expectedParameterCountsByComponent = new();
    private readonly Dictionary<int, HashSet<ushort>> _receivedParameterIndexesByComponent = new();
    private readonly HashSet<(int ComponentId, string Name)> _pendingWrites = new();
    private readonly Dictionary<(int ComponentId, string Name), ParameterWriteState> _writeStates = new();
    private int _anonymousPendingWriteCount;

    public ParameterManager()
    {
        Parameters = new ReadOnlyObservableCollection<Fact>(_parameters);
    }

    public event EventHandler<Fact>? FactAdded;

    public event EventHandler<Fact>? FactUpdated;

    public event EventHandler? RequestStateChanged;

    public event EventHandler? WriteStateChanged;

    public event EventHandler? DownloadStateChanged;

    public ReadOnlyObservableCollection<Fact> Parameters { get; }

    public int Count => _facts.Count;

    public int ExpectedParameterCount => _expectedParameterCountsByComponent.Values.Sum(static count => count);

    public int ReceivedParameterCount => _receivedParameterIndexesByComponent.Values.Sum(static indexes => indexes.Count);

    public double LoadProgress { get; private set; }

    public bool ParametersReady { get; private set; }

    public bool MissingParameters { get; private set; }

    public bool IsParameterRequestActive { get; private set; }

    public DateTimeOffset? LastParameterRequestStartedAt { get; private set; }

    public DateTimeOffset? LastParameterRequestCompletedAt { get; private set; }

    public int PendingWriteCount => _anonymousPendingWriteCount + _pendingWrites.Count;

    public bool HasPendingWrites => PendingWriteCount > 0;

    public DateTimeOffset? LastParameterWriteStartedAt { get; private set; }

    public DateTimeOffset? LastParameterWriteCompletedAt { get; private set; }

    public void BeginParameterRequest()
    {
        IsParameterRequestActive = true;
        ParametersReady = false;
        MissingParameters = false;
        LoadProgress = 0;
        _expectedParameterCountsByComponent.Clear();
        _receivedParameterIndexesByComponent.Clear();
        LastParameterRequestStartedAt = DateTimeOffset.Now;
        RequestStateChanged?.Invoke(this, EventArgs.Empty);
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteParameterRequest()
    {
        IsParameterRequestActive = false;
        ParametersReady = ExpectedParameterCount > 0 && ReceivedParameterCount >= ExpectedParameterCount;
        MissingParameters = ExpectedParameterCount > 0 && ReceivedParameterCount < ExpectedParameterCount;
        LoadProgress = ExpectedParameterCount == 0 ? LoadProgress : Math.Min(1.0, LoadProgress);
        LastParameterRequestCompletedAt = DateTimeOffset.Now;
        RequestStateChanged?.Invoke(this, EventArgs.Empty);
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BeginParameterWrite()
    {
        _anonymousPendingWriteCount++;
        LastParameterWriteStartedAt = DateTimeOffset.Now;
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void BeginParameterWrite(int componentId, string name)
    {
        var key = (componentId, name);
        if (!_pendingWrites.Add(key))
        {
            return;
        }

        _writeStates[key] = new ParameterWriteState(ParameterWriteStatus.Pending, RetryCount: 0);
        LastParameterWriteStartedAt = DateTimeOffset.Now;
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RecordParameterWriteRetry(int componentId, string name)
    {
        var key = (componentId, name);
        if (!_pendingWrites.Contains(key))
        {
            return false;
        }

        var current = GetParameterWriteState(componentId, name);
        _writeStates[key] = current with
        {
            Status = ParameterWriteStatus.Pending,
            RetryCount = checked(current.RetryCount + 1),
            LastError = null
        };
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void CompleteParameterWrite()
    {
        if (_anonymousPendingWriteCount > 0)
        {
            _anonymousPendingWriteCount--;
        }

        LastParameterWriteCompletedAt = DateTimeOffset.Now;
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CompleteParameterWrite(int componentId, string name)
    {
        var key = (componentId, name);
        if (!_pendingWrites.Remove(key))
        {
            return false;
        }

        _writeStates[key] = GetParameterWriteState(componentId, name) with
        {
            Status = ParameterWriteStatus.Succeeded,
            LastError = null
        };
        LastParameterWriteCompletedAt = DateTimeOffset.Now;
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool FailParameterWrite(int componentId, string name, string error)
    {
        var key = (componentId, name);
        var hadState = _pendingWrites.Remove(key) || _writeStates.ContainsKey(key);
        _writeStates[key] = GetParameterWriteState(componentId, name) with
        {
            Status = ParameterWriteStatus.Failed,
            LastError = error
        };
        LastParameterWriteCompletedAt = DateTimeOffset.Now;
        WriteStateChanged?.Invoke(this, EventArgs.Empty);
        return hadState;
    }

    public bool IsParameterWritePending(int componentId, string name)
    {
        return _pendingWrites.Contains((componentId, name));
    }

    public IReadOnlyList<string> GetPendingWriteNames(int componentId)
    {
        return _pendingWrites
            .Where(write => write.ComponentId == componentId)
            .Select(static write => write.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public ParameterWriteState GetParameterWriteState(int componentId, string name)
    {
        return _writeStates.TryGetValue((componentId, name), out var state)
            ? state
            : ParameterWriteState.None;
    }

    public Fact GetOrCreateParameter(MavlinkParamValue value)
    {
        var key = (value.ComponentId, value.Name);
        if (_facts.TryGetValue(key, out var fact))
        {
            fact.SetRawValue(value.Value);
            FactUpdated?.Invoke(this, fact);
            return fact;
        }

        fact = new Fact(value.ComponentId, value.Name, new FactMetaData(value.Name, FactValueType.Float), value.Value);
        _facts.Add(key, fact);
        _parameters.Add(fact);
        FactAdded?.Invoke(this, fact);
        return fact;
    }

    public bool TryGetParameter(int componentId, string name, out Fact? fact)
    {
        return _facts.TryGetValue((componentId, name), out fact);
    }

    public bool ApplyParamValuePayload(int componentId, byte[] payload)
    {
        if (!TryParseParamValue(componentId, payload, out var value))
        {
            return false;
        }

        GetOrCreateParameter(value);
        UpdateDownloadState(value);
        CompleteParameterWrite(value.ComponentId, value.Name);
        return true;
    }

    public IReadOnlyList<ushort> GetMissingParameterIndexes(int componentId)
    {
        if (!_expectedParameterCountsByComponent.TryGetValue(componentId, out var expectedCount) || expectedCount == 0)
        {
            return [];
        }

        _receivedParameterIndexesByComponent.TryGetValue(componentId, out var received);
        var missing = new List<ushort>();
        for (ushort index = 0; index < expectedCount; index++)
        {
            if (received is null || !received.Contains(index))
            {
                missing.Add(index);
            }
        }

        return missing;
    }

    private void UpdateDownloadState(MavlinkParamValue value)
    {
        if (value.Count == 0 || value.Index == ushort.MaxValue)
        {
            return;
        }

        _expectedParameterCountsByComponent[value.ComponentId] = value.Count;
        if (!_receivedParameterIndexesByComponent.TryGetValue(value.ComponentId, out var receivedIndexes))
        {
            receivedIndexes = new HashSet<ushort>();
            _receivedParameterIndexesByComponent.Add(value.ComponentId, receivedIndexes);
        }

        receivedIndexes.Add(value.Index);
        var expected = ExpectedParameterCount;
        var received = ReceivedParameterCount;
        LoadProgress = expected == 0 ? 0 : Math.Min(1.0, received / (double)expected);
        MissingParameters = expected > 0 && received < expected;
        ParametersReady = expected > 0 && received >= expected;
        if (ParametersReady)
        {
            MissingParameters = false;
            IsParameterRequestActive = false;
            LastParameterRequestCompletedAt ??= DateTimeOffset.Now;
        }

        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool TryParseParamValue(int componentId, byte[] payload, out MavlinkParamValue value)
    {
        value = default!;
        if (payload.Length < 25)
        {
            return false;
        }

        var paramValue = BitConverter.ToSingle(payload, 0);
        var paramCount = BitConverter.ToUInt16(payload, 4);
        var paramIndex = BitConverter.ToUInt16(payload, 6);
        var nameBytes = payload.AsSpan(8, 16);
        var terminatorIndex = nameBytes.IndexOf((byte)0);
        var name = System.Text.Encoding.ASCII.GetString(terminatorIndex >= 0 ? nameBytes[..terminatorIndex] : nameBytes);
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        value = new MavlinkParamValue(componentId, name, paramValue, paramCount, paramIndex, payload[24]);
        return true;
    }
}
