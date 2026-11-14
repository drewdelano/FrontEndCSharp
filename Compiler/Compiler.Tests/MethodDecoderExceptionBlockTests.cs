using Compiler.Read;
using System.Reflection;

namespace Compiler.Tests;

public class MethodDecoderExceptionBlockTests
{
    [Test]
    public void ExceptionRegionBoundariesAreInstructionBlockBoundaries()
    {
        var method = typeof(MethodDecoderExceptionBlockTests).GetMethod(
            nameof(MethodWithStraightLineCodeBeforeTry),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var methodBody = method.GetMethodBody()!;
        var decoder = new MethodDecoder();

        decoder.LoadMethod(method);

        var blockOffsets = decoder.InstructionBlocks
            .Select(block => block.ILOffset)
            .ToHashSet();
        var methodLength = methodBody.GetILAsByteArray()!.Length;

        foreach (var clause in methodBody.ExceptionHandlingClauses)
        {
            AssertBoundary(clause.TryOffset);
            AssertBoundary(clause.TryOffset + clause.TryLength);
            AssertBoundary(clause.HandlerOffset);
            AssertBoundary(clause.HandlerOffset + clause.HandlerLength);
        }

        void AssertBoundary(int offset)
        {
            if (offset == methodLength)
            {
                return;
            }

            Assert.Contains(offset, blockOffsets);
        }
    }

    private static int MethodWithStraightLineCodeBeforeTry(int value)
    {
        var divisor = value + 1;
        try
        {
            return 10 / divisor;
        }
        catch (DivideByZeroException)
        {
            return 0;
        }
    }
}
