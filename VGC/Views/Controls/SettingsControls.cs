using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Collections.ObjectModel;
using System.Globalization;
using VGC.Positioning;

namespace VGC.Views.Controls;

// ────────────────────────────────────────────────────────────────
// Data models
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Stored offline map entry used by <see cref="OfflineMapPanel"/>.
/// </summary>
public sealed record StoredMap(string Name, long SizeBytes, int TileCount, DateTime CreatedAt);

/// <summary>
/// Connection type enumeration for <see cref="ConnectionSettingsPanel"/>.
/// </summary>
public enum ConnectionType
{
    Serial,
    Tcp,
    Udp,
    Bluetooth
}

/// <summary>
/// Unit system setting.
/// </summary>
public enum UnitSystem
{
    Metric,
    Imperial
}

/// <summary>
/// Application theme setting.
/// </summary>
public enum AppTheme
{
    Dark,
    Light
}

// ────────────────────────────────────────────────────────────────
// 6. GeneralSettingsPanel
//    QGC equivalent: SettingsPage (General)
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Centered settings form (max width 500 px) with units, language, and theme selectors.
/// Matches QGC's general settings page layout — a single centered column of
/// labelled combo-box rows.
/// </summary>
public class GeneralSettingsPanel : TemplatedControl
{
    public const double MaxFormWidth = 500;

    public static readonly StyledProperty<UnitSystem> UnitsProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, UnitSystem>(nameof(Units), UnitSystem.Metric);

    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, string>(nameof(Language), "en");

    public static readonly StyledProperty<AppTheme> AppThemeSettingProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, AppTheme>(nameof(AppThemeSetting), AppTheme.Dark);

    public static readonly StyledProperty<double> DefaultAltitudeProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, double>(nameof(DefaultAltitude), 50);

    public static readonly StyledProperty<bool> AutoConnectProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, bool>(nameof(AutoConnect), true);

    public static readonly StyledProperty<bool> VirtualJoystickProperty =
        AvaloniaProperty.Register<GeneralSettingsPanel, bool>(nameof(VirtualJoystick), false);

    public UnitSystem Units { get => GetValue(UnitsProperty); set => SetValue(UnitsProperty, value); }
    public string Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public AppTheme AppThemeSetting { get => GetValue(AppThemeSettingProperty); set => SetValue(AppThemeSettingProperty, value); }
    public double DefaultAltitude { get => GetValue(DefaultAltitudeProperty); set => SetValue(DefaultAltitudeProperty, value); }
    public bool AutoConnect { get => GetValue(AutoConnectProperty); set => SetValue(AutoConnectProperty, value); }
    public bool VirtualJoystick { get => GetValue(VirtualJoystickProperty); set => SetValue(VirtualJoystickProperty, value); }

    /// <summary>
    /// Available language codes for the language selector.
    /// </summary>
    public ObservableCollection<string> AvailableLanguages { get; } = ["en", "zh", "ko", "de", "fr", "es", "ja"];

    public event EventHandler? SettingsChanged;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UnitsProperty ||
            change.Property == LanguageProperty ||
            change.Property == AppThemeSettingProperty ||
            change.Property == DefaultAltitudeProperty ||
            change.Property == AutoConnectProperty ||
            change.Property == VirtualJoystickProperty)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

// ────────────────────────────────────────────────────────────────
// 7. OfflineMapPanel
//    QGC equivalent: AppSettings/OfflineMap.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Offline map management panel: region info, tile count estimate,
/// download controls, and a list of stored map tile sets.
/// </summary>
public class OfflineMapPanel : TemplatedControl
{
    public static readonly StyledProperty<string> RegionNameProperty =
        AvaloniaProperty.Register<OfflineMapPanel, string>(nameof(RegionName), "");

    public static readonly StyledProperty<long> TileCountProperty =
        AvaloniaProperty.Register<OfflineMapPanel, long>(nameof(TileCount), 0);

