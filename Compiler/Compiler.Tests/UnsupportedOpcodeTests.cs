using Compiler.Read;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;

namespace Compiler.Tests;

public class UnsupportedOpcodeTests
{
    private static int _staticValue;

    public static TestCaseDataCollection<string, string> AddressDependentMethods => new()
    {
        { nameof(LoadStaticFieldAddress), "ldsflda" },
        { nameof(LoadNative), "ldind.i" },
        { nameof(LoadSingle), "ldind.r4" },
        { nameof(LoadDouble), "ldind.r8" },
        { nameof(StoreNative), "stind.i" },
        { nameof(StoreSingle), "stind.r4" },
        { nameof(StoreDouble), "stind.r8" },
        { nameof(MakeTypedReference), "mkrefany" },
        { nameof(GetReferencedType), "refanytype" },
        { nameof(GetReferencedValue), "refanyval" }
    };

    public static TestCaseDataCollection<string> ReservedPrefixFields => new()
    {
        "Prefix1",
        "Prefix2",
        "Prefix3",
        "Prefix4",
        "Prefix5",
        "Prefix6",
        "Prefix7",
        "Prefixref"
    };

    public static TestCaseDataCollection<string> IncompleteOpcodeFields => new()
    {
        "Localloc",
        "Cpobj",
        "Cpblk",
        "Initblk",
        "Unaligned",
        "Tailcall",
        "Readonly"
    };

    [TestCaseSource(nameof(AddressDependentMethods))]
    public void AddressDependentAndTypedReferenceOpcodesFailDuringDecoding(
        string methodName,
        string opcode)
    {
        var method = typeof(UnsupportedOpcodeTests).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var exception = Assert.Throws<NotSupportedException>(
            () => new MethodDecoder().LoadMethod(method));

        Assert.Contains($"MSIL opcode '{opcode}'", exception.Message);
        Assert.Contains("requires an addressable-storage or typed-reference model", exception.Message);
    }

    [Test]
    public void LoadElementAddressIsDecodedForTheArrayReferenceCellModel()
    {
        var method = typeof(UnsupportedOpcodeTests).GetMethod(
            nameof(LoadElementAddress),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        new MethodDecoder().LoadMethod(method);
    }

    [Test]
    public void LoadInstanceFieldAddressIsDecodedForTheFieldReferenceCellModel()
    {
        var method = typeof(InstanceFieldAddressFixture).GetMethod(
            nameof(InstanceFieldAddressFixture.GetValueReference))!;

        new MethodDecoder().LoadMethod(method);
    }

    [Test]
    public void ConstrainedCallIsDecodedForTheManagedReferenceCellModel()
    {
        var method = typeof(UnsupportedOpcodeTests).GetMethod(
            nameof(DisposeConstrained),
            BindingFlags.NonPublic | BindingFlags.Static)!;

        new MethodDecoder().LoadMethod(method);
    }

    [Test]
    public void ConstrainedPrefixWithoutCallvirtFailsDuringDecoding()
    {
        var method = CreateMethodContaining(OpCodes.Constrained, appendReturn: true);

        var exception = Assert.Throws<InvalidProgramException>(
            () => new MethodDecoder().LoadMethod(method));

        Assert.Contains("must immediately precede 'call' or 'callvirt'", exception.Message);
    }

    [Test]
    public void GeneratedResourceLookupUsesItsEmbeddedDefaultString()
    {
        var constructor = typeof(RequiredAttribute).GetConstructor(Type.EmptyTypes)!;
        var decoder = new MethodDecoder();

        decoder.LoadMethod(constructor);

        Assert.DoesNotContain(
            decoder.ReferencedMethods.Keys,
            referenced => referenced.DeclaringType?.FullName == "System.SR");
    }

    [TestCaseSource(nameof(ReservedPrefixFields))]
    public void ReservedPrefixEncodingsFailDuringDecoding(string opcodeFieldName)
    {
        var opcode = (OpCode)typeof(OpCodes)
            .GetField(opcodeFieldName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
        var method = CreateMethodContaining(opcode, appendReturn: opcodeFieldName != "Prefix1");

        var exception = Assert.Throws<NotSupportedException>(
            () => new MethodDecoder().LoadMethod(method));

        Assert.Contains($"Reserved MSIL encoding '{opcode.Name}'", exception.Message);
        Assert.Contains("cannot be executed", exception.Message);
    }

    [TestCaseSource(nameof(IncompleteOpcodeFields))]
    public void OpcodesWithoutCompleteRuntimeSemanticsFailDuringDecoding(string opcodeFieldName)
    {
        var opcode = (OpCode)typeof(OpCodes)
            .GetField(opcodeFieldName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
        var method = CreateMethodContaining(opcode, appendReturn: true);

        var exception = Assert.Throws<NotSupportedException>(
            () => new MethodDecoder().LoadMethod(method));

        Assert.Contains($"MSIL opcode '{opcode.Name}'", exception.Message);
        Assert.Contains("does not have complete runtime semantics", exception.Message);
    }

    private static MethodInfo CreateMethodContaining(OpCode opcode, bool appendReturn)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ReservedOpcodeFixture_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Fixture");
        var type = module.DefineType("ReservedOpcodeFixture", TypeAttributes.Public);
        var method = type.DefineMethod(
            "Execute",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(void),
            Type.EmptyTypes);
        var il = method.GetILGenerator();
        switch (opcode.OperandType)
        {
            case OperandType.InlineType:
                il.Emit(opcode, typeof(int));
                break;
            case OperandType.ShortInlineI:
                il.Emit(opcode, (byte)1);
                break;
            default:
                il.Emit(opcode);
                break;
        }
        if (appendReturn)
        {
            il.Emit(OpCodes.Ret);
        }

        return type.CreateType()!.GetMethod("Execute")!;
    }

    private static ref int LoadElementAddress(int[] values) => ref values[0];

    private static void DisposeConstrained<T>(ref T value)
        where T : IDisposable => value.Dispose();

    private static ref int LoadStaticFieldAddress() => ref _staticValue;

    private static unsafe nint LoadNative(nint* address) => *address;

    private static unsafe float LoadSingle(float* address) => *address;

    private static unsafe double LoadDouble(double* address) => *address;

    private static unsafe void StoreNative(nint* address, nint value) => *address = value;

    private static unsafe void StoreSingle(float* address, float value) => *address = value;

    private static unsafe void StoreDouble(double* address, double value) => *address = value;

    private static void MakeTypedReference(ref int value)
    {
        TypedReference reference = __makeref(value);
    }

    private static Type GetReferencedType(TypedReference reference) => __reftype(reference);

    private static int GetReferencedValue(TypedReference reference) => __refvalue(reference, int);

    private sealed class InstanceFieldAddressFixture
    {
        private int _value;

        public ref int GetValueReference() => ref _value;
    }
}
