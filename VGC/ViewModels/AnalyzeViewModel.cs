using System.Collections.ObjectModel;
using ReactiveUI;
using System.Reactive;
using VGC.Analyze;
using VGC.Comms;
using VGC.Mavlink;

namespace VGC.ViewModels;

public sealed record AnalyzeConsoleLine(string Text, bool IsCommand, string Timestamp);


public sealed record AnalyzeReplayWorkflowState(
    bool CanOpen,
    bool CanPlay,
    bool CanPause,
    bool CanSeek,
    bool CanStep,
    bool CanFilter,
    bool CanShowDetail,
    string FilterText,
    string StateText,
    string ProgressText,
    string SelectedDetailText);

public sealed record AnalyzeDiagnosticSummary(
    int ReplayPacketCount,
    int ReplayMessageTypeCount,
    int ReplayGapCount,
    int InspectorRowCount,
    string TopReplayMessage,
    string SummaryText);

public enum AnalyzePage
{
    Inspector,
    Console,
    Chart
}

public sealed class AnalyzeViewModel : ViewModelBase
{
    private const uint VfrHudMessageId = 74;
    private const double RadiansToDegrees = 180.0 / Math.PI;

    private readonly MavlinkInspector _inspector;
    private readonly ReplayPlaybackSession _replaySession;
    private readonly TelemetryChartRuntime _chartRuntime;
    private string _filterText = string.Empty;
    private string _consoleInput = string.Empty;
    private AnalyzePage _activePage = AnalyzePage.Inspector;
    private MavlinkInspectorRow? _selectedInspectorRow;
    private ReplayPacketIndexRow? _selectedReplayPacketRow;
    private double _chartSampleIndex;

    public AnalyzeViewModel(MavlinkProtocol protocol)
    {
        _inspector = new MavlinkInspector();
        _replaySession = new ReplayPlaybackSession();
        _chartRuntime = new TelemetryChartRuntime();
        InspectorRows = [];
        ReplayPacketRows = [];
        ReplayMessageRates = [];
        ReplayGaps = [];
        ConsoleLines = [];
        _inspector.Attach(protocol);
        AddDefaultChartSeries();
        protocol.PacketReceived += (_, packet) =>
        {
            RefreshRows();
            AddTelemetryChartData(packet);
        };
        PlayReplayCommand = ReactiveCommand.Create(PlayReplay);
        PauseReplayCommand = ReactiveCommand.Create(PauseReplay);
        StepReplayCommand = ReactiveCommand.Create(() => AdvanceReplayToNextPacket());
        SeekReplayStartCommand = ReactiveCommand.Create(() => SeekReplay(TimeSpan.Zero));
        SetReplayHalfSpeedCommand = ReactiveCommand.Create(() => SetReplaySpeed(0.5));
        SetReplayNormalSpeedCommand = ReactiveCommand.Create(() => SetReplaySpeed(1.0));
        SetReplayDoubleSpeedCommand = ReactiveCommand.Create(() => SetReplaySpeed(2.0));
        ShowInspectorTabCommand = ReactiveCommand.Create(() => SelectAnalyzePage(AnalyzePage.Inspector));
        ShowConsoleTabCommand = ReactiveCommand.Create(() => SelectAnalyzePage(AnalyzePage.Console));
        ShowChartTabCommand = ReactiveCommand.Create(() => SelectAnalyzePage(AnalyzePage.Chart));
        SendConsoleCommandAction = ReactiveCommand.Create(SendConsoleCommand);
        ClearConsoleCommand = ReactiveCommand.Create(ClearConsole);
        ClearChartCommand = ReactiveCommand.Create(ClearChart);
    }

    public string Title => "Analyze";

    public string Summary => string.IsNullOrWhiteSpace(FilterText)
        ? $"MAVLink Inspector rows {InspectorRows.Count}"
        : $"MAVLink Inspector rows {InspectorRows.Count} filtered by \"{FilterText}\"";

    public string FilterText
    {
        get => _filterText;
        set
        {
            var normalized = value ?? string.Empty;
            if (_filterText == normalized)
            {
                return;
            }

            _filterText = normalized;
            RefreshRows();
            this.RaisePropertyChanged();
            RaiseAnalyzeWorkflowChanged();
        }
    }

    public ObservableCollection<MavlinkInspectorRow> InspectorRows { get; }

    public ObservableCollection<ReplayPacketIndexRow> ReplayPacketRows { get; }