    public static readonly StyledProperty<string> EstimatedSizeProperty =
        AvaloniaProperty.Register<OfflineMapPanel, string>(nameof(EstimatedSize), "0 MB");

    public static readonly StyledProperty<double> DownloadProgressProperty =
        AvaloniaProperty.Register<OfflineMapPanel, double>(nameof(DownloadProgress), 0);

    public static readonly StyledProperty<bool> IsDownloadingProperty =
        AvaloniaProperty.Register<OfflineMapPanel, bool>(nameof(IsDownloading), false);

    public static readonly StyledProperty<int> MinZoomProperty =
        AvaloniaProperty.Register<OfflineMapPanel, int>(nameof(MinZoom), 1);

    public static readonly StyledProperty<int> MaxZoomProperty =
        AvaloniaProperty.Register<OfflineMapPanel, int>(nameof(MaxZoom), 18);

    public string RegionName { get => GetValue(RegionNameProperty); set => SetValue(RegionNameProperty, value); }
    public long TileCount { get => GetValue(TileCountProperty); set => SetValue(TileCountProperty, value); }
    public string EstimatedSize { get => GetValue(EstimatedSizeProperty); set => SetValue(EstimatedSizeProperty, value); }
    public double DownloadProgress { get => GetValue(DownloadProgressProperty); set => SetValue(DownloadProgressProperty, value); }
    public bool IsDownloading { get => GetValue(IsDownloadingProperty); set => SetValue(IsDownloadingProperty, value); }
    public int MinZoom { get => GetValue(MinZoomProperty); set => SetValue(MinZoomProperty, value); }
    public int MaxZoom { get => GetValue(MaxZoomProperty); set => SetValue(MaxZoomProperty, value); }

    public ObservableCollection<StoredMap> StoredMaps { get; } = [];

    public event EventHandler? DownloadRequested;
    public event EventHandler? CancelDownloadRequested;
    public event EventHandler<StoredMap>? DeleteMapRequested;

    public bool CanDownload => !string.IsNullOrWhiteSpace(RegionName)
                            && TileCount > 0
                            && !IsDownloading;

