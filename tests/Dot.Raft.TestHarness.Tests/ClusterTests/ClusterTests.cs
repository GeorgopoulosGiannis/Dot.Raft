using Dot.Raft.Testing.Utilities;
using Shouldly;
using Xunit.Abstractions;

namespace Dot.Raft.TestHarness.Tests.ClusterTests;

public class ClusterTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Cluster_ReplicatesAndAppliesCommand()
    {
        var cluster = ClusterFactory.Create(3);

        await cluster.TickUntilLeaderElected();

        var leader = cluster.GetLeader();

        var commands = new List<object>()
        {
            "hello",
        };
        foreach (var command in commands)
        {
            await leader.SubmitCommandAsync(command);
        }


        await cluster.TickAllAsync(100);

        cluster.VisitNodes(new StateMachineVisitor(commands));
        cluster.VisitNodes(new DebugVisitor(output));
    }
}

public class StateMachineVisitor(List<object> expectedCommands) : IRaftNodeVisitor
{
    public void Visit<TStateMachine>(
        NodeId id,
        Term term,
        RaftRole role,
        State state,
        TStateMachine stateMachine)
        where TStateMachine : IStateMachine
    {
        var applied = (stateMachine as DummyStateMachine)?.AppliedCommands;
        applied.ShouldBe(expectedCommands);
    }
}

public class DebugVisitor(ITestOutputHelper output) : IRaftNodeVisitor
{
    public void Visit<TStateMachine>(NodeId id, Term term, RaftRole role, State state, TStateMachine sm)
        where TStateMachine : IStateMachine
    {
        var logStr = state
            .GetLogEntries()
            .Select((e, i) => $"[{i}:{e.Term.Value}]{e.Command}").ToList();

        var log = logStr.Count > 0 ? string.Join(", ", logStr) : "(empty)";

        output.WriteLine($"Node Id: {id.Id} | Term: {term.Value} | Role: {role}");
        output.WriteLine($"  Log: {log}");
        output.WriteLine($"  CommitIndex: {state.CommitIndex}, LastApplied: {state.LastApplied}");

        if (sm is DummyStateMachine dsm)
        {
            output.WriteLine($"  Applied: {string.Join(", ", dsm.AppliedCommands)}");
        }

        output.WriteLine("");
    }
}