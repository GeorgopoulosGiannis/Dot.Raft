namespace Dot.Raft.UnitTests.RaftNodeTests;

public class DummyStateMachine : IStateMachine
{
    public List<object> AppliedCommands { get; } = [];

    public Task ApplyAsync(object command)
    {
        AppliedCommands.Add(command);
        return Task.CompletedTask;
    }
}