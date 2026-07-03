using System.Globalization;
using VGC.Comms;
using VGC.Facts;

namespace VGC.Mavlink;

public sealed class MavlinkParameterService
{
    private readonly MavlinkFrameWriter _frameWriter;
    private readonly MavlinkOutboundRouter _outboundRouter;

    public MavlinkParameterService(
        byte systemId = 255,
        byte componentId = 190,
        MavlinkFrameWriter? frameWriter = null,
        MavlinkOutboundRouter? outboundRouter = null)
    {
        SystemId = systemId;
        ComponentId = componentId;
        _outboundRouter = outboundRouter ?? new MavlinkOutboundRouter(frameWriter);
        _frameWriter = _outboundRouter.FrameWriter;
    }

    public byte SystemId { get; }

    public byte ComponentId { get; }

    public byte[] CreateParamRequestListFrame(MavlinkParameterRequestList request)
    {
        return _frameWriter.CreateParamRequestList(SystemId, ComponentId, request);
    }

    public byte[] CreateParamRequestReadFrame(MavlinkParameterRequestRead request)
    {
        return _frameWriter.CreateParamRequestRead(SystemId, ComponentId, request);
    }

    public byte[] CreateParamSetFrame(MavlinkParameterSet parameterSet)
    {
        return _frameWriter.CreateParamSet(SystemId, ComponentId, parameterSet);
    }

    public byte[] CreateParamSetFrame(byte targetSystemId, byte targetComponentId, Fact fact, MavlinkParamType type = MavlinkParamType.Real32)
    {
        if (fact.RawValue is null)
        {
            throw new InvalidOperationException($"Parameter {fact.Name} has no value to write.");
        }

        var value = Convert.ToSingle(fact.RawValue, CultureInfo.InvariantCulture);
        return CreateParamSetFrame(new MavlinkParameterSet(targetSystemId, targetComponentId, fact.Name, value, type));
    }

    public ValueTask SendParamRequestListAsync(ILinkTransport link, MavlinkParameterRequestList request, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendParamRequestListAsync(link, SystemId, ComponentId, request, cancellationToken);
    }

    public ValueTask SendParamRequestReadAsync(ILinkTransport link, MavlinkParameterRequestRead request, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendParamRequestReadAsync(link, SystemId, ComponentId, request, cancellationToken);
    }

    public ValueTask SendParamSetAsync(ILinkTransport link, MavlinkParameterSet parameterSet, CancellationToken cancellationToken = default)
    {
        return _outboundRouter.SendParamSetAsync(link, SystemId, ComponentId, parameterSet, cancellationToken);
    }

    public ValueTask SendParamSetAsync(
        ILinkTransport link,
        byte targetSystemId,
        byte targetComponentId,
        Fact fact,
        MavlinkParamType type = MavlinkParamType.Real32,
        CancellationToken cancellationToken = default)
    {
        if (fact.RawValue is null)
        {
            throw new InvalidOperationException($"Parameter {fact.Name} has no value to write.");
        }

        var value = Convert.ToSingle(fact.RawValue, CultureInfo.InvariantCulture);
        return SendParamSetAsync(link, new MavlinkParameterSet(targetSystemId, targetComponentId, fact.Name, value, type), cancellationToken);
    }

    public async ValueTask SendParamSetWithReadbackAsync(
        ILinkTransport link,
        MavlinkParameterSet parameterSet,
        CancellationToken cancellationToken = default)
    {
        await SendParamSetAsync(link, parameterSet, cancellationToken).ConfigureAwait(false);
        await SendParamRequestReadAsync(
            link,
            MavlinkParameterRequestRead.ByName(parameterSet.TargetSystemId, parameterSet.TargetComponentId, parameterSet.Name),
            cancellationToken).ConfigureAwait(false);
    }
}
