namespace VGC.Analyze;

public sealed record TelemetryDataPoint(
    double Timestamp,
    double Value);

public sealed record TelemetryDataSeries(
    string Name,
    string Units,
    IReadOnlyList<TelemetryDataPoint> DataPoints);

public sealed record TelemetryChartSnapshot(
    IReadOnlyList<TelemetryDataSeries> Series,
    double TimeRangeStart,
    double TimeRangeEnd,
    string StatusText);

public sealed class TelemetryChartRuntime
{
    private readonly Dictionary<string, TelemetrySeriesAccumulator> _series = new(StringComparer.Ordinal);
    private double _timeRangeStart;
    private double _timeRangeEnd = 60.0;

    public TelemetryChartSnapshot Snapshot => BuildSnapshot();

    public TelemetryChartSnapshot AddSeries(string name, string units)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BuildSnapshot();
        }

        _series.TryAdd(name, new TelemetrySeriesAccumulator(name, units));
        return BuildSnapshot();
    }

    public TelemetryChartSnapshot AddDataPoint(string seriesName, double timestamp, double value)
    {
        if (!_series.TryGetValue(seriesName, out var accumulator))
        {
            return BuildSnapshot();
        }

        accumulator.Add(timestamp, value);
        return BuildSnapshot();
    }

    public TelemetryChartSnapshot SetTimeRange(double start, double end)
    {
        _timeRangeStart = start;
        _timeRangeEnd = Math.Max(start, end);
        return BuildSnapshot();
    }

    public TelemetryChartSnapshot RemoveSeries(string name)
    {
        _series.Remove(name);
        return BuildSnapshot();
    }

    public TelemetryChartSnapshot Clear()
    {
        _series.Clear();
        return BuildSnapshot();
    }

    private TelemetryChartSnapshot BuildSnapshot()
    {
        var series = _series.Values
            .Select(static a => a.ToSeries())
            .ToArray();

        var totalPoints = series.Sum(static s => s.DataPoints.Count);
        return new TelemetryChartSnapshot(
            series,
            _timeRangeStart,
            _timeRangeEnd,
            $"{series.Length} series, {totalPoints} data points");
    }

    private sealed class TelemetrySeriesAccumulator
    {
        private readonly List<TelemetryDataPoint> _points = [];

        public TelemetrySeriesAccumulator(string name, string units)
        {
            Name = name;
            Units = units;
        }

        public string Name { get; }

        public string Units { get; }

        public void Add(double timestamp, double value)
        {
            _points.Add(new TelemetryDataPoint(timestamp, value));
        }

        public TelemetryDataSeries ToSeries()
        {
            return new TelemetryDataSeries(Name, Units, _points.ToArray());
        }
    }
}
