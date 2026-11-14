namespace EditAndContinueFixture;

public static class Program
{
    public static void Main()
    {
        Func<int> getter = GetValue;
        Console.WriteLine(getter());
    }

    public static int GetValue()
    {
        var result = 1;
        return result;
    }

    public static int Closure(int value)
    {
        Func<int> getValue = () => value;
        return getValue();
    }
}