    public ObservableCollection<ReplayMessageRateRow> ReplayMessageRates { get; }

    public ObservableCollection<ReplayGapRow> ReplayGaps { get; }

    public MavlinkInspectorRow? SelectedInspectorRow
    {
        get => _selectedInspectorRow;
        set
        {
            if (Equals(_selectedInspectorRow, value))
            {
                return;
            }

            _selectedInspectorRow = value;
            this.RaisePropertyChanged();
            RaiseAnalyzeWorkflowChanged();
        }
    }

    public ReplayPacketIndexRow? SelectedReplayPacketRow
    {
        get => _selectedReplayPacketRow;
        set
        {
            if (Equals(_selectedReplayPacketRow, value))
            {
                return;
            }

            _selectedReplayPacketRow = value;
            this.RaisePropertyChanged();
            RaiseAnalyzeWorkflowChanged();
        }
    }

    public ReactiveCommand<Unit, Unit> PlayReplayCommand { get; }

    public ReactiveCommand<Unit, Unit> PauseReplayCommand { get; }

    public ReactiveCommand<Unit, LogReplayPacket?> StepReplayCommand { get; }

    public ReactiveCommand<Unit, Unit> SeekReplayStartCommand { get; }

    public ReactiveCommand<Unit, Unit> SetReplayHalfSpeedCommand { get; }

    public ReactiveCommand<Unit, Unit> SetReplayNormalSpeedCommand { get; }

    public ReactiveCommand<Unit, Unit> SetReplayDoubleSpeedCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowInspectorTabCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowConsoleTabCommand { get; }

    public ReactiveCommand<Unit, Unit> ShowChartTabCommand { get; }

    public ReactiveCommand<Unit, Unit> SendConsoleCommandAction { get; }

    public ReactiveCommand<Unit, Unit> ClearConsoleCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearChartCommand { get; }

    // Analyze tab selection
    public AnalyzePage ActivePage
    {
        get => _activePage;
        private set => this.RaiseAndSetIfChanged(ref _activePage, value);
    }

    public string SelectedAnalyzeTab => ActivePage switch
    {
        AnalyzePage.Inspector => "inspector",
        AnalyzePage.Console => "console",
        AnalyzePage.Chart => "chart",
        _ => "inspector"
    };

    public bool IsInspectorTab => ActivePage == AnalyzePage.Inspector;
    public bool IsConsoleTab => ActivePage == AnalyzePage.Console;
    public bool IsChartTab => ActivePage == AnalyzePage.Chart;

    // MAVLink Console
    public ObservableCollection<AnalyzeConsoleLine> ConsoleLines { get; }

    public string ConsoleInput
    {
        get => _consoleInput;
        set => this.RaiseAndSetIfChanged(ref _consoleInput, value);
    }

    public void SendConsoleCommand()
    {
        var command = ConsoleInput.Trim();
        if (string.IsNullOrEmpty(command)) return;

        ConsoleLines.Add(new AnalyzeConsoleLine($"> {command}", true, DateTime.Now.ToString("HH:mm:ss")));
        ConsoleInput = string.Empty;
        // Console response would come from SERIAL_CONTROL message handling
    }

    public void HandleConsoleResponse(string text)
    {
        ConsoleLines.Add(new AnalyzeConsoleLine(text, false, DateTime.Now.ToString("HH:mm:ss")));
    }

    public void ClearConsole()
    {
        ConsoleLines.Clear();
    }

    // Telemetry Chart
    public TelemetryChartSnapshot ChartSnapshot => _chartRuntime.Snapshot;

    public void AddChartSeries(string name, string units)
    {
        _chartRuntime.AddSeries(name, units);
        this.RaisePropertyChanged(nameof(ChartSnapshot));
    }

    public void AddChartDataPoint(string seriesName, double timestamp, double value)
    {
        _chartRuntime.AddDataPoint(seriesName, timestamp, value);
        this.RaisePropertyChanged(nameof(ChartSnapshot));
    }

    public void ClearChart()
    {
        _chartRuntime.Clear();
        AddDefaultChartSeries();
        this.RaisePropertyChanged(nameof(ChartSnapshot));
    }

    public void SelectAnalyzePage(AnalyzePage page)
    {
        ActivePage = page;
        this.RaisePropertyChanged(nameof(SelectedAnalyzeTab));
        this.RaisePropertyChanged(nameof(IsInspectorTab));
        this.RaisePropertyChanged(nameof(IsConsoleTab));
        this.RaisePropertyChanged(nameof(IsChartTab));
    }

