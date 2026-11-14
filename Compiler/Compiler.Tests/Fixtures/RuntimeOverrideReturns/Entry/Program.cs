using System;
using RuntimeOverrideReturns;

var contract = ReturnFactory.CreateContract();
_ = new AfterContractOverride();
_ = contract.Describe();

var parent = ReturnFactory.CreateParent();
_ = new AfterParentOverride();
_ = parent.Describe();

ReturnFactory.Consume(UnknownValues.Input!);
ReturnFactory.Consume(new ManagedPayload());
ReturnFactory.Consume("terminal");

ReturnFactory.Inspect<ExplicitConstructorValue>();
ReturnFactory.Inspect<int>();
ReturnFactory.Inspect<FixtureEnum>();
ReturnFactory.Inspect<int?>();
ReturnFactory.Inspect<(int Left, int Right)>();
ReturnFactory.Inspect<DateTime>();
ReturnFactory.Inspect<string>();

IRefContract? refValue = new IncomingRefContract();
ReturnFactory.ReplaceRef(ref refValue);
_ = refValue!.Describe();

IOutContract? localOut = new ReplacedOutContract();
ReturnFactory.CreateOut("local", out localOut);
_ = localOut?.Describe();

CreateThroughArgument(out var argumentOut);
_ = argumentOut?.Describe();

var holder = new OutHolder { Value = new ReplacedOutContract() };
ReturnFactory.CreateOut("field", out holder.Value);
_ = holder.Value?.Describe();

IOutContract?[] array = [new ReplacedOutContract()];
ReturnFactory.CreateOut("array", out array[0]);
_ = array[0]?.Describe();

static void CreateThroughArgument(out IOutContract? value) =>
    ReturnFactory.CreateOut(string.Empty, out value);

internal sealed class AfterContractOverride;

internal sealed class AfterParentOverride;

internal sealed class OutHolder
{
    public IOutContract? Value;
}

internal enum FixtureEnum
{
    None,
}

internal struct ExplicitConstructorValue
{
    public ExplicitConstructorValue()
    {
        Value = 11;
    }

    [FixtureConstructor(ulong.MaxValue, long.MinValue)]
    private ExplicitConstructorValue(int value = 17)
    {
        Value = value;
    }

    public int Value { get; set; }
}

[AttributeUsage(AttributeTargets.Constructor)]
internal sealed class FixtureConstructorAttribute(object unsignedValue, object signedValue) : Attribute
{
    public object UnsignedValue { get; } = unsignedValue;

    public object SignedValue { get; } = signedValue;
}
