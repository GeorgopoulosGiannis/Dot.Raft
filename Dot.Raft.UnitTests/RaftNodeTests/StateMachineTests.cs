using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class RaftNodeStateMachineTests
{
    [Fact]
    public async Task AppliesCommittedEntriesToStateMachine()
    {
        var transport = new TestTransport();
        var stateMachine = new DummyStateMachine();
        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 2,
            LastApplied = -1
        };
        state.AddLogEntry(new Term(1), "A");
        state.AddLogEntry(new Term(1), "B");
        state.AddLogEntry(new Term(1), "C");

        var node = new RaftNode(new NodeId(1), new(), transport, state, new FixedElectionTimeout(5), stateMachine);
        await node.TickAsync();

        stateMachine.AppliedCommands.ShouldBe(new[] { "A", "B", "C" });
        state.LastApplied.ShouldBe(2);
    }

    [Fact]
    public async Task AppliesOnlyUpToCommitIndex()
    {
        var stateMachine = new DummyStateMachine();
        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 1,
            LastApplied = -1
        };
        state.AddLogEntry(new Term(1), "1");
        state.AddLogEntry(new Term(1), "2");
        state.AddLogEntry(new Term(1), "3");

        var node = new RaftNode(new NodeId(1), new(), new TestTransport(), state, new FixedElectionTimeout(5),
            stateMachine);
        await node.TickAsync();

        stateMachine.AppliedCommands.ShouldBe(new[] { "1", "2" });
        state.LastApplied.ShouldBe(1);
    }

    [Fact]
    public async Task SkipsAlreadyAppliedEntries()
    {
        var stateMachine = new DummyStateMachine();
        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 2,
            LastApplied = 1
        };
        state.AddLogEntry(new Term(1), "X");
        state.AddLogEntry(new Term(1), "Y");
        state.AddLogEntry(new Term(1), "Z");

        var node = new RaftNode(new NodeId(1), new(), new TestTransport(), state, new FixedElectionTimeout(5),
            stateMachine);
        await node.TickAsync();

        stateMachine.AppliedCommands.ShouldBe(new[] { "Z" });
        state.LastApplied.ShouldBe(2);
    }

    [Fact]
    public async Task DoesNothingIfCommitEqualsLastApplied()
    {
        var stateMachine = new DummyStateMachine();
        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 0,
            LastApplied = 0
        };
        state.AddLogEntry(new Term(1), "Only");

        var node = new RaftNode(new NodeId(1), new(), new TestTransport(), state, new FixedElectionTimeout(5),
            stateMachine);
        await node.TickAsync();

        stateMachine.AppliedCommands.ShouldBeEmpty();
        state.LastApplied.ShouldBe(0);
    }

    [Fact]
    public async Task AppliesEntriesInCorrectOrder()
    {
        var stateMachine = new DummyStateMachine();
        var state = new State
        {
            CurrentTerm = new Term(1),
            CommitIndex = 2,
            LastApplied = -1
        };
        state.AddLogEntry(new Term(1), "first");
        state.AddLogEntry(new Term(1), "second");
        state.AddLogEntry(new Term(1), "third");

        var node = new RaftNode(new NodeId(1), new(), new TestTransport(), state, new FixedElectionTimeout(5),
            stateMachine);
        await node.TickAsync();

        stateMachine.AppliedCommands.ShouldBe(new[] { "first", "second", "third" });
        state.LastApplied.ShouldBe(2);
    }
}