    public void SelectAnalyzeTab(string tab)
    {
        SelectAnalyzePage(tab switch
        {
            "console" => AnalyzePage.Console,
            "chart" => AnalyzePage.Chart,
            _ => AnalyzePage.Inspector
        });
    }

    public ReplayPlaybackSnapshot Replay => _replaySession.Snapshot;

    public ReplayTimelineProjection ReplayTimeline => _replaySession.Timeline;

    public string ReplayStatusText => Replay.StatusText;

    public string ReplayProgressText => $"{Replay.PacketIndex}/{Replay.PacketCount} packets | {Replay.CurrentTime:mm\\:ss}/{Replay.Duration:mm\\:ss}";

    public string ReplayTimelineSummary => ReplayTimeline.PacketCount == 0
        ? "Timeline empty"
        : $"Timeline {ReplayTimeline.PacketCount} packets | {ReplayTimeline.Duration:mm\\:ss} | {ReplayTimeline.AverageRateHz:F2} pkt/s | gaps {ReplayTimeline.Gaps.Count}";

    public AnalyzeReplayWorkflowState ReplayWorkflowState => BuildReplayWorkflowState();

    public AnalyzeDiagnosticSummary DiagnosticSummary => BuildDiagnosticSummary();

    public async Task OpenReplayAsync(ILogReplaySource source, CancellationToken cancellationToken = default)
    {
        await _replaySession.OpenAsync(source, cancellationToken).ConfigureAwait(false);
        RefreshReplayTimeline();
        RaiseReplayChanged();
    }

    public void PlayReplay()
    {
        _replaySession.Play();
        RaiseReplayChanged();
    }

    public void PauseReplay()
    {
        _replaySession.Pause();
        RaiseReplayChanged();
    }

    public void SeekReplay(TimeSpan position)
    {
        _replaySession.Seek(position);
        RaiseReplayChanged();
    }

    public void SetReplaySpeed(double speed)
    {
        _replaySession.SetSpeed(speed);
        RaiseReplayChanged();
    }

    public LogReplayPacket? AdvanceReplayToNextPacket()
    {
        var packet = _replaySession.AdvanceToNextPacket();
        RaiseReplayChanged();
        return packet;
    }

    private void RefreshRows()
    {
        InspectorRows.Clear();
        foreach (var row in _inspector.GetRows(CreateFilter()))
        {
            InspectorRows.Add(row);
        }

        this.RaisePropertyChanged(nameof(Summary));
        RaiseAnalyzeWorkflowChanged();
    }

    private void AddDefaultChartSeries()
    {
        _chartRuntime.AddSeries("Roll", "deg");
        _chartRuntime.AddSeries("Pitch", "deg");
        _chartRuntime.AddSeries("Heading", "deg");
        _chartRuntime.AddSeries("Altitude", "m");
        _chartRuntime.AddSeries("Ground speed", "m/s");
    }

    private void AddTelemetryChartData(MavlinkPacket packet)
    {
        var timestamp = _chartSampleIndex++;
        switch (packet.MessageId)
        {
            case MavlinkMessageIds.Attitude when packet.Payload.Length >= 12:
                _chartRuntime.AddDataPoint("Roll", timestamp, BitConverter.ToSingle(packet.Payload, 0) * RadiansToDegrees);
                _chartRuntime.AddDataPoint("Pitch", timestamp, BitConverter.ToSingle(packet.Payload, 4) * RadiansToDegrees);
                _chartRuntime.AddDataPoint("Heading", timestamp, BitConverter.ToSingle(packet.Payload, 8) * RadiansToDegrees);
                this.RaisePropertyChanged(nameof(ChartSnapshot));
                break;
            case MavlinkMessageIds.GlobalPositionInt when packet.Payload.Length >= 20:
                _chartRuntime.AddDataPoint("Altitude", timestamp, BitConverter.ToInt32(packet.Payload, 16) / 1000.0);
                this.RaisePropertyChanged(nameof(ChartSnapshot));
                break;
            case VfrHudMessageId when packet.Payload.Length >= 8:
                _chartRuntime.AddDataPoint("Ground speed", timestamp, BitConverter.ToSingle(packet.Payload, 4));
                this.RaisePropertyChanged(nameof(ChartSnapshot));
                break;
        }
    }

