namespace FrontEndCSharp.Fecsx.Syntax;

public readonly struct FecsxTextSpan : IEquatable<FecsxTextSpan>
{
    public FecsxTextSpan(int start, int length)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        Start = start;
        Length = length;
    }

    public int Start { get; }

    public int Length { get; }

    public int End => Start + Length;

    public bool Equals(FecsxTextSpan other) => Start == other.Start && Length == other.Length;

    public override bool Equals(object? obj) => obj is FecsxTextSpan other && Equals(other);

    public override int GetHashCode() => (Start * 397) ^ Length;

    public override string ToString() => $"[{Start}..{End})";
}
