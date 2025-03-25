namespace Dot.Raft;

/// <summary>
/// An abstraction over the election timeout provider.
/// </summary>
public interface IElectionTimeoutProvider
{
    /// <summary>
    /// The timeout ticks to wait before starting a new election.
    /// </summary>
    /// <returns></returns>
    int GetTimeoutTicks();
}