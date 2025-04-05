namespace Dot.Raft.Testing.Utilities;

public class DummyStateMachine : IStateMachine
{
    public List<object> AppliedCommands { get; } = [];

    public Task<object> ApplyAsync(object command)
    {
        AppliedCommands.Add(command);
        return Task.FromResult<object>("Ok");
    }
}
