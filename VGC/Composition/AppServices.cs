using VGC.Core.Application;
using VGC.Core.Logging;
using VGC.Core.Settings;
using VGC.Comms;
using VGC.Mavlink;
using VGC.Vehicles;
using VGC.ViewModels;

namespace VGC.Composition;

public sealed class AppServices
{
    public AppServices(
        IAppLifecycleService lifecycle,
        IAppCloseCoordinator closeCoordinator,
        IAppSettingsStore settingsStore,
        IAppLogger logger,
        LinkManager linkManager,
        MavlinkProtocol mavlinkProtocol,
        GcsHeartbeatService gcsHeartbeatService,
        MultiVehicleManager multiVehicleManager,
        ShellViewModel shellViewModel)
    {
        Lifecycle = lifecycle;
        CloseCoordinator = closeCoordinator;
        SettingsStore = settingsStore;
        Logger = logger;
        LinkManager = linkManager;
        MavlinkProtocol = mavlinkProtocol;
        GcsHeartbeatService = gcsHeartbeatService;
        MultiVehicleManager = multiVehicleManager;
        ShellViewModel = shellViewModel;
    }

    public IAppLifecycleService Lifecycle { get; }

    public IAppCloseCoordinator CloseCoordinator { get; }

    public IAppSettingsStore SettingsStore { get; }

    public IAppLogger Logger { get; }

    public LinkManager LinkManager { get; }

    public MavlinkProtocol MavlinkProtocol { get; }

    public GcsHeartbeatService GcsHeartbeatService { get; }

    public MultiVehicleManager MultiVehicleManager { get; }

    public ShellViewModel ShellViewModel { get; }
}
