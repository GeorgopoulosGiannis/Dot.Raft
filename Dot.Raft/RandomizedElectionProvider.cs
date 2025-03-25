namespace Dot.Raft;

/// <summary>
/// A randomized election timeout provider to avoid split votes.
/// </summary>
public class RandomizedElectionTimeout : IElectionTimeoutProvider
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Provides a randomized tick window between 150-300.
    /// </summary>
    /// <returns></returns>
    public int GetTimeoutTicks() => _random.Next(150, 301);
}