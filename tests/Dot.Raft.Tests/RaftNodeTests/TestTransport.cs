namespace Dot.Raft.Tests.RaftNodeTests;

public class TestTransport : IRaftTransport
{
    public readonly List<SentMessage> Sent = [];

    public Task SendAsync(NodeId to, object message)
    {
        Sent.Add(new SentMessage(to, message));
        return Task.CompletedTask;
    }
}

public record SentMessage(NodeId To, object Message);

public class TestTransportWithCallback(Action<NodeId, object> onSend) : IRaftTransport
{
    public Task SendAsync(NodeId to, object message)
    {
        onSend(to, message);
        return Task.CompletedTask;
    }
}