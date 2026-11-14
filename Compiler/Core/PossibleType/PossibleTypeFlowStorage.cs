using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using FrontEndCSharp.RuntimeOverrides;
using static Compiler.PossibleType.PossibleTypeInstructionFlow;

namespace Compiler.PossibleType
{
    internal sealed class PossibleTypeFlowStorage
    {
        private readonly MethodDecoder _decoder;
        private readonly IReadOnlyDictionary<FieldLocation, FlowValue> _fields;
        private readonly IReadOnlyDictionary<ArrayLocation, FlowValue> _arrayElements;
        private readonly LinearResult _result;

        internal PossibleTypeFlowStorage(
            MethodDecoder decoder,
            IReadOnlyDictionary<FieldLocation, FlowValue> fields,
            IReadOnlyDictionary<ArrayLocation, FlowValue> arrayElements,
            LinearResult result)
        {
            _decoder = decoder;
            _fields = fields;
            _arrayElements = arrayElements;
            _result = result;
        }

        internal FlowValue LoadReferences(
            FlowValue references,
            FlowValue[] localValues,
            FlowValue[] argumentValues,
            IReadOnlyDictionary<FieldLocation, FlowValue> strongFields,
            IReadOnlyDictionary<ArrayLocation, FlowValue> strongArrayElements)
        {
            var value = new FlowValue();
            foreach (var reference in references.References)
            {
                switch (reference.Kind)
                {
                    case StorageKind.Local:
                        value.UnionWith(localValues[reference.Index]);
                        break;
                    case StorageKind.Argument:
                        value.UnionWith(argumentValues[reference.Index]);
                        break;
                    case StorageKind.Field:
                        if (strongFields != null
                            && strongFields.TryGetValue(reference.Field, out var strongFieldValue))
                        {
                            value.UnionWith(strongFieldValue);
                        }
                        else if (_fields.TryGetValue(reference.Field, out var fieldValue))
                        {
                            value.UnionWith(fieldValue);
                        }
                        break;
                    case StorageKind.ArrayElement:
                        if (strongArrayElements != null
                            && strongArrayElements.TryGetValue(
                            reference.Array,
                            out var strongElementValue))
                        {
                            value.UnionWith(strongElementValue);
                        }
                        else if (_arrayElements.TryGetValue(reference.Array, out var elementValue))
                        {
                            value.UnionWith(elementValue);
                        }
                        break;
                }
            }
            return value;
        }

        internal void StoreReferences(
            FlowValue references,
            FlowValue value,
            bool replace,
            FlowValue[] localValues,
            FlowValue[] argumentValues,
            Dictionary<int, FlowValue> byRefWrites,
            Dictionary<FieldLocation, FlowValue> strongFields,
            Dictionary<ArrayLocation, FlowValue> strongArrayElements)
        {
            foreach (var reference in references.References)
            {
                switch (reference.Kind)
                {
                    case StorageKind.Local:
                        if (replace)
                        {
                            localValues[reference.Index] = value.Clone();
                        }
                        else
                        {
                            localValues[reference.Index].UnionWith(value);
                        }
                        break;
                    case StorageKind.Argument:
                        if (replace)
                        {
                            argumentValues[reference.Index] = value.Clone();
                        }
                        else
                        {
                            argumentValues[reference.Index].UnionWith(value);
                        }
                        if (IsByRefArgument(_decoder.MethodBase, reference.Index))
                        {
                            if (byRefWrites == null)
                            {
                                throw new InvalidDataException(
                                    $"Method '{_decoder.MethodBase}' did not initialize by-ref write tracking.");
                            }
                            if (!byRefWrites.TryGetValue(reference.Index, out var written))
                            {
                                written = new FlowValue();
                                byRefWrites.Add(reference.Index, written);
                            }
                            written.UnionWith(value);
                        }
                        break;
                    case StorageKind.Field:
                        if (strongFields == null)
                        {
                            throw new InvalidDataException(
                                $"Method '{_decoder.MethodBase}' did not initialize field-reference tracking.");
                        }
                        strongFields[reference.Field] = value.Clone();
                        AddFieldWrite(reference.Field, value);
                        break;
                    case StorageKind.ArrayElement:
                        if (strongArrayElements == null)
                        {
                            throw new InvalidDataException(
                                $"Method '{_decoder.MethodBase}' did not initialize array-reference tracking.");
                        }
                        strongArrayElements[reference.Array] = value.Clone();
                        AddArrayWrite(reference.Array, value);
                        break;
                }
            }
        }

