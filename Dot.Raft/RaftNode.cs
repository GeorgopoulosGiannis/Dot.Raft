namespace Dot.Raft;

public class RaftNode
{
    private State State { get; } = new();

    private int _elapsedTicks;
    private int _electionTimeoutTicks;
    private readonly IRaftTransport _transport;
    private readonly IElectionTimeoutProvider _electionTimeoutProvider;
    private readonly IStateMachine _stateMachine;
    private readonly List<NodeId> _peers;

    private HashSet<NodeId> _votesReceived = [];


    /// <summary>
    /// Creates a new <see cref="RaftNode"/> with an explicitly provided state.
    /// </summary>
    /// <remarks>
    /// This constructor should only be used when a specific <see cref="State"/> instance needs to be injected,
    /// such as in testing scenarios or state recovery. In most cases, use the other constructor without the <c>state</c> parameter.
    /// </remarks>
    /// <param name="nodeId">The <see cref="NodeId"/> of the current node.</param>
    /// <param name="peers">The peers of the RAFT cluster.</param>
    /// <param name="transport">The <see cref="IRaftTransport"/> to use for sending messages to peers.</param>
    /// <param name="state">The <see cref="State"/> to provide instead of the default initial.</param>
    /// <param name="electionTimeoutProvider">The <see cref="IElectionTimeoutProvider"/> to use in order to get election timeout ticks.</param>
    /// <param name="stateMachine">The <see cref="IStateMachine"/>That log commands will be applied to.</param>
    public RaftNode(
        NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        State state,
        IElectionTimeoutProvider electionTimeoutProvider,
        IStateMachine stateMachine
    ) : this(nodeId, peers, transport, electionTimeoutProvider, stateMachine)
    {
        State = state;
    }

    /// <summary>
    /// Creates a new <see cref="Raft.RaftNode"/> with a default state.
    /// </summary>
    /// <param name="nodeId">The <see cref="NodeId"/> of the current node.</param>
    /// <param name="peers">The peers of the RAFT cluster.</param>
    /// <param name="transport">The <see cref="IRaftTransport"/> to use for sending messages to peers.</param>
    /// <param name="electionTimeoutProvider">The <see cref="IElectionTimeoutProvider"/> to use in order to get election timeout ticks.</param>
    /// <param name="stateMachine">The <see cref="IStateMachine"/>That log commands will be applied to.</param>
    public RaftNode(NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        IElectionTimeoutProvider electionTimeoutProvider,
        IStateMachine stateMachine)
    {
        _transport = transport;
        _electionTimeoutProvider = electionTimeoutProvider;
        _stateMachine = stateMachine;
        _peers = peers;
        Id = nodeId;
        ResetElectionTimeout();
    }

    private NodeId Id { get; }
    public RaftRole Role { get; private set; } = RaftRole.Follower;

    public async Task TickAsync()
    {
        // Logic for election timeout, heartbeats, etc. will go here.
        _elapsedTicks++;

        if (Role is RaftRole.Follower or RaftRole.Candidate && _elapsedTicks >= _electionTimeoutTicks)
        {
            await StartElectionAsync();
        }

        if (Role is RaftRole.Leader)
        {
            await SendHeartbeatsAsync();
        }

        await ApplyCommitedEntries();
    }

    private async Task StartElectionAsync()
    {
        Role = RaftRole.Candidate;
        State.CurrentTerm++;
        State.VotedFor = Id;
        _elapsedTicks = 0;
        _votesReceived = [Id];

        ResetElectionTimeout();

        var lastLogIndex = State.GetLastLogIndex();
        var lastLogTerm = State.GetLastLogTerm();

        var request = new RequestVote
        {
            Term = State.CurrentTerm,
            CandidateId = Id,
            LastLogIndex = lastLogIndex,
            LastLogTerm = lastLogTerm
        };

        foreach (var peer in _peers)
        {
            await _transport.SendAsync(peer, request);
        }
    }

