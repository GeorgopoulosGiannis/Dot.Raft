namespace Dot.Raft.TestHarness;

public class InMemoryNetwork : IRaftTransport
{
    private readonly Dictionary<NodeId, TestRaftNodeHost> _nodes = new();
    private readonly HashSet<(NodeId From, NodeId To)> _partitions = new();

    public void Register(NodeId id, TestRaftNodeHost receiver)
    {
        _nodes[id] = receiver;
    }

    public Task SendAsync(NodeId to, object message)
    {
        return SendAsync(default, to, message);
    }

    public Task SendAsync(NodeId from, NodeId to, object message)
    {
        if (_partitions.Contains((from, to)) || _partitions.Contains((to, from)))
        {
            return Task.CompletedTask; // simulate dropped message
        }

        if (_nodes.TryGetValue(to, out var receiver))
        {
            receiver.EnqueueMessage(message);
        }

        return Task.CompletedTask;
    }

    public void Partition(NodeId nodeA, NodeId nodeB)
    {
        _partitions.Add((nodeA, nodeB));
        _partitions.Add((nodeB, nodeA));
    }

    public void Heal(NodeId nodeA, NodeId nodeB)
    {
        _partitions.Remove((nodeA, nodeB));
        _partitions.Remove((nodeB, nodeA));
    }

    public void HealAll(NodeId node)
    {
        _partitions.RemoveWhere(pair => pair.From == node || pair.To == node);
    }

    public IEnumerable<NodeId> GetNodes() => _nodes.Keys;
}