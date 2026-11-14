using System;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.DTOs
{
    public class ILInstruction
    {
        public object Operand { get; set; }
        public OpCode OpCode { get; set; }
        public int ILOffset { get; set; }
    }
}
