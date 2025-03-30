using Dot.Raft.Testing.Utilities;
using Xunit.Abstractions;

namespace Dot.Raft.TestHarness;

public class DebugVisitor(ITestOutputHelper output) : IRaftNodeVisitor
{
    public void Visit<TStateMachine>(NodeId id, Term term, RaftRole role, State state, TStateMachine sm)
        where TStateMachine : IStateMachine
    {
        var logStr = state
            .GetLogEntries()
            .Select((e, i) => $"[{i}:{e.Term.Value}]{e.Command}").ToList();

        var log = logStr.Count > 0 ? string.Join(", ", logStr) : "(empty)";

        output.WriteLine($"Node Id: {id.Id} | Term: {term.Value} | Role: {role}");
        output.WriteLine($"  Log: {log}");
        output.WriteLine($"  CommitIndex: {state.CommitIndex}, LastApplied: {state.LastApplied}");

        if (sm is DummyStateMachine dsm)
        {
            output.WriteLine($"  Applied: {string.Join(", ", dsm.AppliedCommands)}");
        }

        output.WriteLine("");
    }
}