    /// <summary>
    /// Leader Heartbeat Logic:
    /// For each peer, send an AppendEntriesRequest (heartbeat) with:<br/>
    /// - PrevLogIndex: the index before the entries we're sending (nextIndex[peer] - 1)<br/>
    /// - PrevLogTerm: the term at PrevLogIndex (0 if PrevLogIndex &lt; 0)<br/>
    /// - Entries: empty list (heartbeat only)<br/>
    /// - LeaderCommit: current commit index<br/>
    ///
    /// This allows the follower to:<br/>
    /// - Validate that its log matches up to PrevLogIndex<br/>
    /// - Accept leader authority and update commit index<br/>
    /// </summary>
    private async Task SendHeartbeatsAsync()
    {
        for (var i = 0; i < _peers.Count; i++)
        {
            var peer = _peers[i];
            var nextIndex = State.NextIndexes[i];
            var prevLogIndex = Math.Max(nextIndex - 1, 0);

            var prevLogTerm = State.GetTermAtIndex(prevLogIndex);

            await _transport.SendAsync(peer, new AppendEntriesRequest
            {
                LeaderId = Id,
                Term = State.CurrentTerm,
                PrevLogIndex = prevLogIndex,
                PrevLogTerm = prevLogTerm,
                LeaderCommit = State.CommitIndex,
                Entries = State.GetLogEntries(nextIndex).Select(x => new LogEntry(x.Term, x.Command)).ToArray(),
            });
        }
    }

    private void ResetElectionTimeout()
    {
        _electionTimeoutTicks = _electionTimeoutProvider.GetTimeoutTicks();
    }

    public async Task ReceivePeerMessageAsync(NodeId nodeId, object message)
    {
        var task = message switch
        {
            RequestVote request => ReceiveAsync(request),
            RequestVoteResponse response => ReceiveAsync(nodeId, response),
            AppendEntriesRequest request => ReceiveAsync(request),
            AppendEntriesResponse response => ReceiveAsync(nodeId, response),
            _ => Task.CompletedTask,
        };
        await task;
    }

    /// <summary>
    /// Handles a <see cref="RequestVote"/>.
    /// The receiver will:
    /// 1. Reply false if term &lt; currentTerm
    /// 2. Grant vote,if votedFor is null or candidateId, and candidate's log is at least as up-to-date as receiver's log. 
    /// </summary>
    /// <param name="requestVote"><see cref="RequestVote"/>.</param>
    private async Task ReceiveAsync(RequestVote requestVote)
    {
        if (requestVote.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(requestVote.CandidateId, new RequestVoteResponse
            {
                Term = State.CurrentTerm,
                VoteGranted = false,
            });
            return;
        }

        Role = RaftRole.Follower;
        State.CurrentTerm = requestVote.Term;

        var alreadyVoted = State.VotedFor is not null
                           && State.VotedFor != requestVote.CandidateId;

        var logUpToDate =
            State.IsCandidateLogUpToDate(requestVote.LastLogIndex, requestVote.LastLogTerm);

        var shouldGrantVote = !alreadyVoted && logUpToDate;

        if (shouldGrantVote)
        {
            State.VotedFor = requestVote.CandidateId;
        }

        await _transport.SendAsync(requestVote.CandidateId, new RequestVoteResponse
        {
            Term = State.CurrentTerm,
            VoteGranted = shouldGrantVote,
        });
    }


