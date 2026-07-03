using VGC.Mavlink;

namespace VGC.Vehicles;

public sealed record PendingVehicleCommand(
    byte TargetComponentId,
    ushort Command,
    DateTimeOffset SentAt,
    int RetryCount = 0,
    int MaxRetries = 3)
{
    public bool IsExpired(TimeSpan timeout, DateTimeOffset now)
    {
        return now - SentAt > timeout;
    }

    public bool CanRetry => RetryCount < MaxRetries;

    public PendingVehicleCommand WithRetry()
    {
        return this with { RetryCount = RetryCount + 1, SentAt = DateTimeOffset.UtcNow };
    }
}

public sealed record VehicleCommandSendResult(
    bool Sent,
    PendingVehicleCommand? PendingCommand,
    string? FailureReason = null)
{
    public static VehicleCommandSendResult SentResult(PendingVehicleCommand pendingCommand)
    {
        return new VehicleCommandSendResult(true, pendingCommand);
    }

    public static VehicleCommandSendResult Duplicate(PendingVehicleCommand pendingCommand)
    {
        return new VehicleCommandSendResult(false, pendingCommand, "Duplicate command pending.");
    }

    public static VehicleCommandSendResult MaxRetriesExceeded(PendingVehicleCommand pendingCommand)
    {
        return new VehicleCommandSendResult(false, pendingCommand, $"Command retry limit ({pendingCommand.MaxRetries}) exceeded.");
    }
}

public sealed class VehicleCommandQueue
{
    private readonly List<PendingVehicleCommand> _pendingCommands = [];
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public IReadOnlyList<PendingVehicleCommand> PendingCommands => _pendingCommands;

    public int PendingCount => _pendingCommands.Count;

    public TimeSpan CommandTimeout { get; set; } = DefaultTimeout;

    public VehicleCommandSendResult TryEnqueue(byte targetComponentId, ushort command, int maxRetries = 3)
    {
        if (_pendingCommands.Any(c => c.TargetComponentId == targetComponentId && c.Command == command))
        {
            var existing = _pendingCommands.First(c => c.TargetComponentId == targetComponentId && c.Command == command);
            return VehicleCommandSendResult.Duplicate(existing);
        }

        var pending = new PendingVehicleCommand(targetComponentId, command, DateTimeOffset.UtcNow, 0, maxRetries);
        _pendingCommands.Add(pending);
        return VehicleCommandSendResult.SentResult(pending);
    }

    public bool TryAcknowledge(byte targetComponentId, ushort command)
    {
        var removed = _pendingCommands.RemoveAll(c => c.TargetComponentId == targetComponentId && c.Command == command);
        return removed > 0;
    }

    public IReadOnlyList<PendingVehicleCommand> GetExpiredCommands()
    {
        var now = DateTimeOffset.UtcNow;
        return _pendingCommands
            .Where(c => c.IsExpired(CommandTimeout, now))
            .ToArray();
    }

    public VehicleCommandSendResult? TryRetry(byte targetComponentId, ushort command)
    {
        var index = _pendingCommands.FindIndex(c => c.TargetComponentId == targetComponentId && c.Command == command);
        if (index < 0)
        {
            return null;
        }

        var cmd = _pendingCommands[index];
        if (!cmd.IsExpired(CommandTimeout, DateTimeOffset.UtcNow))
        {
            return null;
        }

        if (!cmd.CanRetry)
        {
            _pendingCommands.RemoveAt(index);
            return VehicleCommandSendResult.MaxRetriesExceeded(cmd);
        }

        var retried = cmd.WithRetry();
        _pendingCommands[index] = retried;
        return VehicleCommandSendResult.SentResult(retried);
    }

    public int CleanupExpired()
    {
        var now = DateTimeOffset.UtcNow;
        return _pendingCommands.RemoveAll(c => c.IsExpired(CommandTimeout, now) && !c.CanRetry);
    }
}
