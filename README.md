# Dot.Raft

Dot.Raft is a modular implementation of the [Raft consensus algorithm](https://raft.github.io/) in .NET, built with testability, clarity, and extensibility in mind.

It aims to provide a maintainable and verifiable foundation for distributed systems that require consensus, while remaining easy to reason about and simulate.

---

## Goals

- Faithfully implement the Raft algorithm as described in the original paper
- Keep the codebase test-driven and minimal
- Provide a reusable IRaftNode interface for external usage
- Allow realistic simulation of clusters using a custom test harness
- Enable verification of cluster behavior under partitions and message delays
- Maintain clean separation between Raft logic and messaging/state management

---

## Structure

The repository is organized into the following projects:

- `Dot.Raft` — the core Raft implementation
- `Dot.Raft.TestHarness` — tools for simulating and testing clusters
- `Dot.Raft.Tests` — unit tests for internal components
- `Dot.Raft.TestHarness.Tests` — high-level integration tests using the test harness
- `Dot.Raft.Testing.Utilities` — common testing utilities shared across test projects

---

## Features Implemented

- Leader election and heartbeats
- Log replication
- Safety guarantees for log consistency
- State machine command application
- Message transport abstraction (`IRaftTransport`)
- Logical time simulation


## Getting Started

To create a Raft node:

```csharp
var node = new RaftNode(
    nodeId: new NodeId(1),
    peers: new List<NodeId> { new(2), new(3) },
    transport: new YourTransport(),
    electionTimer: new YourElectionTimer(),
    heartbeatTimer: new YourHeartbeatTimer(),
    stateMachine: new YourStateMachine()
);

await node.StartAsync();
```
The node exposes methods such as:

`ReceivePeerMessageAsync(...)` — for handling incoming messages

`SubmitCommandAsync(object command)` — to submit a command to the replicated state machine

`Accept(IRaftNodeVisitor)` — for exposing internal state for testing or diagnostics

---

## Testing and Simulation
The test harness allows for simulating clusters, partitions, and logical time:

```csharp
var cluster = ClusterFactory.Create(3);

await cluster.TickUntilLeaderElected();
await cluster.SubmitToLeaderAsync("set x = 42");
await cluster.TickAllAsync(50);
```

A fluent scenario API is also available:

```csharp
await ScenarioBuilder.Build(3)
    .Then(x => x.TickUntilLeaderElected())
    .Then(x => x.SubmitCommand("cmd"))
    .Then(x => x.Tick(50))
    .Then(x => x.AssertAllApplied());
```

Cluster state can be inspected using the visitor pattern:

```csharp
cluster.VisitNodes(new DebugVisitor(output));
```

Or you can write your own visitor:

```csharp
public class StatePrinter : IRaftNodeVisitor
{
    public void Visit<TStateMachine>(
        NodeId id, Term term, RaftRole role, State state, TStateMachine stateMachine)
        where TStateMachine : IStateMachine
    {
        Console.WriteLine($"Node {id.Id} | Role: {role} | Term: {term.Value}");
        Console.WriteLine($"  Log: {string.Join(", ", state.GetLogEntries())}");
        Console.WriteLine($"  CommitIndex: {state.CommitIndex}, LastApplied: {state.LastApplied}");
    }
}
```
---

## Project Structure

```text
src/
├── Dot.Raft                  # Core Raft logic and abstractions
├── Dot.Raft.TestHarness      # Cluster simulation with in-memory transport

tests/
├── Dot.Raft.Tests                  # Unit tests for RaftNode
├── Dot.Raft.TestHarness.Tests      # Cluster behavior tests
├── Dot.Raft.Testing.Utilities      # Shared test helpers (timers, transports, etc.)
```
