namespace Dot.Raft;

public record LogEntry(Term Term, object Command);