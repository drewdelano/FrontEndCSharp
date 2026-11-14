using Compiler.DTOs;
using Compiler.Helpers;
using Compiler.Read;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Compiler.Write
{
    public class DependencyWriter
    {
        public static void WriteDependency(string target, MethodDecoder methodDecoder, CallGraphInfo callGraph)
        {
            var method = methodDecoder.MethodBase;
            var methodBody = methodDecoder.MethodBody;

            if (methodBody == null && method.DeclaringType.IsInterface)
            {
                methodDecoder.ReferencedTypes[(TypeInfo)method.DeclaringType] = true;
                callGraph.AllReferencedFields[(TypeInfo)method.DeclaringType] = new List<FieldInfo>();
                File.WriteAllText(target, $@"
{GetTypeImports(methodDecoder)}
{GetMethodImports(methodDecoder)}");
                return;
            }

            var fullMethodName = NameNormalizers.GetFullyQualifiedMethodName(method);
            var localTypes = JsonConvert.SerializeObject(
                methodBody.LocalVariables.Select(MsilBinaryPackage.CreateLocalTypeMetadata));
            var exceptionHandlers = GetExceptionHandlingClause(
                methodBody.ExceptionHandlingClauses,
                methodDecoder.InstructionBlocks,
                methodBody.GetILAsByteArray().Length);
            var genericMethodParameters = JsonConvert.SerializeObject((method.ContainsGenericParameters && !method.IsConstructor) ? method.GetGenericArguments().Select(a => a.Name) : Type.EmptyTypes);
            var declaringType = NameNormalizers.GetFullTypeName(method.DeclaringType);

            var ehList = new List<string>();
            var methodBodyKey = JsonConvert.SerializeObject(MsilBinaryPackage.GetMethodBodyKey(method));

            File.WriteAllText(target, $@"
{GetTypeImports(methodDecoder)}
{GetMethodImports(methodDecoder)}
import {{ ExecuteMSIL }} from ""../msil.js"";
import {{ getMethodBody }} from ""../msil-binary.js"";


globalThis.DotNet.Methods[""{fullMethodName}""] = function(ctx, ...args) {{
    const blocks = getMethodBody({methodBodyKey});
    const methodInfo = {{
        hasThis: {(!method.IsStatic).ToString().ToLower()},
        localTypes: {localTypes},
        exceptionHandlers: [
{exceptionHandlers}
        ],
        genericMethodParameters: {genericMethodParameters},
        debugModuleId: ""{method.Module.ModuleVersionId:D}"",
        debugMethodToken: {method.MetadataToken},
        fullMethodName: ""{fullMethodName}"",
        declaringType: ""{declaringType}""
    }};
    return ExecuteMSIL(ctx, methodInfo, blocks, args);
}}

".TrimStart('\n', '\r'));
        }

        static string GetTypeImports(MethodDecoder methodDecoder)
        {
            var referencedTypes = new HashSet<TypeInfo>(methodDecoder.ReferencedTypes.Keys);
            if (methodDecoder.MethodBody !=  null)
            {
                foreach (var local in methodDecoder.MethodBody.LocalVariables)
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
                    if (!localType.IsPrimitive
                        && localType != typeof(string)
                        && !localType.IsGenericParameter
                        && !localType.IsFunctionPointer)
                    {
                        referencedTypes.Add(
                            GenericsHelper.GetPossiblyGenericReferenceFromType((TypeInfo)localType));
                    }
                }
            }

            var list = new List<string>();
            foreach (var typeInfo in referencedTypes)
            {
                if (typeInfo.IsGenericTypeParameter || typeInfo.IsGenericMethodParameter)
                {
                    continue;
                }
                list.Add($@"import ""./{NameNormalizers.GetTypeModuleFileName(typeInfo)}"";");
            }
            return string.Join(Environment.NewLine, list);
        }

        static string GetMethodImports(MethodDecoder methodDecoder)
        {
            var list = new List<string>();
            foreach (var methodInfo in methodDecoder.ReferencedMethods.Keys)
            {
                var methodFileName = NameNormalizers.GetMethodModuleFileName(methodInfo);
                list.Add($@"import ""./{methodFileName}"";");
            }
            return string.Join(Environment.NewLine, list);
        }

        internal static object FormatOperand(object operand)
        {
            if (operand == null)
            {
                return null;
            }
            var type = operand.GetType();
            switch (type.FullName)
            {
                case "System.Byte":
                case "System.SByte":
                case "System.SByte[]":
                case "System.Double":
                case "System.Double[]":
                case "System.Single":
                case "System.Single[]":
                case "System.Int32":
                case "System.Int32[]":
                case "System.Int64":
                case "System.Int64[]":
                case "System.String":
                case "System.String[]":
                    return operand;

                case "System.Byte[]":
                    // Newtonsoft's JSON writer historically represented an InlineSig
                    // blob as Base64. Preserve that normalized operand shape now that
                    // the binary package consumes the typed token tree directly.
                    return Convert.ToBase64String((byte[])operand);

                case "System.Reflection.RuntimeMethodInfo":
                    {
                        var methodInfo = (MethodInfo)operand;
                        if (methodInfo.CustomAttributes.Any(attribute =>
                            attribute.AttributeType.FullName == "System.Runtime.InteropServices.UnmanagedCallersOnlyAttribute"))
                        {
                            throw new NotSupportedException(
                                $"Unmanaged function pointers are not supported: {methodInfo.DeclaringType?.FullName}.{methodInfo.Name}");
                        }
                        var methodInfoForName = methodInfo;
                        if (methodInfoForName.IsGenericMethod)
                        {
                            methodInfoForName = methodInfoForName.GetGenericMethodDefinition();
                        }
                        if (methodInfoForName.DeclaringType.IsConstructedGenericType)
                        {
                            var genericType = methodInfoForName.DeclaringType.GetGenericTypeDefinition();
                            methodInfoForName = genericType
                                .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)
                                .SingleOrDefault(m => m.HasSameMetadataDefinitionAs(methodInfoForName));
                        }
                        var methodParams = methodInfo.GetParameters();
                        var genericParams = methodInfo.ContainsGenericParameters ? methodInfo.GetGenericArguments() : null;
                        var declType = methodInfo.DeclaringType;
                        var genericTypeParams = GenericsHelper.GetGenericTypeMap(declType);
                        var genericMethodParams = GenericsHelper.GetGenericMethodMap(methodInfo);
                        if (declType.IsConstructedGenericType)
                        {
                            declType = declType.GetGenericTypeDefinition();
                        }
                        var typeName = NameNormalizers.GetFullTypeName(declType);

                        return new
                        {
                            typeName,
                            methodName = NameNormalizers.GetMethodName(methodInfoForName),
                            genericTypeParams,
                            genericMethodParams,
                            argCount = methodParams.Count(),
                            paramTypes = methodParams.Select(p => NameNormalizers.GetFullTypeName(p.ParameterType)).ToList(),
                            returnType = NameNormalizers.GetFullTypeName(methodInfo.ReturnType),
                            hasThis = !methodInfo.IsStatic,
                            isDelegateInvocation = methodInfo.Name == nameof(Action.Invoke)
                                && typeof(MulticastDelegate).IsAssignableFrom(methodInfo.DeclaringType)
                                    ? true
                                    : (bool?)null
                        };
                    }
                case "System.Reflection.RuntimeConstructorInfo":
                    {
                        var ctorInfo = (ConstructorInfo)operand;
                        var declType = ctorInfo.DeclaringType;
                        var genericTypeParams = GenericsHelper.GetGenericTypeMap(declType);
                        if (declType.IsConstructedGenericType)
                        {
                            declType = declType.GetGenericTypeDefinition();
                            ctorInfo = declType
                                .GetGenericTypeDefinition()
                                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                .Single(m => m.HasSameMetadataDefinitionAs(ctorInfo));
                        }
                        var ctorParams = ctorInfo.GetParameters();
                        var typeName = NameNormalizers.GetFullTypeName(declType);

                        return new
                        {
                            typeName,
                            methodName = NameNormalizers.GetMethodName(ctorInfo),
                            genericTypeParams,
                            argCount = ctorParams.Count(),
                            paramTypes = ctorParams.Select(p => NameNormalizers.GetFullTypeName(p.ParameterType)).ToList(),
                            hasThis = !ctorInfo.IsStatic,
                            isDelegate = typeof(MulticastDelegate).IsAssignableFrom(ctorInfo.DeclaringType)
                                ? true
                                : (bool?)null
                        };
                    }
                case "System.Reflection.RtFieldInfo":
                    {
                        var fieldInfo = (FieldInfo)operand;
                        var declType = fieldInfo.DeclaringType;
                        var genericTypeParams = GenericsHelper.GetGenericTypeMap(declType);
                        if (declType.IsConstructedGenericType)
                        {
                            declType = declType.GetGenericTypeDefinition();
                        }
                        var typeName = NameNormalizers.GetFullTypeName(declType);

                        return new
                        {
                            typeName,
                            fieldName = fieldInfo.Name,
                            storageKey = fieldInfo.IsStatic
                                ? fieldInfo.Name
                                : NameNormalizers.GetFieldStorageKey(fieldInfo),
                            genericTypeParams,
                            hasThis = !fieldInfo.IsStatic
                        };
                    }

                case "System.Reflection.RuntimeLocalVariableInfo":
                    return ((LocalVariableInfo)operand).LocalIndex;

                case "System.Reflection.RuntimeParameterInfo":
                    var parameterInfo = (ParameterInfo)operand;
                    var declaringMethod = (MethodBase)parameterInfo.Member;
                    return parameterInfo.Position + (declaringMethod.IsStatic ? 0 : 1);

                case "System.RuntimeType":
                    var typeInfo = (TypeInfo)operand;
                    var typeReference = GenericsHelper.GetTypeReference(typeInfo);
                    return typeReference is string simpleTypeName
                        ? new { typeName = simpleTypeName, genericTypeParams = (object)null }
                        : typeReference;

                case "Compiler.Read.TokenMetadata":
                    var tokenMetadata = (TokenMetadata)operand;
                    return new
                    {
                        tokenType = tokenMetadata.TokenType,
                        tokenValue = tokenMetadata.TokenValue,
                        fieldData = tokenMetadata.FieldData,
                        genericTypeArguments = tokenMetadata.GenericTypeArguments,
                        isGenericParameter = tokenMetadata.IsGenericParameter
                    };

                default:
                    throw new NotSupportedException(
                        $"IL operand type '{operand?.GetType().FullName ?? "<null>"}' is not supported.");
            }
        }

        public static string GetExceptionHandlingClause(
            IList<ExceptionHandlingClause> exceptionHandlingClauses,
            List<ILBlock> instructionBlocks,
            int methodLength)
        {
            return string.Join(Environment.NewLine, CreateExceptionHandlingClauses(
                exceptionHandlingClauses,
                instructionBlocks,
                methodLength).Select(item => $"            {JsonConvert.SerializeObject(
                    item,
                    Formatting.None,
                    new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore })},"));
        }

        internal static IReadOnlyList<MyExceptionHandlingClause> CreateExceptionHandlingClauses(
            IList<ExceptionHandlingClause> exceptionHandlingClauses,
            List<ILBlock> instructionBlocks,
            int methodLength)
        {
            var list = new List<MyExceptionHandlingClause>();
            foreach (var exceptionHandlingClause in exceptionHandlingClauses)
            {
                var newItem = new MyExceptionHandlingClause
                {
                    Flags = exceptionHandlingClause.Flags.ToString(),
                    TryStartBlockIndex = GetExceptionBoundaryBlockIndex(
                        instructionBlocks,
                        exceptionHandlingClause.TryOffset,
                        methodLength),
                    TryEndBlockIndex = GetExceptionBoundaryBlockIndex(
                        instructionBlocks,
                        exceptionHandlingClause.TryOffset + exceptionHandlingClause.TryLength,
                        methodLength),
                    HandlerStartBlockIndex = GetExceptionBoundaryBlockIndex(
                        instructionBlocks,
                        exceptionHandlingClause.HandlerOffset,
                        methodLength),
                    HandlerEndBlockIndex = GetExceptionBoundaryBlockIndex(
                        instructionBlocks,
                        exceptionHandlingClause.HandlerOffset + exceptionHandlingClause.HandlerLength,
                        methodLength),
                };
                switch (exceptionHandlingClause.Flags)
                {
                    case ExceptionHandlingClauseOptions.Clause:
                        newItem.CatchType = NameNormalizers.GetFullTypeName(exceptionHandlingClause.CatchType);
                        break;

                    case ExceptionHandlingClauseOptions.Finally:
                    case ExceptionHandlingClauseOptions.Fault:
                        /* nop */
                        break;

                    case ExceptionHandlingClauseOptions.Filter:
                        newItem.FilterStartBlockIndex = GetExceptionBoundaryBlockIndex(
                            instructionBlocks,
                            exceptionHandlingClause.FilterOffset,
                            methodLength);
                        break;

                    default:
                        throw new NotSupportedException(
                            $"Unsupported exception handling clause: {exceptionHandlingClause.Flags}.");
                }

                list.Add(newItem);
            }
            return list;
        }

        static int GetExceptionBoundaryBlockIndex(
            List<ILBlock> instructionBlocks,
            int ilOffset,
            int methodLength)
        {
            if (ilOffset == methodLength)
            {
                return instructionBlocks.Count;
            }

            var blockIndex = instructionBlocks.FindIndex(block => block.ILOffset == ilOffset);
            if (blockIndex < 0)
            {
                throw new InvalidOperationException(
                    $"IL offset 0x{ilOffset:X4} is not an instruction block boundary.");
            }

            return blockIndex;
        }

        public static void WriteType(string target, TypeInfo type, List<FieldInfo> fields)
        {
            if (type.IsGenericTypeParameter || type.IsGenericMethodParameter)
            {
                return;
            }
            var sb = new StringBuilder();
            var fieldTypes = new Dictionary<Type, bool>();
            if (type.BaseType !=  null)
            {
                sb.AppendLine($"    baseType: \"{NameNormalizers.GetFullyQualifiedTypeName(type.BaseType)}\",");
            }
            sb.AppendLine(
                $"    interfaces: {JsonConvert.SerializeObject(type.GetInterfaces().Select(NameNormalizers.GetFullyQualifiedTypeName))},");
            sb.AppendLine($"    statics: {{}},");
            sb.AppendLine($"    staticsDefinition: {{");
            foreach (var field in fields.Where(f => f.IsStatic))
            {
                sb.AppendLine($@"        [""{field.Name}""]: {{ typeName: ""{NameNormalizers.GetFullyQualifiedTypeName(field.FieldType)}"" }},");
                fieldTypes[field.FieldType] = true;
            }
            sb.AppendLine($"    }},");
            sb.AppendLine($"    instances: {{");
            foreach (var field in fields.Where(f => !f.IsStatic))
            {
                sb.AppendLine($@"        [""{field.Name}""]: {{ typeName: ""{NameNormalizers.GetFullyQualifiedTypeName(field.FieldType)}"" }},");
                fieldTypes[field.FieldType] = true;
            }
            sb.AppendLine($"    }},");

            var imports = new List<string>();
            foreach (var fieldTypeKey in fieldTypes.Keys)
            {
                var fieldType = fieldTypeKey;
                if (fieldType.IsArray)
                {
                    fieldType = fieldType.GetElementType();
                }

                if (fieldType.IsGenericParameter || fieldType.IsGenericMethodParameter)
                {
                    continue;
                }
                
                if (!fieldType.IsPrimitive && fieldType != typeof(string))
                {
                    var import = $@"import ""./{NameNormalizers.GetTypeModuleFileName(fieldType)}"";";
                    imports.Add(import);
                }
            }


            var fullTypeName = NameNormalizers.GetFullyQualifiedTypeName(type);
            File.WriteAllText(target, $@"
{string.Join(Environment.NewLine, imports)}

globalThis.DotNet.Types[""{fullTypeName}""] = {{
{sb}
    isClass: {(type.IsClass || type.IsInterface).ToString().ToLowerInvariant()}
}};
");
        }
    }
}
