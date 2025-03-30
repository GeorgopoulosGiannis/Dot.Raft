namespace Dot.Raft.TestHarness;

public class InMemoryNetwork : IRaftTransport
{
    private readonly Dictionary<NodeId, TestRaftNodeHost> _nodes = new();

    public void Register(NodeId id, TestRaftNodeHost host)
    {
        _nodes[id] = host;
    }

    public Task SendAsync(NodeId to, object message)
    {
        if (_nodes.TryGetValue(to, out var host))
        {
            host.EnqueueMessage(message);
        }

        return Task.CompletedTask;
    }
}