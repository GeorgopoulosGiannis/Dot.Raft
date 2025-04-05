using System.Collections.Concurrent;

namespace Dot.Raft;

/// <inheritdoc/>
public class RaftNode : IRaftNode
{
    private readonly IRaftTransport _transport;

    private readonly IElectionTimer _electionTimer;
    private readonly IHeartbeatTimer _heartbeatTimer;

    private readonly List<NodeId> _peers;

    private readonly
        ConcurrentDictionary<(string clientId, int sequenceNum),
            TaskCompletionSource<object?>> _pendingCommands = new();

    private HashSet<NodeId> _votesReceived = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="RaftNode"/> class with an explicitly provided state.
    /// </summary>
    /// <remarks>
    /// This constructor should only be used when a specific <see cref="State"/> instance needs to be injected,
    /// such as in testing scenarios or state recovery. In most cases, use the other constructor without the <c>state</c> parameter.
    /// </remarks>
    /// <param name="nodeId">The <see cref="NodeId"/> of the current node.</param>
    /// <param name="peers">The peers of the RAFT cluster.</param>
    /// <param name="transport">The <see cref="IRaftTransport"/> to use for sending messages to peers.</param>
    /// <param name="state">The <see cref="State"/> to provide instead of the default initial.</param>
    /// <param name="electionTimer">The <see cref="IElectionTimer"/> to use in order to determine election timeouts.</param>
    /// <param name="heartbeatTimer">The <see cref="IHeartbeatTimer"/> to use in order to trigger heartbeat broadcasts.</param>
    /// <param name="stateMachine">The <see cref="IStateMachine"/>That log commands will be applied to.</param>
    public RaftNode(
        NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        State state,
        IElectionTimer electionTimer,
        IHeartbeatTimer heartbeatTimer,
        IStateMachine stateMachine)
        : this(
            nodeId,
            peers,
            transport,
            electionTimer,
            heartbeatTimer,
            stateMachine)
    {
        State = state;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RaftNode"/> class.
    /// </summary>
    /// <param name="nodeId">The <see cref="NodeId"/> of the current node.</param>
    /// <param name="peers">The peers of the RAFT cluster.</param>
    /// <param name="transport">The <see cref="IRaftTransport"/> to use for sending messages to peers.</param>
    /// <param name="electionTimer">The <see cref="IElectionTimer"/> to use in order to determine election timeouts.</param>
    /// <param name="heartbeatTimer">The <see cref="IHeartbeatTimer"/> to use in order to trigger heartbeat broadcasts.</param>
    /// <param name="stateMachine">The <see cref="IStateMachine"/>That log commands will be applied to.</param>
    public RaftNode(
        NodeId nodeId,
        List<NodeId> peers,
        IRaftTransport transport,
        IElectionTimer electionTimer,
        IHeartbeatTimer heartbeatTimer,
        IStateMachine stateMachine)
    {
        _transport = transport;
        _electionTimer = electionTimer;
        _heartbeatTimer = heartbeatTimer;
        StateMachine = stateMachine;
        _peers = peers;
        Id = nodeId;
        _electionTimer.Reset();
    }

    /// <inheritdoc />
    public NodeId Id { get; }

    /// <inheritdoc />
    public Term CurrentTerm => State.CurrentTerm;

    /// <inheritdoc />
    public IStateMachine StateMachine { get; }

    /// <inheritdoc />
    public RaftRole Role { get; private set; } = RaftRole.Follower;

    private State State { get; } = new();

    /// <inheritdoc />
    public async Task TickAsync()
    {
        if (Role is RaftRole.Follower or RaftRole.Candidate && _electionTimer.ShouldTriggerElection())
        {
            await StartElectionAsync().ConfigureAwait(false);
        }

        if (Role is RaftRole.Leader && _heartbeatTimer.ShouldSendHeartbeat())
        {
            await BroadcastHeartbeatAsync().ConfigureAwait(false);
        }

        await ApplyCommitedEntries().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReceivePeerMessageAsync(object message)
    {
        var task = message switch
        {
            RequestVote request => ReceiveAsync(request),
            RequestVoteResponse response => ReceiveAsync(response),
            AppendEntries request => ReceiveAsync(request),
            AppendEntriesResponse response => ReceiveAsync(response),
            _ => throw new ArgumentOutOfRangeException(nameof(message), message, "Unknown message type"),
        };
        await task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<object?> SubmitCommandAsync(ClientCommandEnvelope command)
    {
        if (Role != RaftRole.Leader)
        {
            throw new InvalidOperationException("Only leader can accept commands.");
        }

        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _pendingCommands[(command.ClientId, command.SequenceNumber)] = tcs;

        State.AddLogEntry(State.CurrentTerm, command);

        await BroadcastHeartbeatAsync().ConfigureAwait(false);
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Accepts a visitor that can inspect or interact with this Raft node's internal state.
    /// </summary>
    /// <param name="visitor">The visitor that will access this node's state.</param>
    public void Accept(IRaftNodeVisitor visitor)
    {
        visitor.Visit(Id, State.CurrentTerm, Role, State, StateMachine);
    }

    /// <summary>
    /// Broadcasts <see cref="RequestVote"/> requests and
    /// sets the state for a <see cref="RaftRole.Candidate"/> node.
    /// </summary>
    private async Task StartElectionAsync()
    {
        Role = RaftRole.Candidate;
        State.CurrentTerm++;
        State.VotedFor = Id;
        _votesReceived = [Id];

        _electionTimer.Reset();

        var lastLogIndex = State.GetLastLogIndex();
        var lastLogTerm = State.GetLastLogTerm();

        var request = new RequestVote
        {
            CandidateId = Id, Term = State.CurrentTerm, LastLogIndex = lastLogIndex, LastLogTerm = lastLogTerm,
        };

        await Task.WhenAll(
            _peers.Select(peer => _transport.SendAsync(peer, request))).ConfigureAwait(false);
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
    /// - Accept leader authority and update commit index.<br/>
    /// </summary>
    private async Task BroadcastHeartbeatAsync()
    {
        _heartbeatTimer.Reset();

        var heartbeatTasks = _peers
            .Select(SendAppendEntriesAsync)
            .ToList();

        await Task.WhenAll(heartbeatTasks).ConfigureAwait(false);
    }

    private async Task SendAppendEntriesAsync(NodeId nodeId)
    {
        var peerIndex = _peers.IndexOf(nodeId);
        var nextIndex = State.NextIndexes[peerIndex];
        var prevLogIndex = nextIndex - 1;
        var prevLogTerm = State.GetTermAtIndex(prevLogIndex);

        var entries = State
            .GetLogEntries(nextIndex)
            .Select(x => new AppendEntries.LogEntry(x.Term, x.Command))
            .ToArray();

        var request = new AppendEntries
        {
            LeaderId = Id,
            Term = State.CurrentTerm,
            PrevLogIndex = prevLogIndex,
            PrevLogTerm = prevLogTerm,
            LeaderCommit = State.CommitIndex,
            Entries = entries,
        };

        await _transport.SendAsync(nodeId, request).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles a <see cref="RequestVote"/>.
    /// The receiver will:
    /// 1. Reply false if term &lt; currentTerm
    /// 2. Grant vote, if votedFor is null or candidateId, and candidate's log is at least as up-to-date as receiver's log.
    /// </summary>
    /// <param name="requestVote"><see cref="RequestVote"/>.</param>
    private async Task ReceiveAsync(RequestVote requestVote)
    {
        if (requestVote.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(
                    requestVote.CandidateId,
                    new RequestVoteResponse { ReplierId = Id, Term = State.CurrentTerm, VoteGranted = false, })
                .ConfigureAwait(false);
            return;
        }

        Role = RaftRole.Follower;
        State.CurrentTerm = requestVote.Term;
        _electionTimer.Reset();

        var alreadyVoted = State.VotedFor is not null
                           && State.VotedFor != requestVote.CandidateId;

        var logUpToDate =
            State.IsCandidateLogUpToDate(requestVote.LastLogIndex, requestVote.LastLogTerm);

        var shouldGrantVote = !alreadyVoted && logUpToDate;

        if (shouldGrantVote)
        {
            State.VotedFor = requestVote.CandidateId;
        }

        await _transport.SendAsync(
                requestVote.CandidateId,
                new RequestVoteResponse { ReplierId = Id, Term = State.CurrentTerm, VoteGranted = shouldGrantVote, })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Invoked to handle a <see cref="RequestVoteResponse"/>.
    /// </summary>
    /// <param name="response">The <see cref="RequestVoteResponse"/> received.</param>
    private Task ReceiveAsync(RequestVoteResponse response)
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

        if (!response.VoteGranted)
        {
            return Task.CompletedTask;
        }

        _votesReceived.Add(response.ReplierId);
        int majority = ((_peers.Count + 1) / 2) + 1;

        if (_votesReceived.Count < majority)
        {
            return Task.CompletedTask;
        }

        Role = RaftRole.Leader;
        State.NextIndexes.Clear();
        State.MatchIndexes.Clear();

        var nextIndex = State.GetLastLogIndex() + 1;
        for (var i = 0; i < _peers.Count; i++)
        {
            State.NextIndexes.Add(nextIndex);
            State.MatchIndexes.Add(-1);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the <see cref="AppendEntries"/> request.
    /// </summary>
    /// <param name="request">The <see cref="AppendEntries"/> request.</param>
    private async Task ReceiveAsync(AppendEntries request)
    {
        if (request.Term < State.CurrentTerm)
        {
            await _transport.SendAsync(
                    request.LeaderId,
                    new AppendEntriesResponse { ReplierId = Id, Term = State.CurrentTerm, Success = false, })
                .ConfigureAwait(false);
            return;
        }

        if (request.Term > State.CurrentTerm)
        {
            Role = RaftRole.Follower;
            State.CurrentTerm = request.Term;
            State.VotedFor = null;
        }

        _electionTimer.Reset();

        // Reply false if log doesn't contain an entry at prevLogIndex
        // whose term matches prevLogTerm
        if (request.PrevLogIndex >= 0 && !State.HasMatchingEntry(request.PrevLogIndex, request.PrevLogTerm))
        {
            await _transport.SendAsync(
                    request.LeaderId,
                    new AppendEntriesResponse { ReplierId = Id, Term = State.CurrentTerm, Success = false, })
                .ConfigureAwait(false);
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
            if (index < State.GetCount())
            {
                // Already exists — skip if terms match
                if (State.GetTermAtIndex(index) == request.Entries[entryIdx].Term)
                {
                    continue;
                }

                // Conflict: truncate and break
                State.RemoveEntriesFrom(index);
            }

            index++;
            State.AddLogEntry(entry.Term, entry.Command);
        }

        // Update commit index
        if (request.LeaderCommit > State.CommitIndex)
        {
            State.CommitIndex = Math.Max(Math.Min(request.LeaderCommit, State.GetLastLogIndex()), 0);
            await ApplyCommitedEntries().ConfigureAwait(false);
        }

        await _transport.SendAsync(
                request.LeaderId,
                new AppendEntriesResponse { ReplierId = Id, Term = State.CurrentTerm, Success = true, })
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the response from a <see cref="AppendEntries"/>.
    /// </summary>
    /// <param name="response">The <see cref="AppendEntriesResponse"/>.</param>
    private async Task ReceiveAsync(AppendEntriesResponse response)
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

        var peerIndex = _peers.IndexOf(response.ReplierId);
        if (peerIndex == -1)
        {
            return;
        }

        if (response.Success)
        {
            if (State.NextIndexes[peerIndex] <= State.GetLastLogIndex())
            {
                // safe to assume entries were appended
                State.MatchIndexes[peerIndex] = State.NextIndexes[peerIndex];
                State.NextIndexes[peerIndex] = State.MatchIndexes[peerIndex] + 1;
            }

            // Recalculate commit index regardless — heartbeat success still confirms logs are consistent
            var matchIndexes = State.MatchIndexes.Append(State.GetLastLogIndex()).OrderByDescending(x => x).ToList();
            var majorityIndex = matchIndexes[(matchIndexes.Count - 1) / 2];

            if (majorityIndex > State.CommitIndex && State.GetTermAtIndex(majorityIndex) == State.CurrentTerm)
            {
                State.CommitIndex = majorityIndex;
            }

            await ApplyCommitedEntries().ConfigureAwait(false);
        }
        else
        {
            State.NextIndexes[peerIndex] = Math.Max(0, State.NextIndexes[peerIndex] - 1);

            var nextIndex = State.NextIndexes[peerIndex];
            var prevLogIndex = Math.Max(nextIndex - 1, 0);
            var prevLogTerm = State.GetTermAtIndex(prevLogIndex);

            await _transport.SendAsync(response.ReplierId, new AppendEntries
            {
                LeaderId = Id,
                Term = State.CurrentTerm,
                PrevLogIndex = prevLogIndex,
                PrevLogTerm = prevLogTerm,
                Entries = State
                    .GetLogEntries(nextIndex)
                    .Select(entry => new AppendEntries.LogEntry(entry.Term, entry.Command))
                    .ToArray(),
                LeaderCommit = State.CommitIndex,
            }).ConfigureAwait(false);
        }
    }

    private async Task ApplyCommitedEntries()
    {
        while (State.LastApplied < State.CommitIndex)
        {
            State.LastApplied++;
            var entry = State.GetCommandAtIndex(State.LastApplied);
            if (entry is ClientCommandEnvelope command)
            {
                var result = await StateMachine.ApplyAsync(entry).ConfigureAwait(false);
                var key = (command.ClientId, command.SequenceNumber);
                if (_pendingCommands.TryRemove(key, out var tcs))
                {
                    tcs.TrySetResult(result);
                }
            }
            else if (entry is not null)
            {
                await StateMachine.ApplyAsync(entry).ConfigureAwait(false);
            }
        }
    }
}
