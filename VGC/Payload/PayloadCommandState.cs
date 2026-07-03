namespace VGC.Payload;

public enum PayloadCommandStatus
{
    Idle,
    Pending,
    Succeeded,
    Failed
}

public sealed record PayloadCommandState(
    string Name,
    PayloadCommandStatus Status,
    int AttemptCount = 0,
    string? Error = null,
    bool CanRetry = false)
{
    public static PayloadCommandState Idle(string name)
    {
        return new PayloadCommandState(name, PayloadCommandStatus.Idle);
    }

    public PayloadCommandState Begin()
    {
        return this with
        {
            Status = PayloadCommandStatus.Pending,
            AttemptCount = AttemptCount + 1,
            Error = null,
            CanRetry = false
        };
    }

    public PayloadCommandState Succeed()
    {
        return this with
        {
            Status = PayloadCommandStatus.Succeeded,
            Error = null,
            CanRetry = false
        };
    }

    public PayloadCommandState Fail(string error, bool canRetry)
    {
        return this with
        {
            Status = PayloadCommandStatus.Failed,
            Error = string.IsNullOrWhiteSpace(error) ? "Command failed" : error,
            CanRetry = canRetry
        };
    }

    public string StatusText => Status switch
    {
        PayloadCommandStatus.Idle => $"{Name} idle",
        PayloadCommandStatus.Pending => $"{Name} pending",
        PayloadCommandStatus.Succeeded => $"{Name} succeeded",
        PayloadCommandStatus.Failed => Error is { Length: > 0 } ? $"{Name} failed: {Error}" : $"{Name} failed",
        _ => $"{Name} unknown"
    };
}