    /// <summary>
    /// Invoked to handle a <see cref="RequestVoteResponse"/>.
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="response"></param>
    private Task ReceiveAsync(NodeId nodeId, RequestVoteResponse response)
    {
        // response term was greater than anything seen, convert to follower
        if (response.Term > State.CurrentTerm)
        {
            _votesReceived.Clear();
            Role = RaftRole.Follower;
            State.CurrentTerm = response.Term;
            State.VotedFor = null;
            return Task.CompletedTask;
        }

        // If old response ignore
        if (Role != RaftRole.Candidate || response.Term < State.CurrentTerm)
        {
            return Task.CompletedTask;
        }

        if (response.VoteGranted)
        {
            _votesReceived.Add(nodeId);
            var majority = (_peers.Count + 1) / 2 + 1;

            if (_votesReceived.Count >= majority)
            {
                Role = RaftRole.Leader;

                State.NextIndexes.Clear();
                State.MatchIndexes.Clear();

                var nextIndex = Math.Max(State.GetLastLogIndex(), 0);
                for (var i = 0; i < _peers.Count; i++)
                {
                    State.NextIndexes.Add(nextIndex);
                    State.MatchIndexes.Add(-1);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task ReceiveAsync(AppendEntriesRequest request)
    {
        if (request.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
            {
                Term = State.CurrentTerm,
                Success = false,
            });
            return;
        }

        if (request.Term > State.CurrentTerm)
        {
            Role = RaftRole.Follower;
            State.CurrentTerm = request.Term;
            State.VotedFor = null;
        }

        _elapsedTicks = 0;

        // Reply false if log doesn't contain an entry at prevLogIndex
        // whose term matches prevLogTerm
        if (request.PrevLogIndex >= 0 && !State.HasMatchingEntry(request.PrevLogIndex, request.PrevLogTerm))
        {
            await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
            {
                Term = State.CurrentTerm,
                Success = false,
            });
            return;
        }

        // If an existing entry conflicts with a new one (same index
        // but different terms), delete the existing entry and all that
        // follow it
        var index = request.PrevLogIndex + 1;
        var entryIdx = 0;
        var logCount = State.GetCount();

        while (index < logCount && entryIdx < request.Entries.Length)
        {
            if (State.GetTermAtIndex(index) != request.Entries[entryIdx].Term)
            {
                State.RemoveEntriesFrom(index);
                break;
            }

            index++;
            entryIdx++;
        }

        // Append new entries
        for (; entryIdx < request.Entries.Length; entryIdx++)
        {
            var entry = request.Entries[entryIdx];
            State.AddLogEntry(entry.Term, entry.Command);
        }

        // Update commit index
        if (request.LeaderCommit > State.CommitIndex)
        {
            State.CommitIndex = Math.Min(request.LeaderCommit, State.GetLastLogIndex());
            await ApplyCommitedEntries();
        }

        await _transport.SendAsync(request.LeaderId, new AppendEntriesResponse
        {
            Term = State.CurrentTerm,
            Success = true,
        });
    }

    /// <summary>
    /// Handles the response from a <see cref="AppendEntriesRequest"/>.
    /// </summary>
    /// <param name="nodeId">The <see cref="NodeId"/> sending the response.</param>
    /// <param name="response">The <see cref="AppendEntriesResponse"/>.</param>
    private async Task ReceiveAsync(NodeId nodeId, AppendEntriesResponse response)
    {
        if (response.Term > State.CurrentTerm)
        {
            Role = RaftRole.Follower;
            State.CurrentTerm = response.Term;
            State.VotedFor = null;
            _votesReceived.Clear();
            return;
        }

        if (Role != RaftRole.Leader || response.Term < State.CurrentTerm)
        {
            return;
        }

        var peerIndex = _peers.IndexOf(nodeId);
        if (peerIndex == -1)
        {
            return;
        }

        if (response.Success)
        {
            State.MatchIndexes[peerIndex] = State.NextIndexes[peerIndex];
            State.NextIndexes[peerIndex] = State.MatchIndexes[peerIndex] + 1;

            var matchIndexes = State.MatchIndexes.Append(State.GetLastLogIndex()).OrderByDescending(x => x).ToList();
            var majorityIndex = matchIndexes[(matchIndexes.Count - 1) / 2];

            if (majorityIndex > State.CommitIndex && State.GetTermAtIndex(majorityIndex) == State.CurrentTerm)
            {
                State.CommitIndex = majorityIndex;
            }

            await ApplyCommitedEntries();
        }
        else
        {
            // Decrement next index and retry
            State.NextIndexes[peerIndex] = Math.Max(0, State.NextIndexes[peerIndex] - 1);
            // Optionally resend AppendEntries here (can be covered later)
        }
    }

    public async Task SubmitCommandAsync(object command)
    {
        if (Role != RaftRole.Leader)
            return;


        State.AddLogEntry(State.CurrentTerm, command);

        for (var i = 0; i < _peers.Count; i++)
        {
            var peer = _peers[i];
            var nextIndex = State.NextIndexes[i];
            var prevLogIndex = Math.Max(nextIndex - 1, 0);

            var request = new AppendEntriesRequest
            {
                LeaderId = Id,
                Term = State.CurrentTerm,
                PrevLogIndex = prevLogIndex,
                PrevLogTerm = State.GetTermAtIndex(prevLogIndex),
                Entries = State.GetLogEntries(nextIndex).Select(pair => new LogEntry(pair.Term, pair.Command))
                    .ToArray(),
                LeaderCommit = State.CommitIndex
            };

            await _transport.SendAsync(peer, request);
        }
    }

    private async Task ApplyCommitedEntries()
    {
        while (State.LastApplied < State.CommitIndex)
        {
            State.LastApplied++;
            var entry = State.GetCommandAtIndex(State.LastApplied);
            if (entry is not null)
            {
                await _stateMachine.ApplyAsync(entry);
            }
        }
    }
}