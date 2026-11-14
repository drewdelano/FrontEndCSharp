using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Compiler.PossibleType;
using Compiler.Read;
using FrontEndCSharp.RuntimeOverrides;

namespace Compiler.Helpers
{
    internal static class RuntimeOverrideManagedDependencyInspector
    {
        internal static IReadOnlyList<RuntimeOverrideManagedDependency> GetManagedDependencies(
            MethodInfo providerMethod,
            RuntimeOverrideDependencyReader dependencyReader)
        {
            if ((providerMethod.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0
                || providerMethod.GetMethodBody() == null)
            {
                return Array.Empty<RuntimeOverrideManagedDependency>();
            }

            var executableProviderMethod = dependencyReader.ResolveProviderMethod(
                providerMethod.Module.ModuleVersionId,
                providerMethod.MetadataToken);
            var decoder = new MethodDecoder();
            decoder.LoadMethod(executableProviderMethod, validateRuntimeSemantics: false);
            return decoder.ReferencedMethods.Keys
                .Where(method => method.Module.Assembly != executableProviderMethod.Module.Assembly)
                .Select(method => new RuntimeOverrideManagedDependency
                {
                    AssemblyName = method.Module.Assembly.GetName().Name
                        ?? throw new InvalidDataException(
                            $"Managed dependency '{method}' has no assembly simple name."),
                    Signature = NameNormalizers.GetFullyQualifiedMethodName(method),
                    GenericArgumentPatterns = GetDependencyGenericArgumentPatterns(
                        executableProviderMethod,
                        method),
                })
                .DistinctBy(
                    dependency => new ManagedDependencyIdentity(
                        dependency.AssemblyName,
                        dependency.Signature,
                        string.Join(
                            ";",
                            dependency.GenericArgumentPatterns.Select(pattern =>
                                $"{pattern.GenericParameterIndex}:{pattern.AssemblyName}:{pattern.TypeName}"))))
                .ToList();
        }

        internal static IReadOnlyList<RuntimeOverrideManagedTypeReference>
            GetManagedAwaitableResultTypePatterns(
                MethodInfo providerMethod,
                RuntimeOverrideDependencyReader dependencyReader)
        {
            if (!providerMethod.ReturnType.IsConstructedGenericType
                || !string.Equals(
                    providerMethod.ReturnType.GetGenericTypeDefinition().FullName,
                    typeof(System.Threading.Tasks.Task<>).FullName,
                    StringComparison.Ordinal)
                || (providerMethod.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0
                || providerMethod.GetMethodBody() == null)
            {
                return Array.Empty<RuntimeOverrideManagedTypeReference>();
            }

            var executableProviderMethod = dependencyReader.ResolveProviderMethod(
                providerMethod.Module.ModuleVersionId,
                providerMethod.MetadataToken);
            var resultContract = executableProviderMethod.ReturnType.GetGenericArguments()[0];
            var decoder = new MethodDecoder();
            decoder.LoadMethod(executableProviderMethod, validateRuntimeSemantics: false);
            return PossibleTypeAnalysis.FindAwaitableResultTypes(decoder)
                .Where(type => resultContract.IsAssignableFrom(type)
                    && !type.IsAbstract
                    && !type.IsInterface)
                .Distinct()
                .Select(CreateManagedTypeReference)
                .ToList();
        }

        internal static RuntimeOverrideManagedTypeReference CreateManagedTypeReference(Type type)
        {
            if (type.IsArray)
            {
                return new RuntimeOverrideManagedTypeReference
                {
                    ArrayElementType = CreateManagedTypeReference(type.GetElementType()!),
                    ArrayKind = type.IsSZArray ? "szarray" : "array",
                    ArrayRank = type.GetArrayRank(),
                    ArraySizes = Array.Empty<int>(),
                    ArrayLowerBounds = Array.Empty<int>(),
                };
            }
            var definition = type.IsConstructedGenericType
                ? type.GetGenericTypeDefinition()
                : type;
            return new RuntimeOverrideManagedTypeReference
            {
                AssemblyName = definition.Assembly.GetName().Name
                    ?? throw new InvalidDataException($"Managed result type '{type}' has no assembly name."),
                TypeName = definition.FullName
                    ?? throw new InvalidDataException($"Managed result type '{type}' has no metadata name."),
                GenericArguments = type.IsConstructedGenericType
                    ? type.GetGenericArguments().Select(CreateManagedTypeReference).ToList()
                    : Array.Empty<RuntimeOverrideManagedTypeReference>(),
            };
        }

        internal static IReadOnlyList<RuntimeOverrideManagedByRefParameterWrite>
            GetManagedByRefParameterWrites(
                MethodInfo providerMethod,
                RuntimeOverrideDependencyReader dependencyReader,
                RuntimeOverrideMemberKind memberKind)
        {
            if ((providerMethod.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0
                || providerMethod.GetMethodBody() == null)
            {
                return Array.Empty<RuntimeOverrideManagedByRefParameterWrite>();
            }

            var executableProviderMethod = dependencyReader.ResolveProviderMethod(
                providerMethod.Module.ModuleVersionId,
                providerMethod.MetadataToken);
            var decoder = new MethodDecoder();
            decoder.LoadMethod(executableProviderMethod, validateRuntimeSemantics: false);
            var providerParameterOffset = memberKind == RuntimeOverrideMemberKind.InstanceMethod ? 1 : 0;
            return PossibleTypeAnalysis.FindByRefParameterTypes(decoder)
                .Select(write =>
                {
                    var targetParameterIndex = write.ParameterIndex - providerParameterOffset;
                    if (targetParameterIndex < 0)
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider '{providerMethod}' writes through its instance receiver parameter.");
                    }
                    return new RuntimeOverrideManagedByRefParameterWrite
                    {
                        ParameterIndex = targetParameterIndex,
                        IsOut = write.IsOut,
                        IncludesNull = write.IncludesNull,
                        Types = write.Types.Select(CreateManagedTypeReference).ToList(),
                    };
                })
                .ToList();
        }

        private static IReadOnlyList<RuntimeOverrideManagedTypeReference>
            GetDependencyGenericArgumentPatterns(
                MethodInfo providerMethod,
                MethodBase dependency)
        {
            if (dependency is not MethodInfo dependencyMethod
                || !dependencyMethod.IsConstructedGenericMethod)
            {
                return Array.Empty<RuntimeOverrideManagedTypeReference>();
            }

            var result = new List<RuntimeOverrideManagedTypeReference>();
            foreach (var argument in dependencyMethod.GetGenericArguments())
            {
                if (argument.IsGenericMethodParameter
                    && argument.DeclaringMethod != null
                    && argument.DeclaringMethod.HasSameMetadataDefinitionAs(providerMethod))
                {
                    result.Add(new RuntimeOverrideManagedTypeReference
                    {
                        GenericParameterIndex = argument.GenericParameterPosition,
                    });
                    continue;
                }
                if (argument.ContainsGenericParameters)
                {
                    throw new InvalidDataException(
                        $"Runtime override provider method '{providerMethod}' has managed dependency "
                        + $"'{dependency}' with unsupported generic argument pattern '{argument}'.");
                }

                result.Add(CreateManagedTypeReference(argument));
            }
            return result;
        }

        internal static IReadOnlyList<RuntimeOverrideManagedTypeReference> GetManagedReturnTypePatterns(
            MethodInfo providerMethod)
        {
            var attributes = FindAttributes(
                providerMethod.GetCustomAttributesData(),
                typeof(RuntimeOverrideReturnTypeAttribute).FullName).ToList();
            if (attributes.Count == 0)
            {
                return Array.Empty<RuntimeOverrideManagedTypeReference>();
            }
            if (providerMethod.ReturnType == typeof(void))
            {
                throw new InvalidDataException(
                    $"Runtime override provider method '{providerMethod}' cannot declare return implementations "
                    + "because it returns void.");
            }

            var result = new List<RuntimeOverrideManagedTypeReference>();
            foreach (var attribute in attributes)
            {
                if (attribute.ConstructorArguments.Count == 1
                    && attribute.ConstructorArguments[0].Value is int genericParameterIndex)
                {
                    var genericParameters = providerMethod.GetGenericArguments();
                    if (genericParameterIndex < 0
                        || genericParameterIndex >= genericParameters.Length
                        || providerMethod.ReturnType != genericParameters[genericParameterIndex])
                    {
                        throw new InvalidDataException(
                            $"Runtime override provider method '{providerMethod}' declares invalid return generic "
                            + $"parameter index {genericParameterIndex}. The selected method generic parameter "
                            + "must be its declared return type.");
                    }
                    if (!result.Any(existing =>
                        existing.GenericParameterIndex == genericParameterIndex))
                    {
                        result.Add(new RuntimeOverrideManagedTypeReference
                        {
                            GenericParameterIndex = genericParameterIndex,
                        });
                    }
                    continue;
                }

                var implementationType = ReadRequiredTypeArgument(
                    attribute,
                    providerMethod.ToString());
                if (implementationType.IsInterface
                    || implementationType.IsAbstract
                    || implementationType.IsGenericParameter
                    || implementationType.HasElementType
                    || implementationType.IsGenericType && !implementationType.IsGenericTypeDefinition)
                {
                    throw new InvalidDataException(
                        $"Runtime override provider method '{providerMethod}' declares invalid concrete return "
                        + $"type pattern '{implementationType}'. Use a non-generic concrete type or an open "
                        + "generic type definition.");
                }
                if (!IsCompatibleReturnTypePattern(implementationType, providerMethod.ReturnType))
                {
                    throw new InvalidDataException(
                        $"Runtime override provider method '{providerMethod}' declares return type pattern "
                        + $"'{implementationType}', which cannot implement its declared return contract "
                        + $"'{providerMethod.ReturnType}'.");
                }

                var assemblyName = implementationType.Assembly.GetName().Name
                    ?? throw new InvalidDataException(
                        $"Runtime override return type pattern '{implementationType}' has no assembly name.");
                var typeName = implementationType.FullName
                    ?? throw new InvalidDataException(
                        $"Runtime override return type pattern '{implementationType}' has no metadata name.");
                var reference = new RuntimeOverrideManagedTypeReference
                {
                    AssemblyName = assemblyName,
                    TypeName = typeName,
                };
                if (!result.Any(existing =>
                    string.Equals(existing.AssemblyName, reference.AssemblyName, StringComparison.Ordinal)
                        && string.Equals(existing.TypeName, reference.TypeName, StringComparison.Ordinal)))
                {
                    result.Add(reference);
                }
            }
            return result;
        }

        private static bool IsCompatibleReturnTypePattern(Type pattern, Type contract)
        {
            if (!pattern.ContainsGenericParameters)
            {
                return contract.IsAssignableFrom(pattern);
            }
            if (!pattern.IsGenericTypeDefinition || !contract.IsGenericType)
            {
                return false;
            }

            var contractDefinition = contract.GetGenericTypeDefinition();
            return GetTypeHierarchy(pattern).Any(candidate =>
                candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == contractDefinition);
        }

        private static IEnumerable<Type> GetTypeHierarchy(Type type)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                yield return current;
            }
            foreach (var implementedInterface in type.GetInterfaces())
            {
                yield return implementedInterface;
            }
        }


        private static IEnumerable<CustomAttributeData> FindAttributes(
            IEnumerable<CustomAttributeData> attributes,
            string attributeTypeName)
        {
            return attributes.Where(attribute =>
                string.Equals(
                    attribute.AttributeType.FullName,
                    attributeTypeName,
                    StringComparison.Ordinal));
        }

        private static Type ReadRequiredTypeArgument(
            CustomAttributeData attribute,
            string path)
        {
            if (attribute.ConstructorArguments.Count != 1
                || attribute.ConstructorArguments[0].Value is not Type value)
            {
                throw new InvalidDataException(
                    $"Attribute '{attribute.AttributeType.FullName}' in '{path}' requires one type argument.");
            }
            return value;
        }

        private sealed record class ManagedDependencyIdentity(
            string AssemblyName,
            string Signature,
            string GenericArgumentPatterns);
    }
}
