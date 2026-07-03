namespace VGC.Facts;

using System.Text.Json;

public sealed record ParameterCacheEntry(
    int ComponentId,
    string Name,
    string Value,
    FactValueType ValueType,
    DateTimeOffset UpdatedAt);

public sealed record ParameterCacheSnapshot(
    string VehicleIdentity,
    string FirmwareId,
    string VehicleType,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ParameterCacheEntry> Parameters)
{
    public bool IsStale(DateTimeOffset now, TimeSpan maxAge)
    {
        return now - UpdatedAt > maxAge;
    }
}

public interface IParameterCacheStore
{
    Task<ParameterCacheSnapshot?> LoadAsync(
        string vehicleIdentity,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ParameterCacheSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryParameterCacheStore : IParameterCacheStore
{
    private readonly Dictionary<string, ParameterCacheSnapshot> _snapshots = new(StringComparer.Ordinal);

    public Task<ParameterCacheSnapshot?> LoadAsync(
        string vehicleIdentity,
        CancellationToken cancellationToken = default)
    {
        _snapshots.TryGetValue(vehicleIdentity, out var snapshot);
        return Task.FromResult(snapshot);
    }

    public Task SaveAsync(
        ParameterCacheSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _snapshots[snapshot.VehicleIdentity] = snapshot;
        return Task.CompletedTask;
    }
}

public sealed class JsonParameterCacheStore : IParameterCacheStore
{
    private readonly string _directory;

    public JsonParameterCacheStore(string directory)
    {
        _directory = directory;
    }

    public async Task<ParameterCacheSnapshot?> LoadAsync(
        string vehicleIdentity,
        CancellationToken cancellationToken = default)
    {
        var path = GetSnapshotPath(vehicleIdentity);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ParameterCacheSnapshot>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveAsync(
        ParameterCacheSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_directory);
        var path = GetSnapshotPath(snapshot.VehicleIdentity);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private string GetSnapshotPath(string vehicleIdentity)
    {
        var safeName = string.Join("_", vehicleIdentity.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_directory, $"{safeName}.parameters.json");
    }
}

public sealed class ParameterCacheRuntime
{
    public ParameterCacheSnapshot CreateSnapshot(
        ParameterManager manager,
        string vehicleIdentity,
        string firmwareId,
        string vehicleType,
        DateTimeOffset now)
    {
        var entries = manager.Parameters
            .Select(fact => new ParameterCacheEntry(
                fact.ComponentId,
                fact.Name,
                Convert.ToString(fact.RawValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                fact.MetaData.ValueType,
                now))
            .OrderBy(static item => item.ComponentId)
            .ThenBy(static item => item.Name, StringComparer.Ordinal)
            .ToArray();

        return new ParameterCacheSnapshot(vehicleIdentity, firmwareId, vehicleType, now, entries);
    }

    public Task SaveAsync(
        IParameterCacheStore store,
        ParameterCacheSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        return store.SaveAsync(snapshot, cancellationToken);
    }

    public Task<ParameterCacheSnapshot?> LoadAsync(
        IParameterCacheStore store,
        string vehicleIdentity,
        CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(vehicleIdentity, cancellationToken);
    }
}
