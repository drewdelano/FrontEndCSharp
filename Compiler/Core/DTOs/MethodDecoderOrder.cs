using Compiler.Read;

namespace Compiler.DTOs;

public class MethodDecoderOrder
{
    public MethodDecoder MethodDecoder { get; set; }
    public int Order { get; set; }
    public MethodBaseEntry Caller { get; set; }
}
