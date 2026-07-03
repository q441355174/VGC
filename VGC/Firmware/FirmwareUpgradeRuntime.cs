namespace VGC.Firmware;

public enum FirmwareUpgradeState
{
    Idle,
    Detecting,
    Downloading,
    Flashing,
    Verifying,
    Complete,
    Failed
}

public enum FirmwareReleaseType
{
    Stable,
    Beta,
    Dev,
    Custom
}

public sealed record FirmwareBoardInfo(
    string BoardId,
    string BootloaderVersion,
    string BoardType,
    bool IsInBootloader);

public sealed record FirmwareVersionInfo(
    string Version,
    FirmwareReleaseType ReleaseType,
    string Url,
    string ReleaseNotes,
    long FileSize);

public sealed record FirmwareUpgradeProgress(
    FirmwareUpgradeState State,
    double ProgressPercent,
    string StatusText,
    FirmwareBoardInfo? BoardInfo,
    FirmwareVersionInfo? SelectedVersion);

public interface IFirmwareUpgradePlatform
{
    Task<FirmwareBoardInfo?> DetectBoardAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FirmwareVersionInfo>> GetAvailableVersionsAsync(
        string boardType,
        CancellationToken cancellationToken = default);

    Task<string> DownloadFirmwareAsync(
        FirmwareVersionInfo version,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);

    Task FlashFirmwareAsync(
        string filePath,
        IProgress<double> progress,
        CancellationToken cancellationToken = default);
}

public sealed class FirmwareUpgradeRuntime
{
    private readonly IFirmwareUpgradePlatform _platform;
    private readonly object _lock = new();

    private FirmwareUpgradeState _state = FirmwareUpgradeState.Idle;
    private double _progressPercent;
    private string _statusText = "Idle";
    private FirmwareBoardInfo? _boardInfo;
    private FirmwareVersionInfo? _selectedVersion;
    private CancellationTokenSource? _cts;

    public FirmwareUpgradeRuntime(IFirmwareUpgradePlatform platform)
    {
        _platform = platform;
    }

    public FirmwareUpgradeProgress Snapshot
    {
        get
        {
            lock (_lock)
            {
                return new FirmwareUpgradeProgress(
                    _state,
                    Math.Clamp(_progressPercent, 0, 100),
                    _statusText,
                    _boardInfo,
                    _selectedVersion);
            }
        }
    }

    public async Task DetectBoardAsync(CancellationToken cancellationToken = default)
    {
        SetState(FirmwareUpgradeState.Detecting, 0, "Detecting board...");

        try
        {
            var board = await _platform.DetectBoardAsync(cancellationToken).ConfigureAwait(false);
            lock (_lock)
            {
                _boardInfo = board;
                _statusText = board is not null
                    ? $"Detected: {board.BoardType} (bootloader {board.BootloaderVersion})"
                    : "No board detected";
                _state = FirmwareUpgradeState.Idle;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetState(FirmwareUpgradeState.Failed, 0, $"Detection failed: {ex.Message}");
        }
    }

    public void SelectVersion(FirmwareVersionInfo version)
    {
        lock (_lock)
        {
            _selectedVersion = version;
            _statusText = $"Selected: {version.Version} ({version.ReleaseType})";
        }
    }

    public async Task StartUpgradeAsync(CancellationToken cancellationToken = default)
    {
        FirmwareVersionInfo version;
        lock (_lock)
        {
            if (_selectedVersion is null)
            {
                throw new InvalidOperationException("No firmware version selected.");
            }

            if (_boardInfo is null)
            {
                throw new InvalidOperationException("No board detected.");
            }

            version = _selectedVersion;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        var linked = _cts.Token;

        try
        {
            SetState(FirmwareUpgradeState.Downloading, 0, "Downloading firmware...");
            var downloadProgress = new Progress<double>(percent =>
            {
                lock (_lock)
                {
                    _progressPercent = percent;
                }
            });

            var filePath = await _platform.DownloadFirmwareAsync(version, downloadProgress, linked).ConfigureAwait(false);

            SetState(FirmwareUpgradeState.Flashing, 0, "Flashing firmware...");
            var flashProgress = new Progress<double>(percent =>
            {
                lock (_lock)
                {
                    _progressPercent = percent;
                }
            });

            await _platform.FlashFirmwareAsync(filePath, flashProgress, linked).ConfigureAwait(false);

            SetState(FirmwareUpgradeState.Verifying, 90, "Verifying firmware...");
            SetState(FirmwareUpgradeState.Complete, 100, "Firmware upgrade complete.");
        }
        catch (OperationCanceledException)
        {
            SetState(FirmwareUpgradeState.Failed, 0, "Firmware upgrade cancelled.");
        }
        catch (Exception ex)
        {
            SetState(FirmwareUpgradeState.Failed, 0, $"Firmware upgrade failed: {ex.Message}");
        }
    }

    public void CancelUpgrade()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _statusText = "Cancelling...";
        }
    }

    private void SetState(FirmwareUpgradeState state, double progress, string statusText)
    {
        lock (_lock)
        {
            _state = state;
            _progressPercent = progress;
            _statusText = statusText;
        }
    }
}
