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
    internal static class MsilPackageMetadataFactory
    {
        internal static MethodPayload CreateMethod(MethodDecoder decoder, JArray body)
        {
            var method = decoder.MethodBase;
            var methodBody = decoder.MethodBody!;
            var metadata = new JObject
            {
                ["hasThis"] = !method.IsStatic,
                ["localTypes"] = new JArray(methodBody.LocalVariables.Select(
                    CreateLocalTypeMetadata)),
                ["exceptionHandlers"] = JArray.FromObject(
                    DependencyWriter.CreateExceptionHandlingClauses(
                        methodBody.ExceptionHandlingClauses,
                        decoder.InstructionBlocks,
                        methodBody.GetILAsByteArray().Length),
                    JsonSerializer.Create(new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    })),
                ["genericMethodParameters"] = new JArray(
                    (method is MethodInfo methodInfo && methodInfo.ContainsGenericParameters)
                        ? methodInfo.GetGenericArguments().Select(argument => argument.Name)
                        : Enumerable.Empty<string>()),
                ["debugModuleId"] = method.Module.ModuleVersionId.ToString("D"),
                ["debugMethodToken"] = method.MetadataToken,
                ["fullMethodName"] = NameNormalizers.GetFullyQualifiedMethodName(method),
                ["declaringType"] = NameNormalizers.GetFullTypeName(method.DeclaringType)
            };
            return new MethodPayload(
                GetMethodBodyKey(decoder.MethodBase),
                NameNormalizers.GetFullyQualifiedMethodName(method),
                metadata,
                decoder.InstructionBlocks.Select((block, blockIndex) => new BlockPayload(
                    block.ILOffset,
                    block.Instructions.Select((instruction, instructionIndex) =>
                    {
                        var normalized = (JObject)body[blockIndex]!["instructions"]![instructionIndex]!;
                        return new InstructionPayload(
                            unchecked((ushort)instruction.OpCode.Value),
                            normalized.Value<string>("oc"),
                            normalized["opr"]);
                    }).ToList())).ToList());
        }

        internal static TypePayload CreateType(
            TypeInfo type,
            IEnumerable<FieldInfo> fields,
            bool includeReflectionMetadata,
            bool includeEnumMetadata,
            IEnumerable<ConstructorInfo> constructors)
        {
            var statics = new JObject();
            var instances = new JObject();
            foreach (var field in fields)
            {
                var definition = CreateTypeMetadata(field.FieldType);
                definition["name"] = field.Name;
                definition["declaringType"] = NameNormalizers.GetFullTypeName(field.DeclaringType!);
                definition["metadataToken"] = field.MetadataToken;
                var key = field.IsStatic
                    ? field.Name
                    : NameNormalizers.GetFieldStorageKey(field);
                (field.IsStatic ? statics : instances)[key] = definition;
            }

            var metadata = new JObject
            {
                ["interfaces"] = new JArray(type.GetInterfaces()
                    .Select(CreateTypeReference)),
                ["statics"] = new JObject(),
                ["staticsDefinition"] = statics,
                ["instances"] = instances,
                ["isClass"] = type.IsClass || type.IsInterface,
                ["isValueType"] = type.IsValueType
            };
            if (type.BaseType != null)
            {
                metadata.AddFirst(new JProperty(
                    "baseType",
                    CreateTypeReference(type.BaseType)));
            }
            if (includeEnumMetadata)
            {
                foreach (var property in CreateEnumMetadata(type).Properties())
                {
                    metadata.Add(property.Name, property.Value);
                }
            }
            if (includeReflectionMetadata)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                    | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                metadata["properties"] = new JArray(type.GetProperties(flags).Select(property =>
                {
                    var propertyMetadata = CreateTypeMetadata(property.PropertyType);
                    propertyMetadata["name"] = property.Name;
                    propertyMetadata["metadataToken"] = property.MetadataToken;
                    propertyMetadata["getter"] = property.GetMethod == null
                        ? JValue.CreateNull()
                        : NameNormalizers.GetFullyQualifiedMethodName(property.GetMethod);
                    propertyMetadata["setter"] = property.SetMethod == null
                        ? JValue.CreateNull()
                        : NameNormalizers.GetFullyQualifiedMethodName(property.SetMethod);
                    propertyMetadata["getterIsStatic"] = property.GetMethod?.IsStatic;
                    propertyMetadata["setterIsStatic"] = property.SetMethod?.IsStatic;
                    propertyMetadata["getterIsPublic"] = property.GetMethod?.IsPublic;
                    propertyMetadata["setterIsPublic"] = property.SetMethod?.IsPublic;
                    propertyMetadata["indexParameterCount"] = property.GetIndexParameters().Length;
                    propertyMetadata["attributes"] = CreateAttributes(property.CustomAttributes);
                    return propertyMetadata;
                }));
                metadata["fields"] = new JArray(type.GetFields(flags).Select(field =>
                {
                    var fieldMetadata = CreateTypeMetadata(field.FieldType);
                    fieldMetadata["name"] = field.Name;
                    fieldMetadata["declaringType"] = NameNormalizers.GetFullTypeName(field.DeclaringType!);
                    fieldMetadata["metadataToken"] = field.MetadataToken;
                    fieldMetadata["storageKey"] = field.IsStatic
                        ? field.Name
                        : NameNormalizers.GetFieldStorageKey(field);
                    fieldMetadata["isStatic"] = field.IsStatic;
                    fieldMetadata["isPublic"] = field.IsPublic;
                    fieldMetadata["isInitOnly"] = field.IsInitOnly;
                    fieldMetadata["attributes"] = CreateAttributes(field.CustomAttributes);
                    return fieldMetadata;
                }));
                metadata["methods"] = new JArray(type.GetMethods(flags).Select(method => new JObject
                {
                    ["name"] = method.Name,
                    ["methodName"] = NameNormalizers.GetFullyQualifiedMethodName(method),
                    ["returnType"] = CreateTypeReference(method.ReturnType),
                    ["parameterTypes"] = new JArray(method.GetParameters()
                        .Select(parameter => CreateTypeReference(parameter.ParameterType))),
                    ["isStatic"] = method.IsStatic,
                    ["attributes"] = CreateAttributes(method.CustomAttributes)
                }));
                metadata["constructors"] = new JArray(constructors.Select(constructor => new JObject
                {
                    ["methodName"] = NameNormalizers.GetFullyQualifiedMethodName(constructor),
                    ["isPublic"] = constructor.IsPublic,
                    ["parameters"] = new JArray(constructor.GetParameters().Select(parameter =>
                    {
                        var parameterMetadata = CreateTypeMetadata(parameter.ParameterType);
                        parameterMetadata["name"] = parameter.Name == null
                            ? JValue.CreateNull()
                            : parameter.Name;
                        parameterMetadata["hasDefaultValue"] = parameter.HasDefaultValue;
                        parameterMetadata["defaultValue"] = parameter.HasDefaultValue
                            ? CreateConstant(parameter.DefaultValue)
                            : JValue.CreateNull();
                        parameterMetadata["attributes"] = CreateAttributes(parameter.CustomAttributes);
                        return parameterMetadata;
                    })),
                    ["attributes"] = CreateAttributes(constructor.CustomAttributes)
                }));
                metadata["attributes"] = CreateAttributes(type.CustomAttributes);
            }
            return new TypePayload(NameNormalizers.GetFullTypeName(type), metadata);
        }

        internal static TypeInfo GetRuntimeTypeDefinition(TypeInfo type) =>
            type.IsConstructedGenericType
                ? type.GetGenericTypeDefinition().GetTypeInfo()
                : type;

        internal static FieldInfo GetRuntimeFieldDefinition(TypeInfo runtimeType, FieldInfo field)
        {
            if (!runtimeType.IsGenericTypeDefinition
                || field.DeclaringType == null
                || !field.DeclaringType.IsConstructedGenericType)
            {
                return field;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return runtimeType
                .GetFields(flags)
                .Single(candidate => candidate.MetadataToken == field.MetadataToken);
        }

        internal static ConstructorInfo GetRuntimeConstructorDefinition(
            ConstructorInfo constructor)
        {
            if (constructor.DeclaringType?.IsConstructedGenericType != true)
            {
                return constructor;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public
                | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return constructor.DeclaringType
                .GetGenericTypeDefinition()
                .GetConstructors(flags)
                .Single(candidate => candidate.MetadataToken == constructor.MetadataToken);
        }

        internal static JToken CreateTypeReference(Type type) =>
            JToken.FromObject(GenericsHelper.GetTypeReference(type));

        internal static JObject CreateTypeMetadata(Type type)
        {
            var reference = GenericsHelper.GetTypeReference(type);
            return reference is string typeName
                ? new JObject { ["typeName"] = typeName }
                : JObject.FromObject(reference);
        }

        internal static JObject CreateLocalTypeMetadata(LocalVariableInfo variable)
        {
            var metadata = variable.LocalType.IsFunctionPointer
                ? new JObject { ["typeName"] = JValue.CreateNull() }
                : CreateTypeMetadata(variable.LocalType);
            metadata["isFunctionPointer"] = variable.LocalType.IsFunctionPointer;
            return metadata;
        }

        internal static void RejectRuntimeIdentityCollisions(CallGraphInfo callGraph)
        {
            var typeCollision = callGraph.AllReferencedFields.Keys
                // Arrays, pointers, by-ref types, and generic parameters are type
                // specifications owned by their element/context, not independently
                // defined runtime identities. Only metadata type definitions can
                // collide across modules.
                .Where(type => !type.IsGenericParameter
                    && !type.IsArray
                    && !type.IsPointer
                    && !type.IsByRef
                    && !type.IsFunctionPointer)
                .GroupBy(NameNormalizers.GetFullyQualifiedTypeName, StringComparer.Ordinal)
                .Select(group => new
                {
                    Name = group.Key,
                    Types = group
                        .GroupBy(type =>
                            MetadataIdentity.GetTypeDefinition(type).ModuleIdentity)
                        .Select(definition => definition.First())
                        .ToList()
                })
                .FirstOrDefault(group => group.Types.Count > 1);
            if (typeCollision != null)
            {
                throw new NotSupportedException(
                    $"Managed type name collision '{typeCollision.Name}' is not supported. "
                    + $"It is defined by: {string.Join("; ", typeCollision.Types.Select(type => MetadataIdentity.GetTypeDefinition(type)))}.");
            }

            var methodCollision = callGraph.MethodDecoders
                .Select(decoder => decoder.MethodBase)
                .GroupBy(NameNormalizers.GetFullyQualifiedMethodName, StringComparer.Ordinal)
                .Select(group => new
                {
                    Name = group.Key,
                    Methods = group
                        .GroupBy(MetadataIdentity.GetDefinition)
                        .Select(definition => definition.First())
                        .ToList()
                })
                // Some CoreLib overloads intentionally share one canonical runtime
                // override (for example the synchronous and task-returning generic
                // Task.Run delegates). That selected boundary owns their dispatch;
                // only an unoverridden collision would be ambiguous in emitted MSIL.
                .FirstOrDefault(group => group.Methods.Count > 1
                    && group.Methods.Any(method =>
                        !callGraph.RuntimeOverrides.HasRuntimeOverride(method)));
            if (methodCollision != null)
            {
                throw new NotSupportedException(
                    $"Managed method name collision '{methodCollision.Name}' is not supported. "
                    + $"It is defined by: {string.Join("; ", methodCollision.Methods.Select(MetadataIdentity.GetDefinition))}.");
            }
        }

        internal static JArray CreateAttributes(IEnumerable<CustomAttributeData> attributes)
            => new JArray(attributes.Select(attribute => new JObject
            {
                ["typeName"] = NameNormalizers.GetFullTypeName(attribute.AttributeType),
                ["constructor"] = NameNormalizers.GetFullyQualifiedMethodName(
                    attribute.Constructor),
                ["constructorArgumentTypes"] = new JArray(
                    attribute.Constructor.GetParameters().Select(parameter =>
                        CreateTypeReference(parameter.ParameterType))),
                ["constructorArguments"] = new JArray(attribute.ConstructorArguments.Select(argument =>
                    CreateAttributeValue(argument))),
                ["namedArguments"] = new JObject(attribute.NamedArguments.Select(argument =>
                    new JProperty(
                        argument.MemberName,
                        CreateAttributeValue(argument.TypedValue))))
            }));

        internal static JToken CreateAttributeValue(CustomAttributeTypedArgument argument)
        {
            if (argument.Value == null)
            {
                return JValue.CreateNull();
            }
            if (argument.ArgumentType == typeof(ulong))
            {
                return new JValue(AssertAttributeValueType<ulong>(argument));
            }
            return JToken.FromObject(argument.Value);
        }

        internal static T AssertAttributeValueType<T>(CustomAttributeTypedArgument argument)
            => argument.Value is T value
                ? value
                : throw new InvalidDataException(
                    $"Custom attribute argument declared as '{argument.ArgumentType}' has runtime value type '{argument.Value.GetType()}'.");

        internal static JToken CreateConstant(object value)
            => value == null ? JValue.CreateNull() : JToken.FromObject(value);

        internal static JObject CreateEnumMetadata(TypeInfo type)
        {
            if (!type.IsEnum)
            {
                throw new InvalidDataException(
                    $"Enum metadata was requested for non-enum type '{NameNormalizers.GetFullTypeName(type)}'.");
            }
            return new JObject
            {
                ["enumUnderlyingType"] = NameNormalizers.GetFullTypeName(
                    Enum.GetUnderlyingType(type)),
                ["enumIsFlags"] = type.IsDefined(typeof(FlagsAttribute), inherit: false),
                ["enumValues"] = new JArray(type.GetFields(
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(field => field.IsLiteral)
                    .OrderBy(field => field.MetadataToken)
                    .Select(field => new JObject
                    {
                        ["name"] = field.Name,
                        ["value"] = CreateEnumConstant(field.GetRawConstantValue()!)
                    }))
            };
        }

        internal static JToken CreateEnumConstant(object value) => value switch
        {
            sbyte signed => new JValue(signed),
            byte unsigned => new JValue(unsigned),
            short signed => new JValue(signed),
            ushort unsigned => new JValue(unsigned),
            int signed => new JValue(signed),
            uint unsigned => new JValue(unsigned),
            long signed => new JValue(signed),
            ulong unsigned => new JValue(unsigned),
            _ => throw new InvalidDataException(
                $"Enum constant type '{value.GetType()}' is not an integral CLI type.")
        };

    }
}
