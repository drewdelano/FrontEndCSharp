using Compiler.DTOs;
using Compiler.Read;
using Compiler.Write;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Compiler.Tests;

public sealed class MethodDecoderVariableOperandTests
{
    [Test]
    public void LocalVariableOperandsUseUnsignedCliIndexesAcrossEncodingBoundaries()
    {
        var method = CreateLocalBoundaryMethod();
        var decoder = Decode(method);
        var instructions = GetInstructions(decoder);
        var indexes = instructions
            .Where(instruction => instruction.OpCode == OpCodes.Ldloc_S
                || instruction.OpCode == OpCodes.Stloc_S
                || instruction.OpCode == OpCodes.Ldloc
                || instruction.OpCode == OpCodes.Stloc)
            .Select(instruction => Assert.IsAssignableFrom<LocalVariableInfo>(instruction.Operand).LocalIndex)
            .ToArray();

        Assert.Equal([127, 127, 128, 128, 255, 255, 256, 256], indexes);
        Assert.All(instructions.Where(instruction =>
            instruction.Operand is LocalVariableInfo local && local.LocalIndex <= byte.MaxValue),
            instruction => Assert.EndsWith(".s", instruction.OpCode.Name, StringComparison.Ordinal));
        Assert.All(instructions.Where(instruction =>
            instruction.Operand is LocalVariableInfo local && local.LocalIndex > byte.MaxValue),
            instruction => Assert.False(
                instruction.OpCode.Name!.EndsWith(".s", StringComparison.Ordinal)));
    }

    [Test]
    public void ArgumentOperandsUseUnsignedCliIndexesAndPreserveExplicitThis()
    {
        var staticMethod = CreateArgumentBoundaryMethod();
        var staticDecoder = Decode(staticMethod);
        var staticIndexes = GetInstructions(staticDecoder)
            .Where(instruction => instruction.OpCode.Name!.StartsWith("ldarg", StringComparison.Ordinal))
            .Select(instruction => Assert.IsType<int>(instruction.Operand))
            .ToArray();
        Assert.Equal([127, 128, 255, 256], staticIndexes);

        var instanceMethod = CreateInstanceArgumentBoundaryMethod();
        var instanceDecoder = Decode(instanceMethod);
        var instanceIndexes = GetInstructions(instanceDecoder)
            .Where(instruction => instruction.OpCode.Name!.StartsWith("ldarg", StringComparison.Ordinal))
            .Select(instruction => Assert.IsType<int>(instruction.Operand))
            .ToArray();
        Assert.Equal([0, 127, 128, 255, 256], instanceIndexes);
    }

    [Test]
    public void InvalidVariableIndexesHaveDeterministicDiagnostics()
    {
        var localMethod = CreateInvalidLocalMethod();
        var firstLocal = Assert.Throws<InvalidDataException>(() => Decode(localMethod));
        var secondLocal = Assert.Throws<InvalidDataException>(() => Decode(localMethod));
        Assert.Equal(firstLocal.Message, secondLocal.Message);
        Assert.Contains("local slot 2", firstLocal.Message, StringComparison.Ordinal);
        Assert.Contains("declares 1 locals", firstLocal.Message, StringComparison.Ordinal);

        var argumentMethod = CreateInvalidArgumentMethod();
        var firstArgument = Assert.Throws<InvalidDataException>(() => Decode(argumentMethod));
        var secondArgument = Assert.Throws<InvalidDataException>(() => Decode(argumentMethod));
        Assert.Equal(firstArgument.Message, secondArgument.Message);
        Assert.Contains("argument slot 2", firstArgument.Message, StringComparison.Ordinal);
        Assert.Contains("declares 2 argument slots", firstArgument.Message, StringComparison.Ordinal);
    }

    [Test]
    public void SignedShortOperandsRemainSigned()
    {
        var decoder = Decode(CreateSignedShortOperandMethod());
        var instructions = GetInstructions(decoder);
        var constant = Assert.Single(instructions, instruction =>
            instruction.OpCode == OpCodes.Ldc_I4_S);
        Assert.Equal(-128, Assert.IsType<sbyte>(constant.Operand));

        var branch = Assert.Single(instructions, instruction =>
            instruction.OpCode == OpCodes.Br_S);
        Assert.True(Assert.IsType<int>(branch.Operand) < branch.ILOffset);
    }

    [Test]
    public void VariableOperandsProduceDeterministicMethodMetadata()
    {
        var method = CreateLocalBoundaryMethod();
        var first = MsilBinaryPackage.CreateMethodBody(Decode(method));
        var second = MsilBinaryPackage.CreateMethodBody(Decode(method));

        Assert.True(JToken.DeepEquals(first, second));
    }

    [Test]
    public void FloatingPointOperandsPreserveTheirExactCliBits()
    {
        var decoder = Decode(CreateFloatingPointOperandMethod());
        var instructions = GetInstructions(decoder);
        var single = Assert.IsType<float>(Assert.Single(
            instructions,
            instruction => instruction.OpCode == OpCodes.Ldc_R4).Operand);
        var @double = Assert.IsType<double>(Assert.Single(
            instructions,
            instruction => instruction.OpCode == OpCodes.Ldc_R8).Operand);

        Assert.Equal(unchecked((int)0x80000000), BitConverter.SingleToInt32Bits(single));
        Assert.Equal(unchecked((long)0x8000000000000000), BitConverter.DoubleToInt64Bits(@double));

        var body = MsilBinaryPackage.CreateMethodBody(decoder);
        var operands = body.SelectMany(block => block["instructions"]!)
            .OfType<JObject>()
            .Where(instruction => instruction["opr"] !=  null)
            .ToDictionary(
                instruction => instruction.Value<string>("oc")!,
                instruction => Assert.IsType<JValue>(instruction["opr"]));
        Assert.Equal(
            unchecked((int)0x80000000),
            BitConverter.SingleToInt32Bits(Assert.IsType<float>(operands["ldc.r4"].Value)));
        Assert.Equal(
            unchecked((long)0x8000000000000000),
            BitConverter.DoubleToInt64Bits(Assert.IsType<double>(operands["ldc.r8"].Value)));
    }

