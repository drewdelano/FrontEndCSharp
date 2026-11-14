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
    internal static class MsilBinaryPackageReader
    {
        private static ReadOnlySpan<byte> Magic => MsilBinaryPackage.Magic;
        private const byte Version = MsilBinaryPackage.Version;

        internal static MethodDefinition ReadMethodDefinition(
            string path,
            string key,
            string moduleId,
            int? methodToken)
        {
            var definition = ReadPackageSelection(
                path,
                key,
                moduleId,
                methodToken,
                typeName: null).Method;
            return definition ?? throw new KeyNotFoundException(
                key != null
                    ? $"MSIL package does not contain method body '{key}'."
                    : $"MSIL package does not contain method token 0x{methodToken:x8} in module '{moduleId}'.");
        }

        internal static JObject ReadTypeMetadata(string path, string typeName)
            => ReadPackageSelection(
                    path,
                    key: null,
                    moduleId: null,
                    methodToken: null,
                    typeName)
                .TypeMetadata
                ?? throw new KeyNotFoundException(
                    $"MSIL package does not contain type registration '{typeName}'.");

        private static PackageSelection ReadPackageSelection(
            string path,
            string key,
            string moduleId,
            int? methodToken,
            string typeName)
        {
            using var stream = File.OpenRead(path);
            Span<byte> magic = stackalloc byte[Magic.Length];
            ReadExactly(stream, magic);
            if (!magic.SequenceEqual(Magic))
            {
                throw new InvalidDataException($"'{path}' is not a FrontEndCSharp MSIL package.");
            }
            var version = stream.ReadByte();
            if (version != Version)
            {
                throw new InvalidDataException(
                    $"MSIL package version {version} is unsupported; expected {Version}.");
            }

            var strings = new string[ReadCount(stream, "string")];
            var previousBytes = Array.Empty<byte>();
            for (var index = 0; index < strings.Length; index++)
            {
                var prefixLength = ReadCount(stream, "string prefix byte");
                if (prefixLength > previousBytes.Length)
                {
                    throw new InvalidDataException(
                        $"MSIL package string prefix length {prefixLength} exceeds the previous string's {previousBytes.Length} bytes.");
                }
                var suffixLength = ReadCount(stream, "string suffix byte");
                int length;
                try
                {
                    length = checked(prefixLength + suffixLength);
                }
                catch (OverflowException exception)
                {
                    throw new InvalidDataException("MSIL package string byte length is too large.", exception);
                }
                var bytes = new byte[length];
                previousBytes.AsSpan(0, prefixLength).CopyTo(bytes);
                ReadExactly(stream, bytes.AsSpan(prefixLength));
                strings[index] = Wtf8Codec.Decode(bytes);
                if (index > 0 && StringComparer.Ordinal.Compare(strings[index - 1], strings[index]) >= 0)
                {
                    throw new InvalidDataException("MSIL package strings are not strictly ordinally sorted.");
                }
                previousBytes = bytes;
            }

            var opcodes = new Dictionary<ushort, string>();
            var opcodeCount = ReadCount(stream, "opcode");
            for (var index = 0; index < opcodeCount; index++)
            {
                var opcode = ReadUInt16(stream);
                var name = strings[ReadIndex(stream, strings.Length, "opcode name")];
                if (!opcodes.TryAdd(opcode, name))
                {
                    throw new InvalidDataException($"MSIL package repeats opcode 0x{opcode:X4}.");
                }
            }

            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            JObject selectedTypeMetadata = null;
            var typeCount = ReadCount(stream, "type");
            for (var typeIndex = 0; typeIndex < typeCount; typeIndex++)
            {
                var registeredTypeName = strings[ReadIndex(stream, strings.Length, "type name")];
                if (!seenTypes.Add(registeredTypeName))
                {
                    throw new InvalidDataException($"MSIL package repeats type registration '{registeredTypeName}'.");
                }
                var metadata = ReadValue(stream, strings) as JObject
                    ?? throw new InvalidDataException(
                        $"MSIL package type registration '{registeredTypeName}' has invalid metadata.");
                if (string.Equals(registeredTypeName, typeName, StringComparison.Ordinal))
                {
                    selectedTypeMetadata = metadata;
                }
            }

            MethodDefinition result = null;
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var methodCount = ReadCount(stream, "method");
            for (var methodIndex = 0; methodIndex < methodCount; methodIndex++)
            {
                var methodKey = strings[ReadIndex(stream, strings.Length, "method key")];
                if (!seenKeys.Add(methodKey))
                {
                    throw new InvalidDataException($"MSIL package repeats method body '{methodKey}'.");
                }
                var methodName = strings[ReadIndex(stream, strings.Length, "method registration name")];
                var methodMetadata = (JObject)ReadValue(stream, strings);
                var blocks = new JArray();
                var blockCount = ReadCount(stream, "block");
                for (var blockIndex = 0; blockIndex < blockCount; blockIndex++)
                {
                    var instructions = new JArray();
                    var ilOffset = checked((int)ReadVarUInt(stream));
                    var instructionCount = ReadCount(stream, "instruction");
                    for (var instructionIndex = 0; instructionIndex < instructionCount; instructionIndex++)
                    {
                        var opcode = ReadUInt16(stream);
                        if (!opcodes.TryGetValue(opcode, out var opcodeName))
                        {
                            throw new InvalidDataException(
                                $"MSIL package uses undeclared opcode 0x{opcode:X4}.");
                        }
                        var instruction = new JObject { ["oc"] = opcodeName };
                        var hasOperand = stream.ReadByte();
                        if (hasOperand == 1)
                        {
                            instruction["opr"] = ReadValue(stream, strings);
                        }
                        else if (hasOperand != 0)
                        {
                            throw new InvalidDataException("MSIL package has an invalid operand marker.");
                        }
                        instructions.Add(instruction);
                    }
                    blocks.Add(new JObject
                    {
                        ["ilOffset"] = ilOffset,
                        ["instructions"] = instructions
                    });
                }
                if (string.Equals(methodKey, key, StringComparison.Ordinal)
                    || (methodToken.HasValue
                        && methodMetadata.Value<int>("debugMethodToken") == methodToken.Value
                        && string.Equals(
                            methodMetadata.Value<string>("debugModuleId"),
                            moduleId,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    result = new MethodDefinition(methodName, methodMetadata, blocks);
                }
            }
            if (stream.Position != stream.Length)
            {
                throw new InvalidDataException("MSIL package contains trailing data.");
            }
            return new PackageSelection(result, selectedTypeMetadata);
        }

    }
}
