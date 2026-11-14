using Compiler.DTOs;
using Compiler.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Compiler.Read
{
    public class MethodDecoder
    {
        readonly static Dictionary<ushort, OpCode> _opCodes;
        static MethodDecoder()
        {
            // Build the opcode map using reflection on System.Reflection.Emit.OpCodes
            _opCodes = typeof(OpCodes)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(OpCode))
                .Select(f => (OpCode)f.GetValue(null)!)
                .ToDictionary(oc => (ushort)oc.Value);

        }

        public Dictionary<int, bool> JumpTargets { get; init; } = new Dictionary<int, bool>();
        public Dictionary<MethodBase, bool> ReferencedMethods { get; init; } = new Dictionary<MethodBase, bool>();
        public Dictionary<TypeInfo, bool> ReferencedTypes { get; init; } = new Dictionary<TypeInfo, bool>();
        public Dictionary<MemberInfo, bool> ReferencedMembers { get; init; } = new Dictionary<MemberInfo, bool>();

        List<ILInstruction> Instructions { get; init; } = new List<ILInstruction>();
        public List<ILBlock> InstructionBlocks { get; init; } = new List<ILBlock>();

        public MethodBase MethodBase { get; private set; }
        public MethodBody MethodBody { get; private set; }
        public bool IsMandatoryIntrinsic { get; private set; }

        int _ilOffset = 0;

        string GetFullyQualifiedName(MethodBase methodBase) => $"'{methodBase.Module.Assembly.Location}'!{methodBase.DeclaringType} -> {methodBase}";
        string GetFullyQualifiedName() => GetFullyQualifiedName(MethodBase);

        Type[] _typeContext = Type.EmptyTypes;
        Type[] _methodContext = Type.EmptyTypes;
        private TypeInfo _newArrType;
        private bool _validateRuntimeSemantics = true;

        public void LoadMethod(MethodBase method, bool validateRuntimeSemantics = true)
        {
            _validateRuntimeSemantics = validateRuntimeSemantics;
            try
            {
                LoadMethodInternal(method);
            }
            catch (Exception)
            {
                Console.WriteLine($"IL dump for {GetFullyQualifiedName()} at IL Offset 0x{_ilOffset:X4}:");
                foreach (var inst in Instructions)
                {
                    Console.WriteLine($"0x{inst.ILOffset:X4} - {inst.OpCode.Name}");
                }
                throw;
            }
        }

        void LoadMethodInternal(MethodBase method)
        {
            MethodBase = method;
            var isIntrinsic = method.CustomAttributes.Any(ca => ca.AttributeType.FullName == "System.Runtime.CompilerServices.IntrinsicAttribute");

            // setup generics
            _typeContext = MethodBase.DeclaringType.GetGenericArguments() ?? Type.EmptyTypes;
            if (MethodBase.IsGenericMethod)
            {
                _methodContext = MethodBase.GetGenericArguments() ?? Type.EmptyTypes;
            }

            // extract il
            MethodBody = method.GetMethodBody();
            if (isIntrinsic || MethodBody == null || MethodBody.LocalVariables.Any(lv => lv.IsPinned || lv.LocalType.Name.Contains("*")))
            {
                if (!MethodBase.IsAbstract && !MethodBase.DeclaringType.IsInterface)
                {
                    IsMandatoryIntrinsic = true;
                }
                return;
            }
            TrackReferencedLocalTypes();
            var ilBits = MethodBody.GetILAsByteArray();
            HandleExceptionHandlerBlocks();

            // convert il into instructions
            for (var i = 0; i < ilBits.Length; /* nop */)
            {
                var startingPosition = i;
                _ilOffset = i;

                // handle one AND two byte opcodes
                ushort originalOpCodeRaw = ilBits[i];
                ushort opCodeRaw = originalOpCodeRaw;
                i++;
                if (opCodeRaw == 0xFE)
                {
                    if (i >= ilBits.Length)
                    {
                        IlOpcodeValidator.ValidateReservedEncoding(
                            "prefix1",
                            _ilOffset,
                            GetFullyQualifiedName());
                    }

                    opCodeRaw = (ushort)(0xFE00 | ilBits[i]);
                    i++;
                }

                // parse the opcode
                if (!_opCodes.TryGetValue(opCodeRaw, out var opCode))
                {
                    throw new NotSupportedException($"Unknown opcode {opCodeRaw}");
                }

                IlOpcodeValidator.Validate(
                    opCode,
                    _validateRuntimeSemantics,
                    _ilOffset,
                    GetFullyQualifiedName());

                // get the number of bytes from the operand
                var rawOperand = IlOperandReader.Read(opCode, ilBits, i);
                i += rawOperand.Advance;
                var operand = GetOperand(opCode, startingPosition, rawOperand.Value);
                if (operand is ConstructorInfo delegateConstructor
                    && opCode == OpCodes.Newobj
                    && typeof(MulticastDelegate).IsAssignableFrom(delegateConstructor.DeclaringType))
                {
                    ReferencedMethods.Remove(
                        GenericsHelper.GetPossiblyGenericReferenceFromMethod(delegateConstructor));
                }
                else if (operand is MethodInfo delegateInvocation
                    && delegateInvocation.Name == nameof(Action.Invoke)
                    && typeof(MulticastDelegate).IsAssignableFrom(delegateInvocation.DeclaringType))
                {
                    ReferencedMethods.Remove(
                        GenericsHelper.GetPossiblyGenericReferenceFromMethod(delegateInvocation));
                }
                if (opCode.Name == "newarr")
                {
                    // TODO: this is kinda hacky
                    _newArrType = (TypeInfo)operand;
                }

                var lastInstruction = Instructions.LastOrDefault();
                Instructions.Add(new ILInstruction
                {
                    ILOffset = startingPosition,
                    OpCode = opCode,
                    Operand = operand,
                });

                PossiblyHackStringResources(startingPosition, opCode, operand, lastInstruction);
            }

            ValidateInstructionSequences();

            var ilBlocks = new List<ILBlock>();
            var previousILOffset = 0;
            foreach (var jump in JumpTargets.Keys
                .Where(offset => 0 < offset && offset < ilBits.Length)
                .OrderBy(offset => offset))
            {
                ilBlocks.Add(new ILBlock
                {
                    ILOffset = previousILOffset,
                    Instructions = Instructions.Where(i => previousILOffset <= i.ILOffset && i.ILOffset < jump).ToList()
                });
                previousILOffset = jump;
            }
            ilBlocks.Add(new ILBlock
            {
                ILOffset = previousILOffset,
                Instructions = Instructions.Where(i => previousILOffset <= i.ILOffset).ToList()
            });
            InstructionBlocks.AddRange(ilBlocks);
        }

        void ValidateInstructionSequences()
        {
            for (var index = 0; index < Instructions.Count; index++)
            {
                var instruction = Instructions[index];
                if (instruction.OpCode != OpCodes.Constrained)
                {
                    continue;
                }

                if (index + 1 >= Instructions.Count
                    || Instructions[index + 1].OpCode != OpCodes.Callvirt
                        && Instructions[index + 1].OpCode != OpCodes.Call)
                {
                    throw new InvalidProgramException(
                        $"MSIL prefix 'constrained.' at IL offset 0x{instruction.ILOffset:X4} in "
                        + $"{GetFullyQualifiedName()} must immediately precede 'call' or 'callvirt'.");
                }

                var call = Instructions[index + 1];
                if (JumpTargets.ContainsKey(call.ILOffset))
                {
                    throw new InvalidProgramException(
                        $"A branch or exception region in {GetFullyQualifiedName()} targets "
                        + $"'{call.OpCode.Name}' at IL offset 0x{call.ILOffset:X4} without its "
                        + "'constrained.' prefix.");
                }
            }
        }

        void PossiblyHackStringResources(int startingPosition, OpCode opCode, object operand, ILInstruction lastInstruction)
        {
            // hack to patch SR.GetResourceString()
            if (lastInstruction != null && opCode.Name == "call" && operand is MethodBase mb && mb.Name == "GetResourceString" && mb.DeclaringType.FullName == "System.SR")
            {
                var parameters = mb.GetParameters();
                if (parameters.Length == 2
                    && Instructions.Count >= 3
                    && Instructions[^3].OpCode == OpCodes.Ldstr
                    && Instructions[^2].OpCode == OpCodes.Ldstr)
                {
                    var resourceInstruction = Instructions[^3];
                    var defaultInstruction = Instructions[^2];
                    var callInstruction = Instructions[^1];
                    resourceInstruction.Operand = defaultInstruction.Operand;
                    defaultInstruction.OpCode = OpCodes.Nop;
                    defaultInstruction.Operand = null;
                    callInstruction.OpCode = OpCodes.Nop;
                    callInstruction.Operand = null;
                    ReferencedMethods.Remove(
                        GenericsHelper.GetPossiblyGenericReferenceFromMethod(mb));
                    return;
                }
                if (parameters.Length != 1 || lastInstruction.OpCode != OpCodes.Ldstr)
                {
                    return;
                }

                var methodGetResourceString = Type.GetType("System.SR").GetMethod("GetResourceString", BindingFlags.Static | BindingFlags.NonPublic);
                ReferencedMethods.Remove(
                    GenericsHelper.GetPossiblyGenericReferenceFromMethod(mb));
                lastInstruction.Operand = methodGetResourceString.Invoke(null, new[] { lastInstruction.Operand });
                Instructions.RemoveRange(Instructions.Count - 1, 1);
                // pad to keep IL properly byte aligned
                for (var j = 0; j < 5; j++)
                {
                    Instructions.Add(new ILInstruction
                    {
                        ILOffset = startingPosition + j,
                        OpCode = OpCodes.Nop
                    });
                }
            }
        }

        void HandleExceptionHandlerBlocks()
        {
            foreach (var eh in MethodBody.ExceptionHandlingClauses)
            {
                JumpTargets[eh.TryOffset] = true;
                JumpTargets[eh.TryOffset + eh.TryLength] = true;
                JumpTargets[eh.HandlerOffset] = true;
                JumpTargets[eh.HandlerOffset + eh.HandlerLength] = true;
                if (eh.Flags == ExceptionHandlingClauseOptions.Filter)
                {
                    JumpTargets[eh.FilterOffset] = true;
                }
            }
        }

        object GetOperand(OpCode opCode, int startingPosition, object operandRaw)
        {
            switch (opCode.OperandType)
            {
                case OperandType.InlineNone:
                    return null;

                // jump instruction target il addresses
                case OperandType.ShortInlineBrTarget:
                    {
                        var target = startingPosition + (sbyte)operandRaw + 2; // 2 = 1 opcode byte + 1 operand byte (short)
                        JumpTargets[target] = true;
                        return target;
                    }

                case OperandType.InlineBrTarget:
                    {
                        var target = startingPosition + (int)operandRaw + 5; // 5 = 1 opcode byte + 4 operand bytes
                        JumpTargets[target] = true;
                        return target;
                    }

                case OperandType.InlineSwitch:
                    {
                        var targets = (int[])operandRaw;
                        foreach (var target in targets)
                        {
                            JumpTargets[target] = true;
                        }
                        return targets;
                    }

                // local variable references
                case OperandType.ShortInlineVar:
                case OperandType.InlineVar:
                    var variableIndex = operandRaw switch
                    {
                        byte shortIndex => (int)shortIndex,
                        ushort inlineIndex => (int)inlineIndex,
                        _ => throw new InvalidDataException(
                            $"MSIL opcode '{opCode.Name}' at IL offset 0x{startingPosition:X4} in " +
                            $"{GetFullyQualifiedName()} has an invalid variable operand.")
                    };
                    if (opCode.Name.Contains("arg"))
                    {
                        var argumentSlotCount = MethodBase.GetParameters().Length
                            + (MethodBase.IsStatic ? 0 : 1);
                        if (variableIndex >= argumentSlotCount)
                        {
                            throw new InvalidDataException(
                                $"MSIL opcode '{opCode.Name}' at IL offset 0x{startingPosition:X4} in " +
                                $"{GetFullyQualifiedName()} references argument slot {variableIndex}, " +
                                $"but the method declares {argumentSlotCount} argument slots.");
                        }
                        // CLI argument indexes include the implicit `this` slot for an
                        // instance method. Preserve that encoded index directly because
                        // it is also the runtime method-argument array index.
                        return variableIndex;
                    }
                    if (variableIndex >= MethodBody.LocalVariables.Count)
                    {
                        throw new InvalidDataException(
                            $"MSIL opcode '{opCode.Name}' at IL offset 0x{startingPosition:X4} in " +
                            $"{GetFullyQualifiedName()} references local slot {variableIndex}, " +
                            $"but the method declares {MethodBody.LocalVariables.Count} locals.");
                    }
                    return MethodBody.LocalVariables[variableIndex];

                // raw value operand (integer)
                case OperandType.ShortInlineI:
                    return (sbyte)operandRaw;
                case OperandType.InlineI:
                    return (int)operandRaw;
                case OperandType.InlineI8:
                    return (long)operandRaw;

                // raw value operand (floating point)
                case OperandType.ShortInlineR:
                    return BitConverter.Int32BitsToSingle((int)operandRaw);
                case OperandType.InlineR:
                    return BitConverter.Int64BitsToDouble((long)operandRaw);

                // token based look ups
                case OperandType.InlineField:
                    {
                        var contexts = GetDefinitionGenericContexts();
                        var field = MethodBase.Module.ResolveField(
                            (int)operandRaw,
                            contexts.TypeContext,
                            contexts.MethodContext
                        );

                        TrackReferencedField(field, includeTypeInitializer: true);

                        return field;
                    }
                case OperandType.InlineMethod:
                    {
                        var contexts = GetDefinitionGenericContexts();
                        var method = MethodBase.Module.ResolveMethod(
                            (int)operandRaw,
                            contexts.TypeContext,
                            contexts.MethodContext
                        );

                        var methodToTrack = GenericsHelper.GetPossiblyGenericReferenceFromMethod(method);
                        ReferencedMethods[methodToTrack] = true;
                        if ((opCode == OpCodes.Callvirt || opCode == OpCodes.Ldvirtftn)
                            && methodToTrack.DeclaringType is Type declaringType
                            && !declaringType.IsGenericParameter)
                        {
                            var declaringTypeToTrack = GenericsHelper.GetPossiblyGenericReferenceFromType(
                                (TypeInfo)declaringType);
                            ReferencedTypes[declaringTypeToTrack] = true;
                            AddGenericTypesToReferencedTypes((TypeInfo)declaringType);
                        }
                        if (methodToTrack.IsConstructor)
                        {
                            var declType = method.DeclaringType;
                            if (declType.IsConstructedGenericType)
                            {
                                AddGenericTypesToReferencedTypes((TypeInfo)declType);
                            }
                            var ctorType = (TypeInfo)methodToTrack.DeclaringType;
                            ReferencedTypes[ctorType] = true;
                        }
                        return method;
                    }
                case OperandType.InlineType:
                    {
                        var contexts = GetDefinitionGenericContexts();

                        var type = (TypeInfo)MethodBase.Module.ResolveType(
                            (int)operandRaw,
                            contexts.TypeContext,
                            contexts.MethodContext
                        );
                        var typeName = type.Name;

                        var typeToTrack = GenericsHelper.GetPossiblyGenericReferenceFromType(type);

                        ReferencedTypes[typeToTrack] = true;
                        AddGenericTypesToReferencedTypes(type);
                        return type;
                    }
                case OperandType.InlineSig:
                    return MethodBase.Module.ResolveSignature((int)operandRaw);
                case OperandType.InlineString:
                    return MethodBase.Module.ResolveString(MetadataTokens.GetToken(MetadataTokens.UserStringHandle((int)operandRaw)));

                case OperandType.InlineTok:
                    {
                        var tokenHandle = MetadataTokens.EntityHandle((int)operandRaw);
                        var token = MetadataTokens.GetToken(tokenHandle);

                        var contexts = GetDefinitionGenericContexts();

                        // Use Module.ResolveMember to handle field/type/method tokens uniformly
                        var member = MethodBase.Module.ResolveMember(
                            token,
                            contexts.TypeContext,
                            contexts.MethodContext
                        );

                        if (member is FieldInfo memberFieldInfo)
                        {
                            TrackReferencedField(memberFieldInfo);
                            var fieldData = StaticFieldDataReader.Read(
                                memberFieldInfo,
                                _newArrType);
                            if (fieldData.ConsumedArrayType)
                            {
                                _newArrType = null;
                            }

                            return new TokenMetadata
                            {
                                TokenType = "Field",
                                TokenValue = $"{NameNormalizers.GetFullyQualifiedTypeName(memberFieldInfo.DeclaringType)}.{memberFieldInfo.Name}",
                                FieldData = fieldData.Value,
                            };
                        }
                        else if (member is Type memberType)
                        {
                            var type = GenericsHelper.GetPossiblyGenericReferenceFromType((TypeInfo)memberType);
                            ReferencedTypes[type] = true;

                            AddGenericTypesToReferencedTypes(type);
                            return new TokenMetadata
                            {
                                TokenType = "Type",
                                TokenValue = GenericsHelper.GetTypeReference(memberType),
                                IsGenericParameter = memberType.IsGenericParameter ? true : null,
                                SourceType = memberType
                            };
                        }
                        else if (member is MethodBase memberMethodBase)
                        {
                            return new TokenMetadata
                            {
                                TokenType = "Method",
                                TokenValue = new
                                {
                                    methodName = NameNormalizers.GetMethodName(memberMethodBase),
                                    typeName = NameNormalizers.GetFullyQualifiedTypeName(memberMethodBase.DeclaringType)
                                },
                                GenericTypeArguments = GenericsHelper.GetGenericTypeMap(
                                    memberMethodBase.DeclaringType),
                                SourceMethod = memberMethodBase
                            };
                        }
                        else
                        {
                            throw new NotSupportedException($"Unsupported ldtoken member type: {member?.GetType().Name}");
                        }
                    }
                

                default:
                    throw new NotImplementedException($"This should never happen");

            }
        }

        private GenericContexts GetDefinitionGenericContexts()
        {
            // Each generic method body has one runtime registration. Resolve tokens
            // against its definition so the body retains T/!!0 placeholders; the
            // constructed call operand carries the closed substitutions.
            var typeContext = MethodBase.DeclaringType?.IsConstructedGenericType == true
                ? MethodBase.DeclaringType.GetGenericTypeDefinition().GetGenericArguments()
                : _typeContext;
            var methodContext = MethodBase is MethodInfo methodInfo
                && methodInfo.IsConstructedGenericMethod
                    ? methodInfo.GetGenericMethodDefinition().GetGenericArguments()
                    : _methodContext;
            return new GenericContexts(typeContext, methodContext);
        }

        private void AddGenericTypesToReferencedTypes(TypeInfo type)
        {
            if (type.IsConstructedGenericType)
            {
                foreach (var t in type.GetGenericArguments().Cast<TypeInfo>())
                {
                    if (t.IsGenericTypeParameter)
                    {
                        continue;
                    }
                    AddGenericTypesToReferencedTypes(t);
                    ReferencedTypes[GenericsHelper.GetPossiblyGenericReferenceFromType(t)] = true;
                }
            }
        }

        private void TrackReferencedLocalTypes()
        {
            foreach (var local in MethodBody.LocalVariables)
            {
                var localType = local.LocalType;
                while (localType.HasElementType && !localType.IsArray)
                {
                    localType = localType.GetElementType()!;
                }
                if (localType.IsArray)
                {
                    localType = localType.GetElementType()!;
                }
                if (localType.IsPrimitive
                    || localType == typeof(string)
                    || localType.IsGenericParameter
                    || localType.IsFunctionPointer)
                {
                    continue;
                }

                var localTypeInfo = (TypeInfo)localType;
                ReferencedTypes[GenericsHelper.GetPossiblyGenericReferenceFromType(localTypeInfo)] = true;
                AddGenericTypesToReferencedTypes(localTypeInfo);
            }
        }

        private void TrackReferencedField(FieldInfo field, bool includeTypeInitializer = false)
        {
            var fieldToTrack = GenericsHelper.GetPossiblyGenericReferenceFromField(field);
            ReferencedMembers[fieldToTrack] = true;
            ReferencedTypes[(TypeInfo)fieldToTrack.DeclaringType] = true;

            if (includeTypeInitializer
                && fieldToTrack.IsStatic
                && fieldToTrack.DeclaringType?.TypeInitializer is ConstructorInfo typeInitializer
                && typeInitializer != MethodBase)
            {
                ReferencedMethods[typeInitializer] = true;
            }

            var fieldType = fieldToTrack.FieldType;
            while (fieldType.HasElementType)
            {
                fieldType = fieldType.GetElementType();
            }

            if (fieldType.IsGenericParameter || fieldType.IsPrimitive || fieldType == typeof(string))
            {
                return;
            }

            var fieldTypeInfo = (TypeInfo)fieldType;
            ReferencedTypes[GenericsHelper.GetPossiblyGenericReferenceFromType(fieldTypeInfo)] = true;
            AddGenericTypesToReferencedTypes(fieldTypeInfo);
        }

        private sealed record class GenericContexts(Type[] TypeContext, Type[] MethodContext);


        public override string ToString()
        {
            return MethodBase.ToString();
        }
    }
}
