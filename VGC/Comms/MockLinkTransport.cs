namespace VGC.Comms;

public sealed class MockLinkTransport : ILinkTransport
{
    public MockLinkTransport(string name = "Mock Link")
    {
        Configuration = new MockLinkConfiguration(name);
    }

    public event EventHandler<BytesReceivedEventArgs>? BytesReceived;

    public event EventHandler<BytesReceivedEventArgs>? BytesSent;

    public event EventHandler<string>? CommunicationError;

    public LinkConfiguration Configuration { get; }

    public bool IsConnected { get; private set; }

    public bool CanSend => IsConnected;

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            CommunicationError?.Invoke(this, "Mock link is not connected.");
            return ValueTask.CompletedTask;
        }

        BytesSent?.Invoke(this, new BytesReceivedEventArgs(this, bytes.ToArray()));
        return ValueTask.CompletedTask;
    }

    public void EmitIncoming(byte[] bytes)
    {
        if (!IsConnected)
        {
            CommunicationError?.Invoke(this, "Mock link is not connected.");
            return;
        }

        BytesReceived?.Invoke(this, new BytesReceivedEventArgs(this, bytes));
    }

    public ValueTask DisposeAsync()
    {
        IsConnected = false;
        return ValueTask.CompletedTask;
    }
}
