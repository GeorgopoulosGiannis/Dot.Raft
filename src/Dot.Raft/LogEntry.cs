namespace Dot.Raft;

/// <summary>
/// The log entries kept in the RAFT log.
/// </summary>
/// <param name="Term">The <see cref="Term"/> the command was decided on.</param>
/// <param name="Command">The command.</param>
public record LogEntry(Term Term, object Command);
