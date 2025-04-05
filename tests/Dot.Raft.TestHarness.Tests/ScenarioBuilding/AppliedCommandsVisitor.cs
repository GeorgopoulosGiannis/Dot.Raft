using Dot.Raft.Testing.Utilities;
using Shouldly;

namespace Dot.Raft.TestHarness.Tests.ScenarioBuilding;

public class AppliedCommandsVisitor(List<ClientCommandEnvelope> expectedCommands) : IRaftNodeVisitor
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