    private static MethodInfo CreateLocalBoundaryMethod()
    {
        var method = DefineMethod("LocalBoundaries", MethodAttributes.Public | MethodAttributes.Static);
        var generator = method.Generator;
        for (var index = 0; index <= 256; index++) generator.DeclareLocal(typeof(int));
        foreach (var index in new byte[] { 127, 128, 255 })
        {
            generator.Emit(OpCodes.Ldc_I4, (int)index);
            generator.Emit(OpCodes.Stloc_S, index);
            generator.Emit(OpCodes.Ldloc_S, index);
            generator.Emit(OpCodes.Pop);
        }
        generator.Emit(OpCodes.Ldc_I4, 256);
        generator.Emit(OpCodes.Stloc, (short)256);
        generator.Emit(OpCodes.Ldloc, (short)256);
        generator.Emit(OpCodes.Pop);
        generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static MethodInfo CreateArgumentBoundaryMethod()
    {
        var parameters = Enumerable.Repeat(typeof(int), 257).ToArray();
        var method = DefineMethod(
            "ArgumentBoundaries",
            MethodAttributes.Public | MethodAttributes.Static,
            parameters);
        foreach (var index in new byte[] { 127, 128, 255 })
        {
            method.Generator.Emit(OpCodes.Ldarg_S, index);
            method.Generator.Emit(OpCodes.Pop);
        }
        method.Generator.Emit(OpCodes.Ldarg, (short)256);
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static MethodInfo CreateInstanceArgumentBoundaryMethod()
    {
        var parameters = Enumerable.Repeat(typeof(int), 256).ToArray();
        var method = DefineMethod("InstanceArgumentBoundaries", MethodAttributes.Public, parameters);
        method.Generator.Emit(OpCodes.Ldarg_S, (byte)0);
        method.Generator.Emit(OpCodes.Pop);
        foreach (var index in new byte[] { 127, 128, 255 })
        {
            method.Generator.Emit(OpCodes.Ldarg_S, index);
            method.Generator.Emit(OpCodes.Pop);
        }
        method.Generator.Emit(OpCodes.Ldarg, (short)256);
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static MethodInfo CreateInvalidLocalMethod()
    {
        var method = DefineMethod("InvalidLocal", MethodAttributes.Public | MethodAttributes.Static);
        method.Generator.DeclareLocal(typeof(int));
        method.Generator.Emit(OpCodes.Ldloc_S, (byte)2);
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static MethodInfo CreateInvalidArgumentMethod()
    {
        var method = DefineMethod("InvalidArgument", MethodAttributes.Public, typeof(int));
        method.Generator.Emit(OpCodes.Ldarg_S, (byte)2);
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static MethodInfo CreateSignedShortOperandMethod()
    {
        var method = DefineMethod("SignedShortOperands", MethodAttributes.Public | MethodAttributes.Static);
        var generator = method.Generator;
        var loop = generator.DefineLabel();
        generator.MarkLabel(loop);
        generator.Emit(OpCodes.Ldc_I4_S, (sbyte)-128);
        generator.Emit(OpCodes.Pop);
        generator.Emit(OpCodes.Br_S, loop);
        return CreateMethod(method);
    }

    private static MethodInfo CreateFloatingPointOperandMethod()
    {
        var method = DefineMethod(
            "FloatingPointOperands",
            MethodAttributes.Public | MethodAttributes.Static);
        method.Generator.Emit(
            OpCodes.Ldc_R4,
            BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)));
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(
            OpCodes.Ldc_R8,
            BitConverter.Int64BitsToDouble(unchecked((long)0x8000000000000000)));
        method.Generator.Emit(OpCodes.Pop);
        method.Generator.Emit(OpCodes.Ret);
        return CreateMethod(method);
    }

    private static (TypeBuilder Type, MethodBuilder Builder, ILGenerator Generator) DefineMethod(
        string name,
        MethodAttributes attributes,
        params Type[] parameterTypes)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"VariableOperandFixture_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType(
            "VariableOperandFixture",
            TypeAttributes.Public | TypeAttributes.Class);
        var builder = type.DefineMethod(name, attributes, typeof(void), parameterTypes);
        return (type, builder, builder.GetILGenerator());
    }

    private static MethodInfo CreateMethod(
        (TypeBuilder Type, MethodBuilder Builder, ILGenerator Generator) method) =>
        method.Type.CreateType()!.GetMethod(
            method.Builder.Name,
            BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)!;

    private static MethodDecoder Decode(MethodInfo method)
    {
        var decoder = new MethodDecoder();
        decoder.LoadMethod(method, validateRuntimeSemantics: false);
        return decoder;
    }

    private static ILInstruction[] GetInstructions(MethodDecoder decoder) =>
        decoder.InstructionBlocks.SelectMany(block => block.Instructions).ToArray();
}
