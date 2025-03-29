using Shouldly;

namespace Dot.Raft.UnitTests.RaftNodeTests;

public class SubmitCommandTests
{
    private record SentMessage(object Message);

    private class TestTransport : IRaftTransport
    {
        public readonly List<SentMessage> Sent = [];

        public Task SendAsync(NodeId to, object message)
        {
            Sent.Add(new SentMessage(message));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task LeaderAppendsCommandAndSendsToFollowers()
    {
        var state = new State { CurrentTerm = new Term(3) };
        var transport = new TestTransport();
        var peers = new List<NodeId> { new NodeId(2), new NodeId(3) };
        var node = new RaftNode(
            new NodeId(1),
            peers,
            transport,
            state,
            new FixedElectionTimeout(1),
            new DummyStateMachine());

        // become leader
        await node.TickAsync();
        await node.ReceivePeerMessageAsync(peers[0],
            new RequestVoteResponse { Term = new Term(4), VoteGranted = true });
        await node.ReceivePeerMessageAsync(peers[1],
            new RequestVoteResponse { Term = new Term(4), VoteGranted = true });

        transport.Sent.Clear();

        // submit a command
        await node.SubmitCommandAsync("x");

        state.GetCommandAtIndex(state.GetLastLogIndex()).ShouldBe("x");
        transport.Sent.Count.ShouldBe(2);

        foreach (var sent in transport.Sent)
        {
            sent.Message.ShouldBeOfType<AppendEntriesRequest>();
            var request = (AppendEntriesRequest)sent.Message;
            request.Entries.Length.ShouldBe(1);
            request.Entries[0].Command.ShouldBe("x");
            request.PrevLogIndex.ShouldBe(0);
            request.PrevLogTerm.ShouldBe(new Term(4));
        }
    }
}