namespace Dot.Raft.TestHarness;

/// <summary>
/// Simulates a RAFT cluster in-memory for testing purposes. 
/// Provides utilities to join nodes, control time progression, simulate partitions, 
/// and perform assertions against the cluster.
/// </summary>
public class RaftTestCluster
{
    private readonly List<TestRaftNodeHost> _hosts = [];
    private readonly InMemoryNetwork _network = new();

    /// <summary>
    /// Gets the transport layer used for sending messages between nodes.
    /// </summary>
    public IRaftTransport Transport => _network;

    /// <summary>
    /// Joins a new RAFT node to the cluster and registers it in the simulated network.
    /// </summary>
    /// <param name="node">The RAFT node to join.</param>
    public void Join(IRaftNode node)
    {
        var host = new TestRaftNodeHost(node);
        _hosts.Add(host);
        _network.Register(host.Node.Id, host);
    }

    /// <summary>
    /// Advances logical time by ticking all nodes in the cluster the specified number of times.
    /// </summary>
    /// <param name="times">The number of logical ticks to advance.</param>
    public async Task TickAllAsync(int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            foreach (var host in _hosts)
                await host.TickAsync();
        }
    }

    /// <summary>
    /// Returns the single leader in the cluster, or throws an exception 
    /// if zero or multiple leaders exist.
    /// </summary>
    /// <returns>The node currently acting as the RAFT leader.</returns>
    /// <exception cref="InvalidOperationException">Thrown if there isn't exactly one leader.</exception>
    public IRaftNode GetLeader()
    {
        var leaders = _hosts.Where(host =>
            host.Node.Role == RaftRole.Leader).ToList();

        if (leaders.Count != 1)
            throw new InvalidOperationException($"Expected exactly one leader, but found {leaders.Count}.");

        return leaders[0].Node;
    }

    /// <summary>
    /// Returns all nodes in the cluster that are currently in the leader role.
    /// Useful for testing split-brain or partition scenarios.
    /// </summary>
    /// <returns>A list of leader nodes.</returns>
    public IReadOnlyList<IRaftNode> GetAllLeaders()
    {
        return _hosts.Where(host =>
            host.Node.Role == RaftRole.Leader).Select(x => x.Node).ToList();
    }

    /// <summary>
    /// Applies a visitor to all nodes in the cluster for inspection or assertion.
    /// </summary>
    /// <param name="visitor">The visitor to apply to each node.</param>
    public void VisitNodes(IRaftNodeVisitor visitor)
    {
        foreach (var host in _hosts)
        {
            host.Node.Accept(visitor);
        }
    }

    /// <summary>
    /// Submits a command to the current leader in the cluster.
    /// </summary>
    /// <param name="command">The command to submit.</param>
    public async Task SubmitToLeaderAsync(object command)
    {
        var leader = GetLeader();
        await leader.SubmitCommandAsync(command);
    }

    /// <summary>
    /// Continuously ticks the cluster until a leader is elected, or the max number of ticks is reached.
    /// </summary>
    /// <param name="maxTicks">The maximum number of ticks to attempt before failing.</param>
    /// <exception cref="Exception">Thrown if no leader is elected within the allowed ticks.</exception>
    public async Task TickUntilLeaderElected(int maxTicks = 100)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            await TickAllAsync();

            var leaderCount = _hosts.Count(h => h.Node.Role == RaftRole.Leader);
            if (leaderCount == 1)
                return;
        }

        throw new Exception("Leader was not elected in time.");
    }

    /// <summary>
    /// Simulates a bidirectional network partition between two nodes.
    /// Messages between the nodes will be dropped.
    /// </summary>
    /// <param name="a">The first node ID.</param>
    /// <param name="b">The second node ID.</param>
    public void Partition(NodeId a, NodeId b) => _network.Partition(a, b);

    /// <summary>
    /// Heals a previously partitioned connection between two nodes, allowing communication again.
    /// </summary>
    /// <param name="a">The first node ID.</param>
    /// <param name="b">The second node ID.</param>
    public void Heal(NodeId a, NodeId b) => _network.Heal(a, b);

    /// <summary>
    /// Heals all partitions involving the specified node.
    /// </summary>
    /// <param name="node">The node to heal connections for.</param>
    public void HealAll(NodeId node) => _network.HealAll(node);
}