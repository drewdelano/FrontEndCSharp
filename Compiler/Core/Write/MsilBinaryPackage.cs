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
using static Compiler.Write.MsilBinaryValueCodec;
using static Compiler.Write.MsilPackageMetadataFactory;

namespace Compiler.Write
{
    public static class MsilBinaryPackage
    {
        public const string FileName = "msil.bin";

        internal static readonly byte[] Magic = Encoding.ASCII.GetBytes("FECSIL\r\n");
        internal const byte Version = 7;

        internal enum ValueTag : byte
        {
            Null = 0,
            False = 1,
            True = 2,
            SignedInteger = 3,
            UnsignedInteger = 4,
            Float64 = 5,
            String = 6,
            Array = 7,
            Object = 8,
            Float32 = 9
        }

        public static string GetMethodBodyKey(MethodBase method) =>
            $"{method.Module.ModuleVersionId:D}:{method.MetadataToken}:{NameNormalizers.GetFullyQualifiedMethodName(method)}";

        public static JArray CreateMethodBody(MethodDecoder decoder)
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
            return new JArray(decoder.InstructionBlocks.Select(block => new JObject
            {
                ["ilOffset"] = block.ILOffset,
                ["instructions"] = new JArray(block.Instructions.Select(instruction =>
                {
                    var result = new JObject { ["oc"] = instruction.OpCode.ToString() };
                    var operand = DependencyWriter.FormatOperand(instruction.Operand);
                    if (operand != null)
                    {
                        result["opr"] = JToken.FromObject(operand, serializer);
                    }
                    return result;
                }))
            }));
        }

        internal static void Write(string path, CallGraphInfo callGraph)
        {
            RejectRuntimeIdentityCollisions(callGraph);

            var methods = callGraph.MethodDecoders
                .Where(decoder => !PathLocator.HasBcl(decoder.MethodBase)
                    && !callGraph.RuntimeOverrides.HasRuntimeOverride(decoder.MethodBase))
                .Where(decoder => decoder.MethodBody != null)
                .GroupBy(
                    decoder => GetMethodBodyKey(decoder.MethodBase),
                    StringComparer.Ordinal)
                .Select(group =>
                {
                    var candidates = group
                        .Select(decoder => new MethodBodyCandidate(
                            decoder,
                            CreateMethodBody(decoder)))
                        .ToList();
                    var first = candidates[0];
                    if (candidates.Skip(1).Any(candidate =>
                        !JToken.DeepEquals(first.Body, candidate.Body)))
                    {
                        throw new InvalidDataException(
                            $"Call-graph entries for method body '{group.Key}' normalize to different instructions.");
                    }
                    return CreateMethod(first.Decoder, first.Body);
                })
                .OrderBy(method => method.Key, StringComparer.Ordinal)
                .ToList();
            var reflectedTypeNames = callGraph.ReflectedTypes
                .Select(NameNormalizers.GetFullTypeName)
                .ToHashSet(StringComparer.Ordinal);
            reflectedTypeNames.UnionWith(callGraph.FieldReflectedTypes.SelectMany(type =>
                {
                    var hierarchy = new List<string>();
                    for (var current = (Type)type; current != null; current = current.BaseType)
                    {
                        hierarchy.Add(NameNormalizers.GetFullTypeName(current));
                    }
                    return hierarchy;
                }));
            var enumMetadataTypeNames = callGraph.EnumMetadataTypes
                .Select(NameNormalizers.GetFullTypeName)
                .ToHashSet(StringComparer.Ordinal);
            var constructorsByType = callGraph.MethodDecoders
                .Select(decoder => decoder.MethodBase)
                .OfType<ConstructorInfo>()
                .Where(constructor => !constructor.IsStatic && constructor.DeclaringType != null)
                .GroupBy(
                    constructor => NameNormalizers.GetFullTypeName(constructor.DeclaringType!),
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<ConstructorInfo>)group
                        .Select(GetRuntimeConstructorDefinition)
                        .GroupBy(MetadataIdentity.GetDefinition)
                        .Select(constructors => constructors.First())
                        .OrderBy(constructor => constructor.MetadataToken)
                        .ToList(),
                    StringComparer.Ordinal);
            var types = callGraph.AllReferencedFields
                .Where(item => !item.Key.IsGenericTypeParameter && !item.Key.IsGenericMethodParameter)
                .GroupBy(
                    item => NameNormalizers.GetFullTypeName(item.Key),
                    StringComparer.Ordinal)
                .Select(group =>
                {
                    var type = GetRuntimeTypeDefinition(group.First().Key);
                    var fields = group
                        .SelectMany(item => item.Value)
                        .Select(field => GetRuntimeFieldDefinition(type, field))
                        .GroupBy(
                            MetadataIdentity.GetDefinition,
                            field => field)
                        .Select(fieldsByIdentity => fieldsByIdentity.First())
                        .ToList();
                    var includeReflectionMetadata = reflectedTypeNames.Contains(group.Key);
                    var includeEnumMetadata = enumMetadataTypeNames.Contains(group.Key);
                    constructorsByType.TryGetValue(group.Key, out var constructors);
                    return CreateType(
                        type,
                        fields,
                        includeReflectionMetadata,
                        includeEnumMetadata,
                        constructors ?? Array.Empty<ConstructorInfo>());
                })
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .ToList();
            var strings = new SortedSet<string>(StringComparer.Ordinal);
            var opcodes = new SortedDictionary<ushort, string>();
            foreach (var type in types)
            {
                strings.Add(type.Name);
                CollectStrings(type.Metadata, strings);
            }
            foreach (var method in methods)
            {
                strings.Add(method.Key);
                strings.Add(method.Name);
                CollectStrings(method.Metadata, strings);
                foreach (var block in method.Blocks)
                {
                    foreach (var instruction in block.Instructions)
                    {
                        if (opcodes.TryGetValue(instruction.Opcode, out var existing)
                            && !string.Equals(existing, instruction.OpcodeName, StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                $"CLI opcode 0x{instruction.Opcode:X4} maps to both '{existing}' and '{instruction.OpcodeName}'.");
                        }
                        opcodes[instruction.Opcode] = instruction.OpcodeName;
                        strings.Add(instruction.OpcodeName);
                        if (instruction.Operand != null)
                        {
                            CollectStrings(instruction.Operand, strings);
                        }
                    }
                }
            }