    public void RequestDownload()
    {
        if (CanDownload)
        {
            IsDownloading = true;
            DownloadProgress = 0;
            DownloadRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RequestCancelDownload()
    {
        if (IsDownloading)
        {
            IsDownloading = false;
            DownloadProgress = 0;
            CancelDownloadRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RequestDeleteMap(StoredMap map)
    {
        DeleteMapRequested?.Invoke(this, map);
    }

    /// <summary>
    /// Formats a tile count and estimated size into a human-readable summary.
    /// </summary>
    public string GetTileSummary() =>
        $"{TileCount:N0} tiles (~{EstimatedSize})";
}

// ────────────────────────────────────────────────────────────────
// 8. ConnectionSettingsPanel
//    QGC equivalent: AppSettings/LinkSettings.qml
// ────────────────────────────────────────────────────────────────

/// <summary>
/// Connection settings panel with type selector (Serial/TCP/UDP/Bluetooth)
/// and per-type configuration fields. The active type determines which
/// properties are relevant.
/// </summary>
public class ConnectionSettingsPanel : TemplatedControl
{
    // ── Connection type ──

    public static readonly StyledProperty<ConnectionType> ConnectionTypeProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, ConnectionType>(nameof(SelectedConnectionType), ConnectionType.Serial);

    public ConnectionType SelectedConnectionType
    {
        get => GetValue(ConnectionTypeProperty);
        set => SetValue(ConnectionTypeProperty, value);
    }

    // ── Serial ──

    public static readonly StyledProperty<string> SerialPortProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, string>(nameof(SerialPort), "");

    public static readonly StyledProperty<int> BaudRateProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, int>(nameof(BaudRate), 57600);

    public string SerialPort { get => GetValue(SerialPortProperty); set => SetValue(SerialPortProperty, value); }
    public int BaudRate { get => GetValue(BaudRateProperty); set => SetValue(BaudRateProperty, value); }

    /// <summary>
    /// Standard baud rates offered in the serial configuration form.
    /// </summary>
    public static readonly int[] StandardBaudRates =
        [9600, 19200, 38400, 57600, 115200, 230400, 460800, 921600];

    /// <summary>
    /// Detected serial ports available for selection.
    /// </summary>
    public ObservableCollection<string> AvailablePorts { get; } = [];

    // ── TCP ──

    public static readonly StyledProperty<string> TcpHostProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, string>(nameof(TcpHost), "127.0.0.1");

    public static readonly StyledProperty<int> TcpPortProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, int>(nameof(TcpPort), 5760);

    public string TcpHost { get => GetValue(TcpHostProperty); set => SetValue(TcpHostProperty, value); }
    public int TcpPort { get => GetValue(TcpPortProperty); set => SetValue(TcpPortProperty, value); }

    // ── UDP ──

    public static readonly StyledProperty<int> UdpPortProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, int>(nameof(UdpPort), 14550);

    public int UdpPort { get => GetValue(UdpPortProperty); set => SetValue(UdpPortProperty, value); }

    // ── Bluetooth ──

    public static readonly StyledProperty<string> BluetoothDeviceProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, string>(nameof(BluetoothDevice), "");

    public string BluetoothDevice { get => GetValue(BluetoothDeviceProperty); set => SetValue(BluetoothDeviceProperty, value); }

    public ObservableCollection<string> AvailableBluetoothDevices { get; } = [];

    // ── Connection state ──

    public static readonly StyledProperty<bool> IsConnectedProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, bool>(nameof(IsConnected), false);

    public static readonly StyledProperty<string> StatusTextProperty =
        AvaloniaProperty.Register<ConnectionSettingsPanel, string>(nameof(StatusText), "Disconnected");

    public bool IsConnected { get => GetValue(IsConnectedProperty); set => SetValue(IsConnectedProperty, value); }
    public string StatusText { get => GetValue(StatusTextProperty); set => SetValue(StatusTextProperty, value); }

    // ── Events ──

    public event EventHandler? ConnectRequested;
    public event EventHandler? DisconnectRequested;
    public event EventHandler? ScanBluetoothRequested;
    public event EventHandler? RefreshPortsRequested;

    public void RequestConnect()
    {
        if (!IsConnected)
            ConnectRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestDisconnect()
    {
        if (IsConnected)
            DisconnectRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestScanBluetooth() => ScanBluetoothRequested?.Invoke(this, EventArgs.Empty);
    public void RequestRefreshPorts() => RefreshPortsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Returns a human-readable summary of the current connection configuration.
    /// </summary>
    public string GetConnectionSummary() => SelectedConnectionType switch
    {
        ConnectionType.Serial    => $"Serial: {SerialPort} @ {BaudRate}",
        ConnectionType.Tcp       => $"TCP: {TcpHost}:{TcpPort}",
        ConnectionType.Udp       => $"UDP: :{UdpPort}",
        ConnectionType.Bluetooth => $"BT: {BluetoothDevice}",
        _                        => "Unknown"
    };
}

// ────────────────────────────────────────────────────────────────
// 4. MavlinkSettingsPanel
//    QGC equivalent: AppSettings → MAVLink section (MavlinkSettings)
//    Exposes: system ID, component ID, MAVLink version, heartbeat rate,
//    and message forwarding on/off.
// ────────────────────────────────────────────────────────────────

/// <summary>MAVLink protocol version selector.</summary>
public enum MavlinkVersion { V1 = 1, V2 = 2 }

/// <summary>
/// Settings panel for MAVLink protocol options.
/// Mirrors the MAVLink section of QGC's AppSettings page.
/// Rendered as a labeled-row form identical in style to
/// <see cref="GeneralSettingsPanel"/> — a centered 500 px column.
/// </summary>
public class MavlinkSettingsPanel : TemplatedControl
{
    // ── System / component identity ──

    public static readonly StyledProperty<int> SystemIdProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, int>(nameof(SystemId), 255);

    public static readonly StyledProperty<int> ComponentIdProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, int>(nameof(ComponentId), 190);

    // ── Protocol ──

    public static readonly StyledProperty<MavlinkVersion> ProtocolVersionProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, MavlinkVersion>(
            nameof(ProtocolVersion), MavlinkVersion.V2);

    // ── Heartbeat ──

    public static readonly StyledProperty<bool> EmitHeartbeatProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, bool>(nameof(EmitHeartbeat), true);

    public static readonly StyledProperty<int> HeartbeatRateHzProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, int>(nameof(HeartbeatRateHz), 1);

    // ── Forwarding ──

    public static readonly StyledProperty<bool> ForwardMessagesProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, bool>(nameof(ForwardMessages), false);

    // ── Mission retention ──

    public static readonly StyledProperty<bool> PersistMissionOnSdProperty =
        AvaloniaProperty.Register<MavlinkSettingsPanel, bool>(
            nameof(PersistMissionOnSd), true);

    // ── Properties ──

    public int SystemId
    {
        get => GetValue(SystemIdProperty);
        set => SetValue(SystemIdProperty, Math.Clamp(value, 1, 255));
    }

    public int ComponentId
    {
        get => GetValue(ComponentIdProperty);
        set => SetValue(ComponentIdProperty, Math.Clamp(value, 1, 255));
    }

    public MavlinkVersion ProtocolVersion
    {
        get => GetValue(ProtocolVersionProperty);
        set => SetValue(ProtocolVersionProperty, value);
    }

    public bool EmitHeartbeat
    {
        get => GetValue(EmitHeartbeatProperty);
        set => SetValue(EmitHeartbeatProperty, value);
    }

    public int HeartbeatRateHz
    {
        get => GetValue(HeartbeatRateHzProperty);
        set => SetValue(HeartbeatRateHzProperty, Math.Clamp(value, 1, 10));
    }

    public bool ForwardMessages
    {
        get => GetValue(ForwardMessagesProperty);
        set => SetValue(ForwardMessagesProperty, value);
    }

    public bool PersistMissionOnSd
    {
        get => GetValue(PersistMissionOnSdProperty);
        set => SetValue(PersistMissionOnSdProperty, value);
    }

    // ── Events (raised when the user commits a change) ──

    public event EventHandler? SettingsChanged;

    public void CommitSettings() => SettingsChanged?.Invoke(this, EventArgs.Empty);

    // ── Helpers ──

    /// <summary>
    /// Returns a short summary string suitable for a settings overview label,
    /// e.g. "SysID 255 · MAVLink 2 · Heartbeat 1 Hz".
    /// </summary>
    public string GetSummary() =>
        $"SysID {SystemId} · MAVLink {(int)ProtocolVersion} · " +
        (EmitHeartbeat ? $"Heartbeat {HeartbeatRateHz} Hz" : "Heartbeat off");
}

// ═══════════════════════════════════════════════════════════════
// NTRIP Settings Controls
// Equivalent to QGC AppSettings/Ntrip*.qml
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// NTRIP server configuration panel — host, port, username, password, TLS toggle.
/// Fields are disabled while <see cref="IsActive"/> is true (connection live).
/// Equivalent to QGC AppSettings/NtripServerSettings.qml
/// </summary>
public sealed class NtripServerSettingsPanel : TemplatedControl
{
    public static readonly StyledProperty<string> HostProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, string>(nameof(Host), "");

    public static readonly StyledProperty<int> PortProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, int>(nameof(Port), 2101);

    public static readonly StyledProperty<string> UsernameProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, string>(nameof(Username), "");

    public static readonly StyledProperty<string> PasswordProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, string>(nameof(Password), "");

    public static readonly StyledProperty<bool> ShowPasswordProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, bool>(nameof(ShowPassword));

    public static readonly StyledProperty<bool> UseTlsProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, bool>(nameof(UseTls));

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<NtripServerSettingsPanel, bool>(nameof(IsActive));

    public string Host        { get => GetValue(HostProperty);        set => SetValue(HostProperty, value); }
    public int    Port        { get => GetValue(PortProperty);        set => SetValue(PortProperty, value); }
    public string Username    { get => GetValue(UsernameProperty);    set => SetValue(UsernameProperty, value); }
    public string Password    { get => GetValue(PasswordProperty);    set => SetValue(PasswordProperty, value); }
    public bool   ShowPassword { get => GetValue(ShowPasswordProperty); set => SetValue(ShowPasswordProperty, value); }
    public bool   UseTls      { get => GetValue(UseTlsProperty);      set => SetValue(UseTlsProperty, value); }
    public bool   IsActive    { get => GetValue(IsActiveProperty);    set => SetValue(IsActiveProperty, value); }

    /// <summary>Builds an <see cref="NtripConfiguration"/> from current field values.
    /// Mountpoint is left empty — set via <see cref="NtripMountpointBrowserPanel"/>.</summary>
    public NtripConfiguration ToConfiguration() =>
        new(Host.Trim(), Port, Mountpoint: "",
            string.IsNullOrEmpty(Username) ? null : Username.Trim(),
            string.IsNullOrEmpty(Password) ? null : Password);
}

/// <summary>
/// NTRIP connection status panel — status dot, message label, connect/disconnect button,
/// and transfer statistics when streaming.
/// Equivalent to QGC AppSettings/NtripConnectionStatus.qml
/// </summary>
public sealed class NtripConnectionStatusPanel : TemplatedControl
{
    public static readonly StyledProperty<NtripClientState> ConnectionStateProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, NtripClientState>(nameof(ConnectionState));

    public static readonly StyledProperty<string> NtripStatusMessageProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, string>(nameof(NtripStatusMessage), "Disconnected");

    public static readonly StyledProperty<long> MessagesReceivedProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, long>(nameof(MessagesReceived));

    public static readonly StyledProperty<long> BytesReceivedProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, long>(nameof(BytesReceived));

    public static readonly StyledProperty<double> DataRateBytesPerSecProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, double>(nameof(DataRateBytesPerSec));

    public static readonly StyledProperty<string> GgaSourceProperty =
        AvaloniaProperty.Register<NtripConnectionStatusPanel, string>(nameof(GgaSource), "");

    public NtripClientState ConnectionState    { get => GetValue(ConnectionStateProperty);       set => SetValue(ConnectionStateProperty, value); }
    public string NtripStatusMessage           { get => GetValue(NtripStatusMessageProperty);    set => SetValue(NtripStatusMessageProperty, value); }
    public long   MessagesReceived             { get => GetValue(MessagesReceivedProperty);      set => SetValue(MessagesReceivedProperty, value); }
    public long   BytesReceived               { get => GetValue(BytesReceivedProperty);         set => SetValue(BytesReceivedProperty, value); }
    public double DataRateBytesPerSec         { get => GetValue(DataRateBytesPerSecProperty);   set => SetValue(DataRateBytesPerSecProperty, value); }
    public string GgaSource                   { get => GetValue(GgaSourceProperty);             set => SetValue(GgaSourceProperty, value); }

    public event EventHandler? ConnectRequested;
    public event EventHandler? DisconnectRequested;

    /// <summary>Fires connect or disconnect depending on the current state.</summary>
    public void ToggleConnection()
    {
        if (ConnectionState is NtripClientState.Connected or NtripClientState.Streaming)
            DisconnectRequested?.Invoke(this, EventArgs.Empty);
        else
            ConnectRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Status dot color for use in control templates.</summary>
    public Color GetStateColor() => ConnectionState switch
    {
        NtripClientState.Connected  or
        NtripClientState.Streaming   => QgcColors.ColorGreen,
        NtripClientState.Connecting  => QgcColors.ColorOrange,
        NtripClientState.Failed      => QgcColors.ColorRed,
        _                            => QgcColors.ColorGrey
    };

    /// <summary>Button label ("Connect" / "Connecting…" / "Disconnect" / "Retry").</summary>
    public string GetButtonText() => ConnectionState switch
    {
        NtripClientState.Connecting  => "Connecting…",
        NtripClientState.Connected
        or NtripClientState.Streaming => "Disconnect",
        NtripClientState.Failed       => "Retry",
        _                             => "Connect"
    };

    /// <summary>One-line transfer stats, empty when disconnected or no data yet.</summary>
    public string GetStatsText()
    {
        if (ConnectionState is not (NtripClientState.Connected or NtripClientState.Streaming)
            || MessagesReceived == 0)
            return "";

        var parts = new List<string>();
        parts.Add($"{MessagesReceived} msg");
        if (BytesReceived        > 0) parts.Add(FormatBytes(BytesReceived));
        if (DataRateBytesPerSec  > 0) parts.Add(FormatRate(DataRateBytesPerSec));
        if (!string.IsNullOrEmpty(GgaSource)) parts.Add($"GGA: {GgaSource}");
        return string.Join(" · ", parts);
    }

    private static string FormatBytes(long b) =>
        b < 1_024         ? $"{b} B"
        : b < 1_048_576   ? $"{b / 1024.0:F1} KB"
                          : $"{b / 1_048_576.0:F1} MB";

    private static string FormatRate(double bps) =>
        bps < 1024 ? $"{bps:F0} B/s" : $"{bps / 1024:F1} KB/s";
}

/// <summary>
/// NTRIP mountpoint browser — manual text entry + Browse button that queries the caster,
/// and a scrollable list of available mountpoints to pick from.
/// Equivalent to QGC AppSettings/NtripMountpointBrowser.qml
/// </summary>
public sealed class NtripMountpointBrowserPanel : TemplatedControl
{
    public static readonly StyledProperty<string> CurrentMountpointProperty =
        AvaloniaProperty.Register<NtripMountpointBrowserPanel, string>(nameof(CurrentMountpoint), "");

    public static readonly StyledProperty<IReadOnlyList<NtripMountpoint>?> MountpointsProperty =
        AvaloniaProperty.Register<NtripMountpointBrowserPanel, IReadOnlyList<NtripMountpoint>?>(nameof(Mountpoints));

    public static readonly StyledProperty<bool> IsFetchingProperty =
        AvaloniaProperty.Register<NtripMountpointBrowserPanel, bool>(nameof(IsFetching));

    public static readonly StyledProperty<string> FetchErrorProperty =
        AvaloniaProperty.Register<NtripMountpointBrowserPanel, string>(nameof(FetchError), "");

    public string CurrentMountpoint
    {
        get => GetValue(CurrentMountpointProperty);
        set => SetValue(CurrentMountpointProperty, value);
    }

    public IReadOnlyList<NtripMountpoint>? Mountpoints
    {
        get => GetValue(MountpointsProperty);
        set => SetValue(MountpointsProperty, value);
    }

    public bool IsFetching
    {
        get => GetValue(IsFetchingProperty);
        set => SetValue(IsFetchingProperty, value);
    }

    public string FetchError
    {
        get => GetValue(FetchErrorProperty);
        set => SetValue(FetchErrorProperty, value);
    }

    /// <summary>Raised when the user clicks Browse — caller invokes <see cref="NtripClient.GetMountpointsAsync"/>.</summary>
    public event EventHandler? FetchMountpointsRequested;

    /// <summary>Raised when the user selects a mountpoint; argument is the mountpoint name.</summary>
    public event EventHandler<string>? MountpointSelected;

    public void RequestFetchMountpoints() =>
        FetchMountpointsRequested?.Invoke(this, EventArgs.Empty);

    public void SelectMountpoint(string mountpointName)
    {
        CurrentMountpoint = mountpointName;
        MountpointSelected?.Invoke(this, mountpointName);
    }
}
