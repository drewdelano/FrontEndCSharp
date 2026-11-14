using System;
using System.Reflection.Emit;

namespace Compiler.Read
{
    internal static class IlOpcodeValidator
    {
        internal static void Validate(
            OpCode opCode,
            bool validateRuntimeSemantics,
            int ilOffset,
            string methodName)
        {
            ValidateReservedEncoding(opCode.Name, ilOffset, methodName);
            if (!validateRuntimeSemantics)
            {
                return;
            }
            ValidateRuntimeSemantics(opCode, ilOffset, methodName);
            ValidateAddressModel(opCode, ilOffset, methodName);
        }

        internal static void ValidateReservedEncoding(
            string opCodeName,
            int ilOffset,
            string methodName)
        {
            switch (opCodeName)
            {
                case "prefix1":
                case "prefix2":
                case "prefix3":
                case "prefix4":
                case "prefix5":
                case "prefix6":
                case "prefix7":
                case "prefixref":
                    throw new NotSupportedException(
                        $"Reserved MSIL encoding '{opCodeName}' at IL offset 0x{ilOffset:X4} in "
                        + $"{methodName} cannot be executed.");
            }
        }

        private static void ValidateRuntimeSemantics(
            OpCode opCode,
            int ilOffset,
            string methodName)
        {
            switch (opCode.Name)
            {
                case "localloc":
                case "cpobj":
                case "cpblk":
                case "initblk":
                case "unaligned.":
                case "tail.":
                case "readonly.":
                    throw new NotSupportedException(
                        $"MSIL opcode '{opCode.Name}' at IL offset 0x{ilOffset:X4} in "
                        + $"{methodName} does not have complete runtime semantics and is not supported.");
            }
        }

        private static void ValidateAddressModel(
            OpCode opCode,
            int ilOffset,
            string methodName)
        {
            switch (opCode.Name)
            {
                case "ldsflda":
                case "ldind.i":
                case "ldind.r4":
                case "ldind.r8":
                case "stind.i":
                case "stind.r4":
                case "stind.r8":
                case "refanytype":
                case "mkrefany":
                case "refanyval":
                    throw new NotSupportedException(
                        $"MSIL opcode '{opCode.Name}' at IL offset 0x{ilOffset:X4} in "
                        + $"{methodName} requires an addressable-storage or typed-reference model "
                        + "and is not supported.");
            }
        }
    }
}