        internal FlowValue CreateFieldReferences(FieldInfo field, FlowValue receiver)
        {
            var references = new FlowValue();
            if (receiver == null)
            {
                references.AddReference(StorageReference.ForField(
                    new FieldLocation(field, null)));
                return references;
            }
            foreach (var allocation in receiver.Allocations)
            {
                references.AddReference(StorageReference.ForField(
                    new FieldLocation(field, allocation)));
            }
            if (receiver.HasUnknownAllocation || receiver.Allocations.Count == 0)
            {
                references.AddReference(StorageReference.ForField(
                    new FieldLocation(field, null)));
            }
            return references;
        }

        internal FlowValue CreateArrayReferences(FlowValue arrays)
        {
            var references = new FlowValue();
            var allocations = arrays.Allocations
                .Where(allocation => allocation.Type.IsArray)
                .ToArray();
            foreach (var allocation in allocations)
            {
                references.AddReference(StorageReference.ForArrayElement(
                    new ArrayLocation(allocation.Type, allocation)));
            }
            if (arrays.HasUnknownAllocation || allocations.Length == 0)
            {
                foreach (var arrayType in arrays.Types.Where(type => type.IsArray))
                {
                    references.AddReference(StorageReference.ForArrayElement(
                        new ArrayLocation(arrayType, null)));
                }
            }
            return references;
        }

        internal FlowValue LoadField(
            FieldInfo field,
            FlowValue receiver,
            IReadOnlyDictionary<FieldLocation, FlowValue> strongFields)
        {
            var value = new FlowValue();
            var sharedLocation = new FieldLocation(field, null);
            if (strongFields != null
                && strongFields.TryGetValue(sharedLocation, out var strongSharedValue))
            {
                value.UnionWith(strongSharedValue);
            }
            else if (_fields.TryGetValue(sharedLocation, out var sharedValue))
            {
                value.UnionWith(sharedValue);
            }
            if (receiver == null)
            {
                return value;
            }
            foreach (var allocation in receiver.Allocations)
            {
                var location = new FieldLocation(field, allocation);
                if (strongFields != null
                    && strongFields.TryGetValue(location, out var strongAllocatedValue))
                {
                    value.UnionWith(strongAllocatedValue);
                }
                else if (_fields.TryGetValue(location, out var allocatedValue))
                {
                    value.UnionWith(allocatedValue);
                }
            }
            if (receiver.HasUnknownAllocation || receiver.Allocations.Count == 0)
            {
                foreach (var entry in _fields.Where(entry => entry.Key.Field == field))
                {
                    value.UnionWith(entry.Value);
                }
            }
            return value;
        }

        internal void RecordFieldWrite(
            FieldInfo field,
            FlowValue receiver,
            FlowValue value,
            Dictionary<FieldLocation, FlowValue> strongFields)
        {
            if (receiver == null)
            {
                var location = new FieldLocation(field, null);
                if (strongFields?.ContainsKey(location) == true)
                {
                    strongFields[location] = value.Clone();
                }
                AddFieldWrite(location, value);
                return;
            }
            foreach (var allocation in receiver.Allocations)
            {
                var location = new FieldLocation(field, allocation);
                if (strongFields?.ContainsKey(location) == true)
                {
                    strongFields[location] = value.Clone();
                }
                AddFieldWrite(location, value);
            }
            if (receiver.HasUnknownAllocation
                || receiver.Allocations.Count == 0
                    && (receiver.Types.Count != 1
                        || !receiver.Types.Contains(typeof(KnownNull))))
            {
                var location = new FieldLocation(field, null);
                if (strongFields?.ContainsKey(location) == true)
                {
                    strongFields[location] = value.Clone();
                }
                AddFieldWrite(location, value);
            }
        }

