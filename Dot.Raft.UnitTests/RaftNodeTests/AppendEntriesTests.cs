using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class AppendEntriesTests
{
    private record SentMessage(NodeId To, object Message);

    private class TestTransport : IRaftTransport
    {
        public List<SentMessage> Sent = new();

        public Task SendAsync<T>(NodeId sendTo, T command)
        {
            Sent.Add(new SentMessage(sendTo, command!));
            return Task.CompletedTask;
        }
    }

    private class FixedElectionTimeout(int ticks) : IElectionTimeoutProvider
    {
        public int GetTimeoutTicks() => ticks;
    }


    [Fact]
    public async Task RejectsAppendEntries_WhenTermIsLessThanCurrent()
    {
        var transport = new TestTransport();

        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            new State { CurrentTerm = new Term(5) },
            new FixedElectionTimeout(100)
        );

        var request = new AppendEntriesRequest
        {
            Term = new Term(4),
            LeaderId = new NodeId(2),
            PrevLogIndex = -1,
            PrevLogTerm = new Term(0),
            Entries = [],
            LeaderCommit = 0
        };

        await node.ReceiveAsync(request);

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response.Success.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));
    }

    [Fact]
    public async Task RejectsAppendEntries_WhenPrevLogTermDoesNotMatch()
    {
        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            new State
            {
                CurrentTerm = new Term(5),
                LogEntries =
                [
                    new LogEntry { Term = new Term(4), Command = "cmd1" } // Index 0
                ]
            },
            new FixedElectionTimeout(100)
        );

        var request = new AppendEntriesRequest
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(3), // mismatch
            Entries = [],
            LeaderCommit = 0
        };
        await node.ReceiveAsync(request);

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response.ShouldNotBeNull();
        response.Success.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));
    }

    [Fact]
    public async Task AcceptsAppendEntries_AndUpdatesTerm_WhenTermIsGreater()
    {
        var transport = new TestTransport();
        var state = new State { CurrentTerm = new Term(2) };
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new FixedElectionTimeout(100)
        );
        var request = new AppendEntriesRequest
        {
            Term = new Term(3),
            LeaderId = new NodeId(2),
            PrevLogIndex = -1,
            PrevLogTerm = new Term(0),
            Entries = [new LogEntry { Term = new Term(3), Command = "set x" }],
            LeaderCommit = 0
        };

        await node.ReceiveAsync(request);

        state.CurrentTerm.ShouldBe(new Term(3));
        state.LogEntries.Count.ShouldBe(1);
        state.LogEntries[0].Command.ShouldBe("set x");

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
            LogEntries =
            [
                new LogEntry { Term = new Term(1), Command = "old1" },
                new LogEntry { Term = new Term(2), Command = "old2" }
            ]
        };

        var transport = new TestTransport();

        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new FixedElectionTimeout(100)
        );

        var request = new AppendEntriesRequest
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(1),
            Entries =
            [
                new LogEntry { Term = new Term(3), Command = "new2" },
                new LogEntry { Term = new Term(3), Command = "new3" }
            ],
            LeaderCommit = 0
        };

        await node.ReceiveAsync(request);

        state.LogEntries.Count.ShouldBe(3);
        state.LogEntries[1].Command.ShouldBe("new2");
        state.LogEntries[2].Command.ShouldBe("new3");

        var response = transport.Sent[0].Message as AppendEntriesResponse;
        response!.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task AppendsNewEntries_WhenLogMatches()
    {
        var state = new State
        {
            CurrentTerm = new Term(5),
            LogEntries =
            [
                new LogEntry { Term = new Term(1), Command = "set x" }
            ]
        };

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new FixedElectionTimeout(100)
        );

        var request = new AppendEntriesRequest
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 0,
            PrevLogTerm = new Term(1),
            Entries = [new LogEntry { Term = new Term(5), Command = "set y" }],
            LeaderCommit = 0
        };

        await node.ReceiveAsync(request);

        state.LogEntries.Count.ShouldBe(2);
        state.LogEntries[1].Command.ShouldBe("set y");
    }

    [Fact]
    public async Task UpdatesCommitIndex_WhenLeaderCommitIsGreater()
    {
        var state = new State
        {
            CurrentTerm = new Term(5),
            LogEntries =
            [
                new LogEntry { Term = new Term(1), Command = "cmd0" },
                new LogEntry { Term = new Term(2), Command = "cmd1" }
            ],
            CommitIndex = 0
        };

        var transport = new TestTransport();
        var node = new RaftNode(
            new NodeId(1),
            [new NodeId(2)],
            transport,
            state,
            new FixedElectionTimeout(100)
        );

        var request = new AppendEntriesRequest
        {
            Term = new Term(5),
            LeaderId = new NodeId(2),
            PrevLogIndex = 1,
            PrevLogTerm = new Term(2),
            Entries = [],
            LeaderCommit = 10
        };

        await node.ReceiveAsync(request);

        state.CommitIndex.ShouldBe(1); // min(10, lastLogIndex = 1)
    }
}