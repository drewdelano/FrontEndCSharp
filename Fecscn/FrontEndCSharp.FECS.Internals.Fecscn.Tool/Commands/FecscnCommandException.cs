namespace FrontEndCSharp.Fecscn.Commands;

public sealed class FecscnCommandException : Exception
{
    public FecscnCommandException(string message) : base(message) { }
}
