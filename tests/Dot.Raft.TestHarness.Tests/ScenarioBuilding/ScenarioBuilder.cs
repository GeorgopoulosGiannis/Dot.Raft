using Dot.Raft.Testing.Utilities;
using Xunit.Abstractions;

namespace Dot.Raft.TestHarness.Tests.ScenarioBuilding;

/// <summary>
/// A fluent-style builder that simulates Raft cluster scenarios for integration testing.
/// Wraps a <see cref="RaftTestCluster"/> and provides utilities for leader election,
/// command replication, log inspection, and assertions.
/// </summary>
public class ScenarioBuilder
{
    private readonly RaftTestCluster _cluster;
    private readonly List<ClientCommandEnvelope> _submittedCommands = [];
    private readonly List<NodeId> _nodeIds = [];

    public IReadOnlyList<NodeId> NodeIds => _nodeIds;

    private ScenarioBuilder(RaftTestCluster cluster)
    {
        _cluster = cluster;
    }

    /// <summary>
    /// Gets the underlying <see cref="RaftTestCluster"/> used in this scenario.
    /// </summary>
    public RaftTestCluster Cluster => _cluster;

    /// <summary>
    /// Initializes a new <see cref="ScenarioBuilder"/> with a Raft cluster of the given size.
    /// </summary>
    /// <param name="size">The number of nodes to create in the cluster.</param>
    /// <returns>A new <see cref="ScenarioBuilder"/> instance with the initialized cluster.</returns>
    public static ScenarioBuilder Build(int size)
    {
        var cluster = new RaftTestCluster();
        var builder = new ScenarioBuilder(cluster);
        for (var i = 0; i < size; i++)
        {
            var nodeId = new NodeId(i + 1);
            builder._nodeIds.Add(nodeId);
            cluster.Join(new RaftNode(
                new NodeId(i + 1),
                Enumerable.Range(1, size)
                    .Where(n => n != i + 1)
                    .Select(n => new NodeId(n))
                    .ToList(),
                cluster.Transport,
                new LogicalElectionTimer(5 + i),
                new LogicalHeartbeatTimer(2 + i),
                new DummyStateMachine()));
        }

        return builder;
    }

    /// <summary>
    /// Advances the simulation until a single leader is elected, or throws if no leader is elected within the given number of ticks.
    /// </summary>
    /// <param name="maxTicks">The maximum number of ticks to wait for leader election. Defaults to 100.</param>
    /// <returns>The current <see cref="ScenarioBuilder"/> instance for further chaining.</returns>
    public async Task<ScenarioBuilder> TickUntilLeaderElected(int maxTicks = 100)
    {
        await _cluster.TickUntilLeaderElected(maxTicks);
        return this;
    }

    /// <summary>
    /// Submits a command to the current leader in the cluster and tracks it for later validation.
    /// </summary>
    /// <param name="command">The command to submit to the leader.</param>
    /// <returns>The current <see cref="ScenarioBuilder"/> instance for further chaining.</returns>
    public async Task<ScenarioBuilder> SubmitCommand(object command)
    {
        var envelope =
            new ClientCommandEnvelope("scenarioBuilder", _submittedCommands.Count, command);
        await _cluster.SubmitToLeaderAsync(envelope);
        _submittedCommands.Add(envelope);
        return this;
    }

    /// <summary>
    /// Advances the simulation by ticking all nodes the specified number of times.
    /// </summary>
    /// <param name="ticks">The number of ticks to simulate.</param>
    /// <returns>The current <see cref="ScenarioBuilder"/> instance for further chaining.</returns>
    public async Task<ScenarioBuilder> Tick(int ticks)
    {
        await _cluster.TickAllAsync(ticks);
        return this;
    }

    /// <summary>
    /// Asserts that all submitted commands have been applied by every state machine in the cluster.
    /// </summary>
    /// <returns>The current <see cref="ScenarioBuilder"/> instance for further chaining.</returns>
    public ScenarioBuilder AssertAllApplied()
    {
        _cluster.VisitNodes(new AppliedCommandsVisitor(_submittedCommands));
        return this;
    }

    /// <summary>
    /// Prints the current state of the cluster, including logs, commit index, and applied commands.
    /// </summary>
    /// <param name="output">The <see cref="ITestOutputHelper"/> to print to (e.g., xUnit output).</param>
    /// <returns>The current <see cref="ScenarioBuilder"/> instance for further chaining.</returns>
    public ScenarioBuilder PrintClusterState(ITestOutputHelper output)
    {
        _cluster.VisitNodes(new DebugVisitor(output));
        return this;
    }

    /// <summary>
    /// Simulates a network partition between two nodes.
    /// </summary>
    public ScenarioBuilder Partition(NodeId nodeA, NodeId nodeB)
    {
        _cluster.Partition(nodeA, nodeB);
        return this;
    }

    /// <summary>
    /// Heals a previously partitioned connection between two nodes.
    /// </summary>
    public ScenarioBuilder Heal(NodeId nodeA, NodeId nodeB)
    {
        _cluster.Heal(nodeA, nodeB);
        return this;
    }

    /// <summary>
    /// Heals all partitions for a specific node.
    /// </summary>
    public ScenarioBuilder HealAll(NodeId node)
    {
        _cluster.HealAll(node);
        return this;
    }

    /// <summary>
    /// Asserts that exactly one node in the cluster is the leader.
    /// </summary>
    public ScenarioBuilder AssertSingleLeader()
    {
        var leadersCount = 0;
        var visitor = new LinqVisitor(((_, _, role, _, _) =>
        {
            if (role == RaftRole.Leader)
            {
                leadersCount++;
            }
        }));

        _cluster.VisitNodes(visitor);

        if (leadersCount != 1)
        {
            throw new Exception($"Expected exactly 1 leader, found {leadersCount}");
        }

        return this;
    }
}
