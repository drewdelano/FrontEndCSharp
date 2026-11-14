public class ComplexType
{
    public int One { get; set; } = 1;
    public int Two { get; set; } = 2;
}

public class Program
{
    public static async Task Main(string[] args)
    {
        var a = 2;
        var b = 3;
        var c = a + b;
        var hello = "world";
        var complex = new ComplexType();
        NestedCaller(c);
        Console.WriteLine("Hello");
        await NestedAsync();
        Console.WriteLine("World");


    }

    private static int NestedCaller(int value)
    {
        var callerValue = value + 1;
        return NestedLeaf(callerValue);
    }

    private static int NestedLeaf(int value)
    {
        var leafValue = value + 1;
        Console.WriteLine(leafValue);
        return leafValue;
    }

    private static async Task NestedAsync()
    {
        var asyncValue = 42;
        await Task.Delay(2000);
        Console.WriteLine(asyncValue);
    }
}
