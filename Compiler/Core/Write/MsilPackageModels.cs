using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static Compiler.Write.MsilBinaryPackage;
using static Compiler.Write.MsilBinaryValueCodec;
using static Compiler.Write.MsilPackageMetadataFactory;

namespace Compiler.Write
{
    internal sealed record MethodDefinition(string Name, JObject Metadata, JArray Blocks);
    internal sealed record class MethodBodyCandidate(MethodDecoder Decoder, JArray Body);
    internal sealed record PackageSelection(MethodDefinition Method, JObject TypeMetadata);
    internal sealed record TypePayload(string Name, JObject Metadata);
    internal sealed record MethodPayload(
        string Key,
        string Name,
        JObject Metadata,
        List<BlockPayload> Blocks);
    internal sealed record BlockPayload(int ILOffset, List<InstructionPayload> Instructions);
    internal sealed record InstructionPayload(ushort Opcode, string OpcodeName, JToken Operand);
}
