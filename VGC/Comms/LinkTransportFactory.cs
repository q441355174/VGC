namespace VGC.Comms;

public interface ILinkTransportFactory
{
    bool CanCreate(LinkType type);

    ILinkTransport Create(LinkConfiguration configuration);
}

public interface ILogReplayTimingPolicy
{
    TimeSpan ScaleDelay(TimeSpan originalDelay, double speed);
}

public sealed class DefaultLogReplayTimingPolicy : ILogReplayTimingPolicy
{
    public TimeSpan ScaleDelay(TimeSpan originalDelay, double speed)
    {
        if (speed <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), "Replay speed must be greater than zero.");
        }

        return TimeSpan.FromTicks((long)Math.Max(0, originalDelay.Ticks / speed));
    }
}
