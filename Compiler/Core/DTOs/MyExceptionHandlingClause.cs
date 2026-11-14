using System;

namespace Compiler.DTOs
{
    public class MyExceptionHandlingClause
    {
        public string Flags { get; set; }
        public int TryStartBlockIndex { get; set; }
        public int TryEndBlockIndex { get; set; }
        public int HandlerStartBlockIndex { get; set; }
        public int HandlerEndBlockIndex { get; set; }
        public int? FilterStartBlockIndex { get; set; }
        public string CatchType { get; set; }
    }
}
