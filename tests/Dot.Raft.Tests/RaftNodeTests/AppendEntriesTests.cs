using Dot.Raft.Testing.Utilities;
using Shouldly;

namespace Dot.Raft.Tests.RaftNodeTests;

public class AppendEntriesTests
{
    [Fact]
    public async Task RejectsAppendEntries_WhenTermIsLessThanCurrent()
    {
        var state = new State { CurrentTerm = new Term(5) };
        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(4),
            LeaderId = new NodeId(2),
            PrevLogIndex = -1,
            PrevLogTerm = new Term(0),
            Entries = [],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response.Success.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));
    }

    [Fact]
    public async Task RejectsAppendEntries_WhenPrevLogTermDoesNotMatch()
    {
        var state = new State
        {
            CurrentTerm = new Term(5),
        };
        state.AddLogEntry(new Term(4), "cmd1");

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(3), // mismatch
            Entries = [],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response.Success.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));
    }

    [Fact]
    public async Task AcceptsAppendEntries_AndUpdatesTerm_WhenTermIsGreater()
    {
        var state = new State { CurrentTerm = new Term(2) };
        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(3),
            LeaderId = new NodeId(2),
            PrevLogIndex = -1,
            PrevLogTerm = new Term(0),
            Entries = [new LogEntry(new Term(3), "set x")],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        state.CurrentTerm.ShouldBe(new Term(3));
        state.GetCount().ShouldBe(1);
        state.GetCommandAtIndex(0).ShouldBe("set x");

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task DeletesConflictingEntries_AndAppendsNewOnes()
    {
        var state = new State
        {
            CurrentTerm = new Term(5),
        };
        state.AddLogEntry(new Term(1), "old1");
        state.AddLogEntry(new Term(2), "old2");

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());


        await node.ReceivePeerMessageAsync(new AppendEntries
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(1),
            Entries =
            [
                new LogEntry(new Term(3), "new2"),
                new LogEntry(new Term(3), "new3")
            ],
            LeaderCommit = 0
        });

        state.GetCount().ShouldBe(3);
        state.GetCommandAtIndex(1).ShouldBe("new2");
        state.GetCommandAtIndex(2).ShouldBe("new3");

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AppendsNewEntries_WhenLogMatches()
    {
        var state = new State();
        state.AddLogEntry(new Term(1), "set x");
        state.CurrentTerm = new Term(5);

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(1),
            Entries = [new LogEntry(new Term(5), "set y")],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        state.GetCount().ShouldBe(2);
        state.GetCommandAtIndex(1).ShouldBe("set y");
    }

    [Fact]
    public async Task UpdatesCommitIndex_WhenLeaderCommitIsGreater()
    {
        var state = new State
        {
            CurrentTerm = new Term(5),
            CommitIndex = 0
        };
        state.AddLogEntry(new Term(1), "cmd0");
        state.AddLogEntry(new Term(2), "cmd1");

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 1,
            PrevLogTerm = new Term(2),
            Entries = [],
            LeaderCommit = 10
        };

        await node.ReceivePeerMessageAsync(request);

        state.CommitIndex.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotUpdateCommitIndex_WhenLeaderCommitIsZero()
    {
        var transport = new TestTransport();
        var state = new State
        {
            CurrentTerm = new Term(3),
            CommitIndex = 1,
        };
        state.AddLogEntry(new Term(1), "cmd0");
        state.AddLogEntry(new Term(2), "cmd1");


        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(3),
            LeaderId = new NodeId(2),
            PrevLogIndex = 1,
            PrevLogTerm = new Term(2),
            Entries = [],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        state.CommitIndex.ShouldBe(1);
    }

    [Fact]
    public async Task AcceptsFirstEntry_WhenPrevLogIndexIsMinusOneAndLogIsEmpty()
    {
        var transport = new TestTransport();
        var state = new State
        {
            CurrentTerm = new Term(1),
        };

        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(1),
            LeaderId = new NodeId(2),
            PrevLogIndex = -1,
            PrevLogTerm = new Term(0),
            Entries = [new LogEntry(new Term(1), "init")],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        state.GetCount().ShouldBe(1);
        state.GetCommandAtIndex(0).ShouldBe("init");
    }


    [Fact]
    public async Task RejectsAppendEntries_WhenPrevLogIndexIsBeyondLogLength()
    {
        var state = new State
        {
            CurrentTerm = new Term(3),
        };
        state.AddLogEntry(new Term(1), "a");

        var transport = new TestTransport();

        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(3),
            LeaderId = new NodeId(2),
            PrevLogIndex = 2, // log only has 1 entry
            PrevLogTerm = new Term(1),
            Entries = [],
            LeaderCommit = 0
        };

        await node.ReceivePeerMessageAsync(request);

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response!.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task FollowerAppliesCommittedEntries_WhenLeaderCommitAdvances()
    {
        var state = new State
        {
            CurrentTerm = new Term(3),
            CommitIndex = 0,
            LastApplied = 0
        };
        state.AddLogEntry(new Term(1), "a");
        state.AddLogEntry(new Term(2), "b");
        state.AddLogEntry(new Term(3), "c");

        var transport = new TestTransport();

        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new LogicalElectionTimer(100),
            new LogicalHeartbeatTimer(10),
            new DummyStateMachine());

        var request = new AppendEntries
        {
            Term = new Term(3),
            LeaderId = new NodeId(99),
            PrevLogIndex = 2,
            PrevLogTerm = new Term(3),
            Entries = [],
            LeaderCommit = 2
        };

        await node.ReceivePeerMessageAsync(request);

        state.CommitIndex.ShouldBe(2);
        state.LastApplied.ShouldBe(2);
    }
}