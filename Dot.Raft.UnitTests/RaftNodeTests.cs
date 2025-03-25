using Shouldly;

namespace Dot.Raft.UnitTests;

public class RaftNodeTests
{
    private class FakeTransport : IRaftTransport
    {
        public NodeId? SendTo { get; private set; }
        public object? Command { get; private set; }

        public Task SendAsync<T>(NodeId sendTo, T command)
        {
            SendTo = sendTo;
            Command = command;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsGreaterAndLogIsUpToDate()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(nodeId, [candidateId], transport);

        var request = new RequestVoteRequest
        {
            Term = new Term(1),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        transport.SendTo.ShouldBe(candidateId);
        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(1));
    }

    [Fact]
    public async Task GrantsVote_WhenCandidateTermIsHigher_AndLogUpToDate_AndFirstVote()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var state = new State { CurrentTerm = new Term(2) };
        var node = new RaftNode(nodeId, [candidateId], transport, state);

        var request = new RequestVoteRequest
        {
            Term = new Term(3),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeTrue();
        response.Term.ShouldBe(new Term(3));
        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenCandidateTermIsLessThanCurrentTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var transport = new FakeTransport();
        var node = new RaftNode(nodeId, [candidateId], transport, new State { CurrentTerm = new Term(5) });


        var request = new RequestVoteRequest
        {
            Term = new Term(4),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }

    [Fact]
    public async Task RejectsVote_WhenAlreadyVotedForAnotherCandidateInSameTerm()
    {
        var nodeId = new NodeId(1);
        var candidateId = new NodeId(2);
        var alreadyVotedFor = new NodeId(3);
        var transport = new FakeTransport();
        var node = new RaftNode(
            nodeId,
            [candidateId],
            transport,
            new State
            {
                CurrentTerm = new Term(5),
                VotedFor = alreadyVotedFor
            });

        var request = new RequestVoteRequest
        {
            Term = new Term(5),
            CandidateId = candidateId,
            LastLogIndex = 0,
            LastLogTerm = new Term(0)
        };

        await node.ReceiveAsync(request);

        var response = transport.Command as RequestVoteResponse;
        response.ShouldNotBeNull();
        response.VoteGranted.ShouldBeFalse();
        response.Term.ShouldBe(new Term(5));

        transport.SendTo.ShouldBe(candidateId);
    }
}