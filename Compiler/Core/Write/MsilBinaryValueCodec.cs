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
    internal static class MsilBinaryValueCodec
    {
        internal static void CollectStrings(JToken value, ISet<string> strings)
        {
            switch (value.Type)
            {
                case JTokenType.String:
                    var text = value.Value<string>();
                    if (text != null)
                    {
                        strings.Add(text);
                    }
                    break;
                case JTokenType.Array:
                    foreach (var item in value.Children())
                    {
                        CollectStrings(item, strings);
                    }
                    break;
                case JTokenType.Object:
                    foreach (var property in value.Children<JProperty>())
                    {
                        strings.Add(property.Name);
                        CollectStrings(property.Value, strings);
                    }
                    break;
            }
        }

        internal static void WriteValue(Stream stream, JToken value, IReadOnlyDictionary<string, int> strings)
        {
            switch (value.Type)
            {
                case JTokenType.Null:
                    stream.WriteByte((byte)MsilBinaryPackage.ValueTag.Null);
                    return;
                case JTokenType.Boolean:
                    stream.WriteByte(value.Value<bool>() ? (byte)MsilBinaryPackage.ValueTag.True : (byte)MsilBinaryPackage.ValueTag.False);
                    return;
                case JTokenType.Integer:
                    if (((JValue)value).Value is ulong unsigned)
                    {
                        stream.WriteByte((byte)MsilBinaryPackage.ValueTag.UnsignedInteger);
                        WriteVarUInt(stream, unsigned);
                    }
                    else
                    {
                        stream.WriteByte((byte)MsilBinaryPackage.ValueTag.SignedInteger);
                        WriteVarUInt(stream, ZigZagEncode(value.Value<long>()));
                    }
                    return;
                case JTokenType.Float:
                    if (((JValue)value).Value is float single)
                    {
                        stream.WriteByte((byte)MsilBinaryPackage.ValueTag.Float32);
                        Span<byte> encodedSingle = stackalloc byte[sizeof(float)];
                        BinaryPrimitives.WriteInt32LittleEndian(
                            encodedSingle,
                            BitConverter.SingleToInt32Bits(single));
                        stream.Write(encodedSingle);
                        return;
                    }
                    stream.WriteByte((byte)MsilBinaryPackage.ValueTag.Float64);
                    Span<byte> encodedDouble = stackalloc byte[sizeof(double)];
                    BinaryPrimitives.WriteInt64LittleEndian(
                        encodedDouble,
                        BitConverter.DoubleToInt64Bits(value.Value<double>()));
                    stream.Write(encodedDouble);
                    return;
                case JTokenType.String:
                    stream.WriteByte((byte)MsilBinaryPackage.ValueTag.String);
                    WriteVarUInt(stream, (ulong)strings[value.Value<string>()!]);
                    return;
                case JTokenType.Array:
                    stream.WriteByte((byte)MsilBinaryPackage.ValueTag.Array);
                    var array = (JArray)value;
                    WriteVarUInt(stream, (ulong)array.Count);
                    foreach (var item in array)
                    {
                        WriteValue(stream, item, strings);
                    }
                    return;
                case JTokenType.Object:
                    stream.WriteByte((byte)MsilBinaryPackage.ValueTag.Object);
                    var properties = ((JObject)value).Properties().ToList();
                    WriteVarUInt(stream, (ulong)properties.Count);
                    foreach (var property in properties)
                    {
                        WriteVarUInt(stream, (ulong)strings[property.Name]);
                        WriteValue(stream, property.Value, strings);
                    }
                    return;
                default:
                    throw new NotSupportedException(
                        $"MSIL operand JSON token '{value.Type}' is not supported by the binary protocol.");
            }
        }

        internal static JToken ReadValue(Stream stream, IReadOnlyList<string> strings)
        {
            var tag = stream.ReadByte();
            return (MsilBinaryPackage.ValueTag)tag switch
            {
                MsilBinaryPackage.ValueTag.Null => JValue.CreateNull(),
                MsilBinaryPackage.ValueTag.False => new JValue(false),
                MsilBinaryPackage.ValueTag.True => new JValue(true),
                MsilBinaryPackage.ValueTag.SignedInteger => new JValue(ZigZagDecode(ReadVarUInt(stream))),
                MsilBinaryPackage.ValueTag.UnsignedInteger => new JValue(ReadVarUInt(stream)),
                MsilBinaryPackage.ValueTag.Float64 => ReadDouble(stream),
                MsilBinaryPackage.ValueTag.Float32 => ReadSingle(stream),
                MsilBinaryPackage.ValueTag.String => new JValue(strings[ReadIndex(stream, strings.Count, "string value")]),
                MsilBinaryPackage.ValueTag.Array => ReadArray(stream, strings),
                MsilBinaryPackage.ValueTag.Object => ReadObject(stream, strings),
                _ => throw new InvalidDataException($"MSIL package value tag {tag} is unsupported.")
            };
        }

        internal static JValue ReadDouble(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[sizeof(double)];
            ReadExactly(stream, bytes);
            return new JValue(BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(bytes)));
        }

        internal static JValue ReadSingle(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[sizeof(float)];
            ReadExactly(stream, bytes);
            return new JValue(BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bytes)));
        }

        internal static JArray ReadArray(Stream stream, IReadOnlyList<string> strings)
        {
            var result = new JArray();
            var count = ReadCount(stream, "array item");
            for (var index = 0; index < count; index++)
            {
                result.Add(ReadValue(stream, strings));
            }
            return result;
        }

        internal static JObject ReadObject(Stream stream, IReadOnlyList<string> strings)
        {
            var result = new JObject();
            var count = ReadCount(stream, "object property");
            for (var index = 0; index < count; index++)
            {
                var name = strings[ReadIndex(stream, strings.Count, "object property name")];
                if (result.ContainsKey(name))
                {
                    throw new InvalidDataException($"MSIL operand object repeats property '{name}'.");
                }
                result[name] = ReadValue(stream, strings);
            }
            return result;
        }

        internal static void WriteVarUInt(Stream stream, ulong value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }
            stream.WriteByte((byte)value);
        }

        internal static int GetCommonPrefixLength(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
        {
            var length = Math.Min(left.Length, right.Length);
            var index = 0;
            while (index < length && left[index] == right[index])
            {
                index++;
            }
            return index;
        }

        internal static void WriteUInt16(Stream stream, ushort value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ushort)];
            BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
            stream.Write(bytes);
        }

        internal static ushort ReadUInt16(Stream stream)
        {
            Span<byte> bytes = stackalloc byte[sizeof(ushort)];
            ReadExactly(stream, bytes);
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        }

        internal static ulong ReadVarUInt(Stream stream)
        {
            ulong result = 0;
            for (var shift = 0; shift < 64; shift += 7)
            {
                var next = stream.ReadByte();
                if (next < 0)
                {
                    throw new EndOfStreamException("MSIL package ended inside a variable-length integer.");
                }
                if (shift == 63 && (next & 0xfe) != 0)
                {
                    throw new InvalidDataException("MSIL package contains an overflowing variable-length integer.");
                }
                result |= (ulong)(next & 0x7f) << shift;
                if ((next & 0x80) == 0)
                {
                    return result;
                }
            }
            throw new InvalidDataException("MSIL package contains an invalid variable-length integer.");
        }

        internal static int ReadCount(Stream stream, string kind)
        {
            var value = ReadVarUInt(stream);
            if (value > int.MaxValue)
            {
                throw new InvalidDataException($"MSIL package {kind} count is too large.");
            }
            return (int)value;
        }

        internal static int ReadIndex(Stream stream, int count, string kind)
        {
            var index = ReadCount(stream, kind);
            if (index >= count)
            {
                throw new InvalidDataException($"MSIL package {kind} index {index} is out of range.");
            }
            return index;
        }

        internal static ulong ZigZagEncode(long value) =>
            unchecked((ulong)((value << 1) ^ (value >> 63)));

        internal static long ZigZagDecode(ulong value) =>
            unchecked((long)(value >> 1) ^ -((long)value & 1));

        internal static void ReadExactly(Stream stream, Span<byte> buffer)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer[offset..]);
                if (read == 0)
                {
                    throw new EndOfStreamException("MSIL package ended unexpectedly.");
                }
                offset += read;
            }
        }

    }
}
