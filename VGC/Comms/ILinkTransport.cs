namespace VGC.Comms;

public interface ILinkTransport : IAsyncDisposable
{
    event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    event EventHandler<BytesReceivedEventArgs>? BytesSent;

    event EventHandler<string>? CommunicationError;

    LinkConfiguration Configuration { get; }

    bool IsConnected { get; }

    bool CanSend { get; }

    Task ConnectAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();

    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
}