        void AddFieldWrite(FieldLocation location, FlowValue value)
        {
            if (!_result.FieldWrites.TryGetValue(location, out var writtenValue))
            {
                writtenValue = new FlowValue();
                _result.FieldWrites.Add(location, writtenValue);
            }
            writtenValue.UnionWith(value);
        }

        internal FlowValue LoadArrayElements(
            FlowValue arrays,
            IReadOnlyDictionary<ArrayLocation, FlowValue> strongArrayElements)
        {
            var value = new FlowValue();
            var allocatedArrayTypes = new HashSet<Type>();
            foreach (var allocation in arrays.Allocations.Where(allocation => allocation.Type.IsArray))
            {
                allocatedArrayTypes.Add(allocation.Type);
                var found = false;
                var sharedLocation = new ArrayLocation(allocation.Type, null);
                if (strongArrayElements != null
                    && strongArrayElements.TryGetValue(sharedLocation, out var strongSharedValue))
                {
                    value.UnionWith(strongSharedValue);
                    found = true;
                }
                else if (_arrayElements.TryGetValue(sharedLocation, out var sharedValue))
                {
                    value.UnionWith(sharedValue);
                    found = true;
                }
                var allocatedLocation = new ArrayLocation(allocation.Type, allocation);
                if (strongArrayElements != null
                    && strongArrayElements.TryGetValue(
                    allocatedLocation,
                    out var strongAllocatedValue))
                {
                    value.UnionWith(strongAllocatedValue);
                    found = true;
                }
                else if (_arrayElements.TryGetValue(allocatedLocation, out var allocatedValue))
                {
                    value.UnionWith(allocatedValue);
                    found = true;
                }
                if (!found)
                {
                    value.UnionWith(ConcreteDeclaredValue(allocation.Type.GetElementType()!));
                }
            }
            foreach (var arrayType in arrays.Types.Where(type => type.IsArray))
            {
                if (!arrays.HasUnknownAllocation && allocatedArrayTypes.Contains(arrayType))
                {
                    continue;
                }
                var found = false;
                foreach (var entry in _arrayElements.Where(entry => entry.Key.ArrayType == arrayType))
                {
                    value.UnionWith(entry.Value);
                    found = true;
                }
                if (!found)
                {
                    value.UnionWith(ConcreteDeclaredValue(arrayType.GetElementType()!));
                }
            }
            return value;
        }

        internal void RecordArrayWrite(
            FlowValue arrays,
            FlowValue value,
            Dictionary<ArrayLocation, FlowValue> strongArrayElements)
        {
            var arrayAllocations = arrays.Allocations
                .Where(allocation => allocation.Type.IsArray)
                .ToList();
            foreach (var allocation in arrayAllocations)
            {
                var location = new ArrayLocation(allocation.Type, allocation);
                if (strongArrayElements?.ContainsKey(location) == true)
                {
                    strongArrayElements[location] = value.Clone();
                }
                AddArrayWrite(location, value);
            }
            if (arrays.HasUnknownAllocation || arrayAllocations.Count == 0)
            {
                foreach (var arrayType in arrays.Types.Where(type => type.IsArray))
                {
                    var location = new ArrayLocation(arrayType, null);
                    if (strongArrayElements?.ContainsKey(location) == true)
                    {
                        strongArrayElements[location] = value.Clone();
                    }
                    AddArrayWrite(location, value);
                }
            }
        }

        void AddArrayWrite(ArrayLocation location, FlowValue value)
        {
            if (!_result.ArrayWrites.TryGetValue(location, out var writtenValue))
            {
                writtenValue = new FlowValue();
                _result.ArrayWrites.Add(location, writtenValue);
            }
            writtenValue.UnionWith(value);
        }

    }
}
