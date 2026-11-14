using System.Collections.Generic;

namespace Compiler.DTOs
{
    public class ILBlock
    {
        public int ILOffset { get; set; }
        public List<ILInstruction> Instructions { get; set; }
    }
}
