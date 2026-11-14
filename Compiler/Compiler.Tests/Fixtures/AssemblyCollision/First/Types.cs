namespace AssemblyCollision.First
{
    public static class Factory
    {
        public static string Read() => new Shared.Model().Read();
    }
}

namespace Shared
{
    internal sealed class Model
    {
        public string Read() => "first";
    }
}
