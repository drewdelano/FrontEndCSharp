using Compiler.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Compiler.Read
{
    internal static class StaticFieldDataReader
    {
        internal static StaticFieldDataResult Read(
            FieldInfo field,
            TypeInfo pendingArrayType)
        {
            if (!field.Attributes.HasFlag(FieldAttributes.HasFieldRVA))
            {
                return StaticFieldDataResult.NotRead();
            }

            var bytes = File.ReadAllBytes(PathLocator.GetModulePath(field.Module));
            using var assemblyStream = new MemoryStream(bytes);
            using var peReader = new PEReader(assemblyStream);
            var metadataReader = peReader.GetMetadataReader();
            var fieldHandle = MetadataTokens.Handle(field.MetadataToken);
            if (fieldHandle.Kind != HandleKind.FieldDefinition)
            {
                return StaticFieldDataResult.NotRead();
            }

            var definition = metadataReader.GetFieldDefinition(
                (FieldDefinitionHandle)fieldHandle);
            if (definition.GetRelativeVirtualAddress() <= 0)
            {
                return StaticFieldDataResult.NotRead();
            }

            var bytesActuallyUsed = field.FieldType.StructLayoutAttribute.Size;
            var fieldBits = peReader.GetSectionData(definition.GetRelativeVirtualAddress())
                .GetContent()
                .Take(bytesActuallyUsed)
                .ToArray();
            // RVA-backed fields are also used as ordinary implementation details
            // (for example, by source-generated regular expressions). Only consume
            // the data when it is the field token for RuntimeHelpers.InitializeArray.
            if (pendingArrayType == null)
            {
                return StaticFieldDataResult.NotRead();
            }

            object value;
            switch (pendingArrayType.FullName)
            {
                case "System.SByte":
                    value = ConvertArray(fieldBits, (bits, offset) => (sbyte)bits[offset]);
                    break;
                case "System.Byte":
                    value = ConvertArray(fieldBits, (bits, offset) => bits[offset]);
                    break;
                case "System.Int16":
                    value = ConvertArray(fieldBits, BitConverter.ToInt16);
                    break;
                case "System.Int32":
                    value = ConvertArray(fieldBits, BitConverter.ToInt32);
                    break;
                case "System.Int64":
                    value = ConvertArray(fieldBits, BitConverter.ToInt64);
                    break;
                case "System.Single":
                    value = ConvertArray(fieldBits, BitConverter.ToSingle);
                    break;
                case "System.Double":
                    value = ConvertArray(fieldBits, BitConverter.ToDouble);
                    break;
                default:
                    throw new NotSupportedException();
            }
            return StaticFieldDataResult.Read(value);
        }

        private static object ConvertArray<T>(
            byte[] fieldBits,
            Func<byte[], int, T> converter)
        {
            var values = new List<T>();
            var size = Marshal.SizeOf<T>();
            for (var offset = 0; offset < fieldBits.Length; offset += size)
            {
                values.Add(converter(fieldBits, offset));
            }
            return new
            {
                typeName = NameNormalizers.GetFullyQualifiedTypeName(typeof(T)),
                arr = values
            };
        }
    }

    internal sealed class StaticFieldDataResult
    {
        private StaticFieldDataResult(bool consumedArrayType, object value)
        {
            ConsumedArrayType = consumedArrayType;
            Value = value;
        }

        internal bool ConsumedArrayType { get; }
        internal object Value { get; }

        internal static StaticFieldDataResult NotRead() => new(false, null);
        internal static StaticFieldDataResult Read(object value) => new(true, value);
    }
}
