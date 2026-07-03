using VGC.Firmware;
using VGC.Vehicles;

namespace VGC.Mission;

public enum PlanDocumentSection
{
    Mission,
    GeoFence,
    Rally
}

public sealed record PlanTransferSupport(
    bool IsConnected,
    bool GeoFenceSupported,
    bool RallySupported)
{
    public static PlanTransferSupport Offline { get; } = new(false, false, false);
}

public sealed class PlanTransferSupportPolicy
{
    public PlanTransferSupport GetSupportForVehicle(Vehicle? vehicle, FirmwarePluginManager? firmwarePluginManager = null)
    {
        if (vehicle is null)
        {
            return PlanTransferSupport.Offline;
        }

        var plugin = (firmwarePluginManager ?? new FirmwarePluginManager()).GetPlugin(vehicle.Autopilot);
        return new PlanTransferSupport(
            IsConnected: true,
            GeoFenceSupported: plugin.Supports.GeoFenceTransfer,
            RallySupported: plugin.Supports.RallyPointTransfer);
    }

    public bool CanEditOffline(PlanDocumentSection section)
    {
        return section is PlanDocumentSection.Mission or PlanDocumentSection.GeoFence or PlanDocumentSection.Rally;
    }

    public PlanTransferGateResult CanTransferGeoFence(PlanTransferSupport support)
    {
        if (!support.IsConnected)
        {
            return PlanTransferGateResult.Blocked("GeoFence transfer requires a connected vehicle.");
        }

        return support.GeoFenceSupported
            ? PlanTransferGateResult.Allowed
            : PlanTransferGateResult.Blocked("Connected vehicle does not report GeoFence transfer support.");
    }

    public PlanTransferGateResult CanTransferRally(PlanTransferSupport support)
    {
        if (!support.IsConnected)
        {
            return PlanTransferGateResult.Blocked("Rally transfer requires a connected vehicle.");
        }

        return support.RallySupported
            ? PlanTransferGateResult.Allowed
            : PlanTransferGateResult.Blocked("Connected vehicle does not report Rally transfer support.");
    }
}

public sealed record PlanTransferGateResult(bool IsAllowed, string? Reason)
{
    public static PlanTransferGateResult Allowed { get; } = new(true, null);

    public static PlanTransferGateResult Blocked(string reason)
    {
        return new PlanTransferGateResult(false, reason);
    }
}
