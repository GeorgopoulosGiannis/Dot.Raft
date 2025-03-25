namespace Dot.Raft;

public readonly record struct Term(int Value)
{
    public static bool operator >(Term left, Term right)
    {
        return left.Value > right.Value;
    }

    public static bool operator <(Term left, Term right)
    {
        return left.Value < right.Value;
    }

    public static bool operator >=(Term left, Term right)
    {
        return left.Value >= right.Value;
    }

    public static bool operator <=(Term left, Term right)
    {
        return left.Value <= right.Value;
    }

    public static Term operator ++(Term term)
    {
        return new Term(Value: term.Value + 1);
    }
}