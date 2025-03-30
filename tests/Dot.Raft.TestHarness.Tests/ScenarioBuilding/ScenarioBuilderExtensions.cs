namespace Dot.Raft.TestHarness.Tests.ScenarioBuilding;

/// <summary>
/// Provides fluent-style chaining extensions for building test Raft scenarios
/// using <see cref="ScenarioBuilder"/>.
/// </summary>
public static class ScenarioBuilderExtensions
{
    /// <summary>
    /// Chains an asynchronous operation to a pending <see cref="ScenarioBuilder"/> task.
    /// </summary>
    /// <param name="builderTask">The ongoing <see cref="Task{ScenarioBuilder}"/>.</param>
    /// <param name="scenarioBuilderFactory">
    /// A function that receives the resolved builder and returns a new <see cref="Task{ScenarioBuilder}"/>.
    /// </param>
    /// <returns>A new <see cref="Task{ScenarioBuilder}"/> representing the continuation.</returns>
    public static async Task<ScenarioBuilder> Then(
        this Task<ScenarioBuilder> builderTask,
        Func<ScenarioBuilder, Task<ScenarioBuilder>> scenarioBuilderFactory)
    {
        var builder = await builderTask;
        return await scenarioBuilderFactory(builder);
    }

    /// <summary>
    /// Chains an asynchronous operation to an already resolved <see cref="ScenarioBuilder"/>.
    /// </summary>
    /// <param name="builder">The resolved builder instance.</param>
    /// <param name="scenarioBuilderFactory">
    /// A function that performs additional scenario steps and returns a new builder.
    /// </param>
    /// <returns>A new <see cref="Task{ScenarioBuilder}"/> representing the continuation.</returns>
    public static async Task<ScenarioBuilder> Then(
        this ScenarioBuilder builder,
        Func<ScenarioBuilder, Task<ScenarioBuilder>> scenarioBuilderFactory)
    {
        return await scenarioBuilderFactory(builder);
    }

    /// <summary>
    /// Chains a synchronous operation to a pending <see cref="ScenarioBuilder"/> task.
    /// </summary>
    /// <param name="builderTask">The ongoing <see cref="Task{ScenarioBuilder}"/>.</param>
    /// <param name="scenarioBuilderFactory">
    /// A function that synchronously returns a modified <see cref="ScenarioBuilder"/>.
    /// </param>
    /// <returns>A new <see cref="ScenarioBuilder"/> instance.</returns>
    public static async Task<ScenarioBuilder> Then(
        this Task<ScenarioBuilder> builderTask,
        Func<ScenarioBuilder, ScenarioBuilder> scenarioBuilderFactory)
    {
        var builder = await builderTask;
        return scenarioBuilderFactory(builder);
    }
}