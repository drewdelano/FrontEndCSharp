using System.Collections.Generic;

namespace Compiler.Read
{
    public class TokenMetadata
    {
        public string TokenType { get; set; }
        public object TokenValue { get; set; }
        public object FieldData { get; set; }
        public Dictionary<string, object> GenericTypeArguments { get; set; }
        public bool? IsGenericParameter { get; set; }
        public System.Type SourceType { get; set; }
        public System.Reflection.MethodBase SourceMethod { get; set; }
    }
}
