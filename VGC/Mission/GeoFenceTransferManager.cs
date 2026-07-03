namespace VGC.Mission;

public enum GeoFenceTransferType
{
    None,
    Read,
    Write,
    Clear
}

public enum GeoFenceTransferError
{
    None,
    Busy,
    EmptyGeoFence,
    ProtocolUnsupported,
    VehicleRejected,
    Timeout
}

public sealed class GeoFenceTransferManager
{
    public GeoFenceTransferType TransferType { get; private set; }

    public GeoFenceTransferError LastError { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public bool InProgress => TransferType != GeoFenceTransferType.None;

    public int PolygonCount { get; private set; }

    public int CircleCount { get; private set; }

    public double Progress { get; private set; }

    public bool BeginRead()
    {
        return TryBegin(GeoFenceTransferType.Read, polygonCount: 0, circleCount: 0);
    }

    public bool BeginWrite(GeoFencePlan geoFence)
    {
        if (geoFence.Polygons.Count == 0 && geoFence.Circles.Count == 0 && geoFence.BreachReturn is null)
        {
            Fail(GeoFenceTransferError.EmptyGeoFence, "GeoFence write requires at least one polygon, circle, or breach return point.");
            return false;
        }

        return TryBegin(GeoFenceTransferType.Write, geoFence.Polygons.Count, geoFence.Circles.Count);
    }

    public bool BeginClear()
    {
        return TryBegin(GeoFenceTransferType.Clear, polygonCount: 0, circleCount: 0);
    }

    public void MarkProgress(double progress)
    {
        if (!InProgress)
        {
            return;
        }

        Progress = Math.Clamp(progress, 0, 1);
    }

    public void Complete()
    {
        TransferType = GeoFenceTransferType.None;
        LastError = GeoFenceTransferError.None;
        LastErrorMessage = null;
        Progress = 1;
    }

    public void Fail(GeoFenceTransferError error, string message)
    {
        TransferType = GeoFenceTransferType.None;
        LastError = error;
        LastErrorMessage = message;
        Progress = 0;
    }

    private bool TryBegin(GeoFenceTransferType transferType, int polygonCount, int circleCount)
    {
        if (InProgress)
        {
            LastError = GeoFenceTransferError.Busy;
            LastErrorMessage = "GeoFence transfer already in progress.";
            return false;
        }

        TransferType = transferType;
        LastError = GeoFenceTransferError.None;
        LastErrorMessage = null;
        PolygonCount = polygonCount;
        CircleCount = circleCount;
        Progress = 0;
        return true;
    }
}
