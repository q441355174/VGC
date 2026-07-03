using VGC.Core.Application;
using VGC.Core.Logging;
using VGC.Core.Settings;
using VGC.Comms;
using VGC.Mavlink;
using VGC.Vehicles;
using VGC.ViewModels;

namespace VGC.Composition;

public static class ServiceRegistration
{
    public static AppServices Build()
    {
        var settingsStore = new JsonAppSettingsStore();
        var logger = new AppLogger();
        var linkConfigurationStore = new AppSettingsLinkConfigurationStore(settingsStore);
        var linkManager = new LinkManager(logger, linkConfigurationStore);
        var mavlinkFrameWriter = new MavlinkFrameWriter();
        var mavlinkOutboundRouter = new MavlinkOutboundRouter(mavlinkFrameWriter);
        var mavlinkProtocol = new MavlinkProtocol();
        mavlinkProtocol.Attach(linkManager);
        var gcsHeartbeatService = new GcsHeartbeatService(linkManager, mavlinkFrameWriter, logger, mavlinkOutboundRouter);
        var multiVehicleManager = new MultiVehicleManager(mavlinkProtocol, logger);
        var closeCoordinator = new AppCloseCoordinator();
        var lifecycle = new AppLifecycleService(settingsStore, logger);
        var settingsViewModel = new SettingsViewModel(SettingsManager.CreateDefault(), settingsStore, linkConfigurationStore, linkManager);
        var shellViewModel = new ShellViewModel(lifecycle, closeCoordinator, logger, linkManager, mavlinkProtocol, gcsHeartbeatService, multiVehicleManager, settingsViewModel, linkConfigurationStore: linkConfigurationStore);

        return new AppServices(lifecycle, closeCoordinator, settingsStore, logger, linkManager, mavlinkProtocol, gcsHeartbeatService, multiVehicleManager, shellViewModel);
    }
}
