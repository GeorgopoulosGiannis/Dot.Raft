# Dot.Raft

**⚠️ Work in Progress**  
Dot.Raft is a modular and testable implementation of the Raft consensus algorithm in .NET. It is currently under development and **not yet production-ready**. APIs may change as the library evolves, and no complete example in a real application exists yet—aside from a test harness used for simulations.

---

## Overview

Dot.Raft is designed to be:

- Faithful to the [original Raft paper](https://raft.github.io/)
- Easy to simulate, reason about, and test
- Separated cleanly between Raft core logic, transport, and state machine concerns
- Useful as a foundation for building distributed systems in .NET

---

## Goals

- ✅ Implement the Raft algorithm correctly and clearly
- ✅ Support testing and simulation of network partitions, delays, and leadership changes
- ✅ Provide a reusable `IRaftNode` abstraction for integration
- ⚠️ Real-world transport/state machine integrations and examples are *planned*
- ⚠️ Public API is *subject to change* as the library matures

---

## Key Features

- **Leader election** and **heartbeats**
- **Log replication** with consistency guarantees
- **Safety** via Raft's log matching properties
- **State machine application** of committed commands
- **Pluggable transport** via `IRaftTransport`
- **Logical time simulation** for testing scenarios

---

## Getting Started

Create a Raft node:

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

Basic API:
```csharp
await node.ReceivePeerMessageAsync(message);      // Handle incoming message
await node.SubmitCommandAsync(new MyCommand());   // Submit command to leader
node.Accept(new MyDiagnosticVisitor());           // Inspect internal state
```


## Simulation & Testing

You can simulate a Raft cluster and control logical time:

```csharp
var cluster = ClusterFactory.Create(3);
await cluster.TickUntilLeaderElected();
await cluster.SubmitToLeaderAsync("set x = 42");
await cluster.TickAllAsync(50);
```

or use the fluent api of the scenario builder

```csharp
await ScenarioBuilder.Build(3)
    .Then(x => x.TickUntilLeaderElected())
    .Then(x => x.SubmitCommand("cmd"))
    .Then(x => x.Tick(50))
    .Then(x => x.AssertAllApplied());
```


### Observability via Visitors

Use a built-in visitor:

```csharp
cluster.VisitNodes(new DebugVisitor(Console.Out));
```

Or create a custom one:

```csharp
public class StatePrinter : IRaftNodeVisitor
{
    public void Visit<TStateMachine>(NodeId id, Term term, RaftRole role, State state, TStateMachine stateMachine)
        where TStateMachine : IStateMachine
    {
        Console.WriteLine($"Node {id.Id} | Role: {role} | Term: {term.Value}");
        Console.WriteLine($"  Log: {string.Join(", ", state.GetLogEntries())}");
        Console.WriteLine($"  CommitIndex: {state.CommitIndex}, LastApplied: {state.LastApplied}");
    }
}
```

## Status & Roadmap

| Feature                                    | Status        |
|--------------------------------------------|---------------|
| Leader election & heartbeats               | ✅ Implemented |
| Log replication                            | ✅ Implemented |
| Cluster test harness                       | ✅ Implemented |
| State machine integration                  | ✅ Basic       |
| Public API stability                       | ⚠️ Changing    |
| Real-world usage example                   | ❌ Not yet     |
| Snapshotting & log compaction              | 🚧 Planned     |
| Raft membership changes (joint consensus)  | 🚧 Planned     |


## Contributing

Contributions are welcome!
This project is evolving and feedback, use cases, and test cases are incredibly valuable at this stage.

## License

MIT License





