using VGC.Core.Settings;

namespace VGC.Comms;

public sealed class LinkConfigurationState
{
    public string? PreferredActiveLinkName { get; set; }

    public List<PersistedLinkConfiguration> Links { get; set; } = new();
}

public sealed record PersistedLinkConfiguration(
    string Name,
    LinkType Type,
    int? LocalPort = null,
    string? TargetHost = null,
    int? TargetPort = null,
    string? SerialPortName = null,
    int? BaudRate = null,
    int? DataBits = null,
    string? Parity = null,
    string? StopBits = null,
    string? Host = null,
    int? Port = null,
    bool IsServer = false,
    string? FilePath = null,
    double ReplaySpeed = 1.0,
    bool LoopReplay = false)
{
    public static PersistedLinkConfiguration FromRuntime(LinkConfiguration configuration)
    {
        return configuration switch
        {
            UdpLinkConfiguration udp => new PersistedLinkConfiguration(
                udp.Name,
                udp.Type,
                udp.LocalPort,
                udp.TargetHost,
                udp.TargetPort),
            SerialLinkConfiguration serial => new PersistedLinkConfiguration(
                serial.Name,
                serial.Type,
                SerialPortName: serial.PortName,
                BaudRate: serial.BaudRate,
                DataBits: serial.DataBits,
                Parity: serial.Parity,
                StopBits: serial.StopBits),
            TcpLinkConfiguration tcp => new PersistedLinkConfiguration(
                tcp.Name,
                tcp.Type,
                Host: tcp.Host,
                Port: tcp.Port,
                IsServer: tcp.IsServer),
            LogReplayLinkConfiguration replay => new PersistedLinkConfiguration(
                replay.Name,
                replay.Type,
                FilePath: replay.FilePath,
                ReplaySpeed: replay.Speed,
                LoopReplay: replay.Loop),
            _ => new PersistedLinkConfiguration(configuration.Name, configuration.Type)
        };
    }

    public LinkConfiguration ToRuntimeConfiguration()
    {
        return Type switch
        {
            LinkType.Udp => new UdpLinkConfiguration(
                Name,
                LocalPort ?? 14550,
                TargetHost,
                TargetPort),
            LinkType.Serial => new SerialLinkConfiguration(
                Name,
                SerialPortName ?? string.Empty,
                BaudRate ?? 57600,
                DataBits ?? 8,
                Parity ?? "None",
                StopBits ?? "One"),
            LinkType.Tcp => new TcpLinkConfiguration(
                Name,
                Host ?? "127.0.0.1",
                Port ?? 5760,
                IsServer),
            LinkType.LogReplay => new LogReplayLinkConfiguration(
                Name,
                FilePath ?? string.Empty,
                ReplaySpeed,
                LoopReplay),
            LinkType.Mock => new MockLinkConfiguration(Name),
            _ => throw new NotSupportedException($"{Type} link configuration is not supported yet.")
        };
    }
}

public interface ILinkConfigurationStore
{
    LinkConfigurationState Current { get; }

    Task LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(LinkConfigurationState state, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsLinkConfigurationStore : ILinkConfigurationStore
{
    private readonly IAppSettingsStore _settingsStore;

    public AppSettingsLinkConfigurationStore(IAppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
    }

    public LinkConfigurationState Current => _settingsStore.Current.LinkConfiguration;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _settingsStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        _settingsStore.Current.LinkConfiguration ??= new LinkConfigurationState();
    }

    public async Task SaveAsync(LinkConfigurationState state, CancellationToken cancellationToken = default)
    {
        _settingsStore.Current.LinkConfiguration = state;
        await _settingsStore.SaveAsync(cancellationToken).ConfigureAwait(false);
    }
}
