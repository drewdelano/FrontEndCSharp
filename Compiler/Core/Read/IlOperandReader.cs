using System;
using System.Reflection.Emit;

namespace Compiler.Read
{
    internal static class IlOperandReader
    {
        internal static RawIlOperand Read(OpCode opCode, byte[] ilBits, int offset)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    return new RawIlOperand(0, null);

                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                    return new RawIlOperand(1, (sbyte)ilBits[offset]);

                case OperandType.ShortInlineVar:
                    return new RawIlOperand(1, ilBits[offset]);

                case OperandType.InlineVar:
                    return new RawIlOperand(2, BitConverter.ToUInt16(ilBits, offset));

                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineI:
                case OperandType.InlineMethod:
                case OperandType.InlineSig:
                case OperandType.InlineString:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.ShortInlineR:
                    return new RawIlOperand(4, BitConverter.ToInt32(ilBits, offset));

                case OperandType.InlineSwitch:
                    var count = BitConverter.ToInt32(ilBits, offset);
                    var cursor = offset + 4;
                    var targets = new int[count];
                    var endOfSwitch = cursor + (4 * count);
                    for (var index = 0; index < count; index++)
                    {
                        targets[index] = BitConverter.ToInt32(ilBits, cursor) + endOfSwitch;
                        cursor += 4;
                    }
                    return new RawIlOperand(cursor - offset, targets);

                case OperandType.InlineI8:
                case OperandType.InlineR:
                    return new RawIlOperand(8, BitConverter.ToInt64(ilBits, offset));

                default:
                    throw new NotImplementedException(
                        $"Unrecognized operand type: {opCode.OperandType}");
            }
        }
    }

    internal sealed class RawIlOperand
    {
        internal RawIlOperand(int advance, object value)
        {
            Advance = advance;
            Value = value;
        }

        internal int Advance { get; }
        internal object Value { get; }
    }
}
