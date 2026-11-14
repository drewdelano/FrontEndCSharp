using System.Reflection;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using Compiler.Write;

namespace Compiler.Tests;

public class DependencyWriterTests
{
    [Test]
    public void EmitsStableManagedMethodIdentityWithoutInstructionOffsets()
    {
        var method = typeof(DependencyWriterTests).GetMethod(
            nameof(DebugLocationFixture),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var decoder = new MethodDecoder();
        decoder.LoadMethod(method);
        var target = Path.Combine(
            Path.GetTempPath(),
            $"FrontEndCSharp_DebugLocation_{Guid.NewGuid():N}.js");

        try
        {
            DependencyWriter.WriteDependency(target, decoder, new CallGraphInfo());

            var output = File.ReadAllText(target);
            Assert.Contains($"debugModuleId: \"{method.Module.ModuleVersionId:D}\"", output);
            Assert.Contains($"debugMethodToken: {method.MetadataToken}", output);
            Assert.Contains("getMethodBody", output);
            Assert.DoesNotContain("instructions:", output);
            Assert.DoesNotContain("\"oc\":", output);
            Assert.DoesNotContain("\"ilOffset\":", output);
        }
        finally
        {
            File.Delete(target);
        }
    }

    [Test]
    public void SerializesFaultExceptionHandlingClause()
    {
        var clause = new TestExceptionHandlingClause(
            ExceptionHandlingClauseOptions.Fault,
            tryOffset: 0,
            tryLength: 10,
            handlerOffset: 10,
            handlerLength: 10);
        var blocks = new List<ILBlock>
        {
            new() { ILOffset = 0, Instructions = [] },
            new() { ILOffset = 10, Instructions = [] }
        };

        var result = DependencyWriter.GetExceptionHandlingClause([clause], blocks, methodLength: 20);

        Assert.Contains("\"Flags\":\"Fault\"", result);
        Assert.Contains("\"TryStartBlockIndex\":0", result);
        Assert.Contains("\"TryEndBlockIndex\":1", result);
        Assert.Contains("\"HandlerStartBlockIndex\":1", result);
        Assert.Contains("\"HandlerEndBlockIndex\":2", result);
    }

    [Test]
    public void InstanceFieldStorageKeysUseDeclaringMetadataIdentity()
    {
        var baseField = typeof(FieldIdentityBase<int>).GetField(nameof(FieldIdentityBase<int>.Value))!;
        var derivedField = typeof(FieldIdentityDerived<int>).GetField(nameof(FieldIdentityDerived<int>.Value))!;

        Assert.Equal(
            $"$field:{typeof(FieldIdentityBase<>).FullName!.Split('`')[0]}`1[T]:Value:{baseField.MetadataToken:X8}",
            NameNormalizers.GetFieldStorageKey(baseField));
        Assert.NotEqual(
            NameNormalizers.GetFieldStorageKey(baseField),
            NameNormalizers.GetFieldStorageKey(derivedField));
    }

    private static int DebugLocationFixture(int value) => value + 1;

    private class FieldIdentityBase<T>
    {
        public T? Value = default;
    }

    private sealed class FieldIdentityDerived<T> : FieldIdentityBase<T>
    {
        public new T? Value = default;
    }

    private sealed class TestExceptionHandlingClause(
        ExceptionHandlingClauseOptions flags,
        int tryOffset,
        int tryLength,
        int handlerOffset,
        int handlerLength) : ExceptionHandlingClause
    {
        public override ExceptionHandlingClauseOptions Flags => flags;
        public override int TryOffset => tryOffset;
        public override int TryLength => tryLength;
        public override int HandlerOffset => handlerOffset;
        public override int HandlerLength => handlerLength;
    }
}
