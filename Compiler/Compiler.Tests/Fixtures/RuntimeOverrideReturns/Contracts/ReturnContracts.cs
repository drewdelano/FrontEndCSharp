namespace RuntimeOverrideReturns;

public interface IReturnContract
{
    string Describe();
}

public sealed class ReturnContractImplementation : IReturnContract
{
    public string Describe() => "contract implementation";
}

public class ParentReturn
{
    public virtual string Describe() => "parent";
}

public sealed class ChildReturn : ParentReturn
{
    public override string Describe() => "child";
}

public interface IOutContract
{
    string Describe();
}

public sealed class FirstOutContract : IOutContract
{
    public string Describe() => "first out";
}

public sealed class SecondOutContract : IOutContract
{
    public string Describe() => "second out";
}

public sealed class ReplacedOutContract : IOutContract
{
    public string Describe() => "replaced out";
}

public interface IRefContract
{
    string Describe();
}

public sealed class IncomingRefContract : IRefContract
{
    public string Describe() => "incoming ref";
}

public sealed class WrittenRefContract : IRefContract
{
    public string Describe() => "written ref";
}

public static class ReturnFactory
{
    public static IReturnContract CreateContract() => new ReturnContractImplementation();

    public static ParentReturn CreateParent() => new ParentReturn();

    public static void Consume(object value)
    {
    }

    public static void Inspect<T>()
    {
    }

    public static void InspectString<T>()
    {
    }

    public static bool CreateOut(string selector, out IOutContract? value)
    {
        value = null;
        return false;
    }

    public static void ReplaceRef(ref IRefContract? value)
    {
    }
}

public class ManagedPayload
{
    public string Value => "payload";
}

public sealed class SealedInput
{
    public string Value => "sealed";
}

public static class UnknownValues
{
    public static SealedInput? Input;
}
