namespace VGC.Mission;

public enum RallyPointTransferType
{
    None,
    Read,
    Write,
    Clear
}

public enum RallyPointTransferError
{
    None,
    Busy,
    EmptyRallyPoints,
    ProtocolUnsupported,
    VehicleRejected,
    Timeout
}

public sealed class RallyPointTransferManager
{
    public RallyPointTransferType TransferType { get; private set; }

    public RallyPointTransferError LastError { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public bool InProgress => TransferType != RallyPointTransferType.None;

    public int PointCount { get; private set; }

    public double Progress { get; private set; }

    public bool BeginRead()
    {
        return TryBegin(RallyPointTransferType.Read, pointCount: 0);
    }

    public bool BeginWrite(RallyPointsPlan rallyPoints)
    {
        if (rallyPoints.Points.Count == 0)
        {
            Fail(RallyPointTransferError.EmptyRallyPoints, "Rally point write requires at least one point.");
            return false;
        }

        return TryBegin(RallyPointTransferType.Write, rallyPoints.Points.Count);
    }

    public bool BeginClear()
    {
        return TryBegin(RallyPointTransferType.Clear, pointCount: 0);
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
        TransferType = RallyPointTransferType.None;
        LastError = RallyPointTransferError.None;
        LastErrorMessage = null;
        Progress = 1;
    }

    public void Fail(RallyPointTransferError error, string message)
    {
        TransferType = RallyPointTransferType.None;
        LastError = error;
        LastErrorMessage = message;
        Progress = 0;
    }

    private bool TryBegin(RallyPointTransferType transferType, int pointCount)
    {
        if (InProgress)
        {
            LastError = RallyPointTransferError.Busy;
            LastErrorMessage = "Rally point transfer already in progress.";
            return false;
        }

        TransferType = transferType;
        LastError = RallyPointTransferError.None;
        LastErrorMessage = null;
        PointCount = pointCount;
        Progress = 0;
        return true;
    }
}
