namespace VGC.Maps;

public sealed record MapCacheStats(
    int EntryCount,
    long TotalSizeBytes,
    int HitCount,
    int MissCount,
    double HitRatio,
    string Summary);

public sealed class MapCacheStatistics
{
    private int _hitCount;
    private int _missCount;
    private int _entryCount;
    private long _totalSizeBytes;

    public void RecordHit(int bytes)
    {
        _hitCount++;
        _entryCount = Math.Max(1, _entryCount);
        _totalSizeBytes += bytes;
    }

    public void RecordMiss()
    {
        _missCount++;
    }

    public MapCacheStats Snapshot()
    {
        var total = _hitCount + _missCount;
        return new MapCacheStats(
            _entryCount,
            _totalSizeBytes,
            _hitCount,
            _missCount,
            total == 0 ? 0 : Math.Round(100.0 * _hitCount / total, 1),
            $"{_entryCount} entries, {FormatBytes(_totalSizeBytes)}, {_hitCount}/{total} hits");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes / (1024.0 * 1024.0):F1} MB"
        };
    }
}
