namespace Compiler.Tests;

public static class GenericTest<T>
{
    public static string Identity<Y>()
    {
        return $"{typeof(T).ToString()}\n{typeof(Y).ToString()}";
    }
}