    private MavlinkInspectorFilter CreateFilter()
    {
        var text = FilterText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new MavlinkInspectorFilter();
        }

        if (text.StartsWith("msg:", StringComparison.OrdinalIgnoreCase) &&
            uint.TryParse(text[4..], out var messageId))
        {
            return new MavlinkInspectorFilter(MessageId: messageId);
        }

        if (text.StartsWith("sys:", StringComparison.OrdinalIgnoreCase) &&
            byte.TryParse(text[4..], out var systemId))
        {
            return new MavlinkInspectorFilter(SystemId: systemId);
        }

        if (text.StartsWith("comp:", StringComparison.OrdinalIgnoreCase) &&
            byte.TryParse(text[5..], out var componentId))
        {
            return new MavlinkInspectorFilter(ComponentId: componentId);
        }

        if (text.StartsWith("severity:", StringComparison.OrdinalIgnoreCase))
        {
            return new MavlinkInspectorFilter(Severity: text[9..]);
        }

        if (text.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
        {
            return new MavlinkInspectorFilter(MessageName: text[5..]);
        }

        return new MavlinkInspectorFilter(Text: text);
    }

    private void RaiseReplayChanged()
    {
        this.RaisePropertyChanged(nameof(Replay));
        this.RaisePropertyChanged(nameof(ReplayTimeline));
        this.RaisePropertyChanged(nameof(ReplayStatusText));
        this.RaisePropertyChanged(nameof(ReplayProgressText));
        this.RaisePropertyChanged(nameof(ReplayTimelineSummary));
        RaiseAnalyzeWorkflowChanged();
    }

    private void RefreshReplayTimeline()
    {
        ReplayPacketRows.Clear();
        foreach (var row in ReplayTimeline.Packets.Take(200))
        {
            ReplayPacketRows.Add(row);
        }

        ReplayMessageRates.Clear();
        foreach (var row in ReplayTimeline.MessageRates)
        {
            ReplayMessageRates.Add(row);
        }

        ReplayGaps.Clear();
        foreach (var row in ReplayTimeline.Gaps)
        {
            ReplayGaps.Add(row);
        }

        SelectedReplayPacketRow = ReplayPacketRows.FirstOrDefault();
    }

    private void RaiseAnalyzeWorkflowChanged()
    {
        this.RaisePropertyChanged(nameof(ReplayWorkflowState));
        this.RaisePropertyChanged(nameof(DiagnosticSummary));
    }

    private AnalyzeReplayWorkflowState BuildReplayWorkflowState()
    {
        var selectedDetail = SelectedReplayPacketRow is { } replayRow
            ? $"{replayRow.MessageName}: {replayRow.FieldSummary}"
            : SelectedInspectorRow is { } inspectorRow
                ? $"{inspectorRow.MessageName}: {inspectorRow.FieldSummary}"
                : "No message selected";

        return new AnalyzeReplayWorkflowState(
            CanOpen: true,
            CanPlay: Replay.CanPlay,
            CanPause: Replay.CanPause,
            CanSeek: Replay.CanSeek,
            CanStep: Replay.State == ReplayPlaybackState.Playing,
            CanFilter: true,
            CanShowDetail: SelectedReplayPacketRow is not null || SelectedInspectorRow is not null,
            FilterText: FilterText,
            StateText: ReplayStatusText,
            ProgressText: ReplayProgressText,
            SelectedDetailText: selectedDetail);
    }

    private AnalyzeDiagnosticSummary BuildDiagnosticSummary()
    {
        var topMessage = ReplayMessageRates
            .OrderByDescending(static row => row.Count)
            .ThenBy(static row => row.MessageId)
            .FirstOrDefault();
        var top = topMessage is null
            ? "No replay messages"
            : $"{topMessage.MessageName} x{topMessage.Count}";
        var summary = ReplayPacketRows.Count == 0
            ? $"Inspector rows {InspectorRows.Count} | No replay loaded"
            : $"Replay {ReplayPacketRows.Count} packets | Types {ReplayMessageRates.Count} | Gaps {ReplayGaps.Count} | Top {top} | Inspector rows {InspectorRows.Count}";

        return new AnalyzeDiagnosticSummary(
            ReplayPacketRows.Count,
            ReplayMessageRates.Count,
            ReplayGaps.Count,
            InspectorRows.Count,
            top,
            summary);
    }
}