            var stringList = strings.ToList();
            var stringIndexes = new Dictionary<string, int>(stringList.Count, StringComparer.Ordinal);
            for (var index = 0; index < stringList.Count; index++)
            {
                stringIndexes.Add(stringList[index], index);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = File.Create(path);
            stream.Write(Magic);
            stream.WriteByte(Version);
            WriteVarUInt(stream, (ulong)stringList.Count);
            var previousBytes = Array.Empty<byte>();
            foreach (var value in stringList)
            {
                var bytes = Wtf8Codec.Encode(value);
                var prefixLength = GetCommonPrefixLength(previousBytes, bytes);
                WriteVarUInt(stream, (ulong)prefixLength);
                WriteVarUInt(stream, (ulong)(bytes.Length - prefixLength));
                stream.Write(bytes.AsSpan(prefixLength));
                previousBytes = bytes;
            }

            WriteVarUInt(stream, (ulong)opcodes.Count);
            foreach (var (opcode, name) in opcodes)
            {
                WriteUInt16(stream, opcode);
                WriteVarUInt(stream, (ulong)stringIndexes[name]);
            }

            WriteVarUInt(stream, (ulong)types.Count);
            foreach (var type in types)
            {
                WriteVarUInt(stream, (ulong)stringIndexes[type.Name]);
                WriteValue(stream, type.Metadata, stringIndexes);
            }

            WriteVarUInt(stream, (ulong)methods.Count);
            foreach (var method in methods)
            {
                WriteVarUInt(stream, (ulong)stringIndexes[method.Key]);
                WriteVarUInt(stream, (ulong)stringIndexes[method.Name]);
                WriteValue(stream, method.Metadata, stringIndexes);
                WriteVarUInt(stream, (ulong)method.Blocks.Count);
                foreach (var block in method.Blocks)
                {
                    WriteVarUInt(stream, checked((ulong)block.ILOffset));
                    WriteVarUInt(stream, (ulong)block.Instructions.Count);
                    foreach (var instruction in block.Instructions)
                    {
                        WriteUInt16(stream, instruction.Opcode);
                        stream.WriteByte(instruction.Operand == null ? (byte)0 : (byte)1);
                        if (instruction.Operand != null)
                        {
                            WriteValue(stream, instruction.Operand, stringIndexes);
                        }
                    }
                }
            }
        }

        public static JArray ReadMethodBody(string path, string key)
            => MsilBinaryPackageReader.ReadMethodDefinition(
                path,
                key,
                moduleId: null,
                methodToken: null).Blocks;

        internal static MethodDefinition ReadMethodDefinition(string path, string key)
            => MsilBinaryPackageReader.ReadMethodDefinition(
                path,
                key,
                moduleId: null,
                methodToken: null);

        internal static MethodDefinition ReadMethodDefinition(
            string path,
            string moduleId,
            int methodToken)
            => MsilBinaryPackageReader.ReadMethodDefinition(
                path,
                key: null,
                moduleId,
                methodToken);

        internal static JObject ReadTypeMetadata(string path, string typeName)
            => MsilBinaryPackageReader.ReadTypeMetadata(path, typeName);

        internal static JObject CreateTypeMetadata(Type type)
            => MsilPackageMetadataFactory.CreateTypeMetadata(type);

        internal static JObject CreateLocalTypeMetadata(LocalVariableInfo variable)
            => MsilPackageMetadataFactory.CreateLocalTypeMetadata(variable);

        internal static JObject CreateEnumMetadata(TypeInfo type)
            => MsilPackageMetadataFactory.CreateEnumMetadata(type);
    }
}
