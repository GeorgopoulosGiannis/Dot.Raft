namespace Dot.Raft;

/// <summary>
/// Represents a term in the Raft consensus algorithm.
/// Terms are monotonically increasing integers that help determine leadership and consistency.
/// </summary>
/// <param name="Value">The numeric value of the term.</param>
public readonly record struct Term(int Value)
{
    /// <summary>
    /// Determines whether the left term is greater than the right term.
    /// </summary>
    /// <param name="left">The left-hand <see cref="Term"/>.</param>
    /// <param name="right">The right-hand <see cref="Term"/>.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >(Term left, Term right)
    {
        return left.Value > right.Value;
    }

    /// <summary>
    /// Determines whether the left term is less than the right term.
    /// </summary>
    /// <param name="left">The left-hand <see cref="Term"/>.</param>
    /// <param name="right">The right-hand <see cref="Term"/>.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <(Term left, Term right)
    {
        return left.Value < right.Value;
    }

    /// <summary>
    /// Determines whether the left term is greater than or equal to the right term.
    /// </summary>
    /// <param name="left">The left-hand <see cref="Term"/>.</param>
    /// <param name="right">The right-hand <see cref="Term"/>.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator >=(Term left, Term right)
    {
        return left.Value >= right.Value;
    }

    /// <summary>
    /// Determines whether the left term is less than or equal to the right term.
    /// </summary>
    /// <param name="left">The left-hand <see cref="Term"/>.</param>
    /// <param name="right">The right-hand <see cref="Term"/>.</param>
    /// <returns><c>true</c> if <paramref name="left"/> is less than or equal to <paramref name="right"/>; otherwise, <c>false</c>.</returns>
    public static bool operator <=(Term left, Term right)
    {
        return left.Value <= right.Value;
    }

    /// <summary>
    /// Increments the term by 1.
    /// </summary>
    /// <param name="term">The term to increment.</param>
    /// <returns>A new <see cref="Term"/> with its value increased by 1.</returns>
    public static Term operator ++(Term term)
    {
        return new Term(Value: term.Value + 1);
    }
}
