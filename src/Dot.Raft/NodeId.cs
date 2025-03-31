namespace Dot.Raft;

/// <summary>
/// The <see cref="IRaftNode"/> identifier.
/// </summary>
/// <param name="Id">The underlying int identifier.</param>
public readonly record struct NodeId(int Id);
