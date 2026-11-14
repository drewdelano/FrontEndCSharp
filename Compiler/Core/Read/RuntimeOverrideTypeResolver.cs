using Compiler.DTOs;
using Compiler.Helpers;
using FrontEndCSharp.RuntimeOverrides;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.Read
{
    internal static class RuntimeOverrideTypeResolver
    {
        internal static IReadOnlyCollection<Type> FindRuntimeOverrideDependencyImplementations(
            ReaderContext ctx,
            MethodBase runtimeOverrideMethod,
            Type contractType)
        {
            var implementations = new HashSet<Type>();
            foreach (var constructor in ctx.RuntimeOverrides.GetDependencies(runtimeOverrideMethod)
                .OfType<ConstructorInfo>())
            {
                var declaringType = constructor.DeclaringType;
                if (declaringType == null || declaringType.IsInterface || declaringType.IsAbstract)
                {
                    continue;
                }
                if (TryCloseDependencyType(declaringType, contractType, out var implementationType)
                    && contractType.IsAssignableFrom(implementationType))
                {
                    implementations.Add(implementationType);
                }
            }
            return implementations;
        }

        internal static IReadOnlyCollection<Type> FindRuntimeOverrideReturnImplementations(
            ReaderContext ctx,
            MethodBase runtimeOverrideMethod,
            Type contractType)
        {
            var implementations = new HashSet<Type>();
            foreach (var pattern in ctx.RuntimeOverrides.GetReturnTypePatterns(runtimeOverrideMethod))
            {
                if (pattern.IsInterface || pattern.IsAbstract || pattern.IsGenericParameter)
                {
                    continue;
                }
                if (!TryCloseDependencyType(pattern, contractType, out var implementationType)
                    || !contractType.IsAssignableFrom(implementationType))
                {
                    throw new InvalidDataException(
                        $"Runtime override '{NameNormalizers.GetFullyQualifiedMethodName(runtimeOverrideMethod)}' "
                        + $"declares return type pattern '{pattern}' that cannot be closed for contract "
                        + $"'{contractType}'.");
                }
                implementations.Add(implementationType);
            }
            if (implementations.Count == 0
                && (contractType.IsInterface || contractType.IsAbstract))
            {
                implementations.UnionWith(FindRuntimeOverrideDependencyImplementations(
                    ctx,
                    runtimeOverrideMethod,
                    contractType));
            }
            return implementations;
        }

        private static bool TryCloseDependencyType(
            Type dependencyType,
            Type contractType,
            out Type closedType)
        {
            if (!dependencyType.IsGenericType)
            {
                closedType = dependencyType;
                return !dependencyType.ContainsGenericParameters;
            }

            var template = dependencyType;
            foreach (var pattern in GetTypeHierarchy(template))
            {
                foreach (var contractShape in new[] { contractType }.Concat(contractType.GetInterfaces()))
                {
                    var substitutions = new Dictionary<Type, Type>();
                    if (!TryUnifyTypePattern(
                        pattern,
                        contractShape,
                        substitutions,
                        allowObjectPlaceholder: false))
                    {
                        continue;
                    }
                    try
                    {
                        var candidate = SubstituteTypePattern(template, substitutions);
                        if (!candidate.ContainsGenericParameters
                            && contractType.IsAssignableFrom(candidate))
                        {
                            closedType = candidate;
                            return true;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // A dependency may have constraints that the requested contract
                        // does not satisfy. It is not a candidate implementation.
                    }
                }
            }
            closedType = null;
            return false;
        }

        private static Type SubstituteTypePattern(
            Type pattern,
            IReadOnlyDictionary<Type, Type> substitutions)
        {
            if (substitutions.TryGetValue(pattern, out var substitution))
            {
                return substitution;
            }
            if (pattern.IsArray)
            {
                var elementType = SubstituteTypePattern(pattern.GetElementType()!, substitutions);
                return pattern.GetArrayRank() == 1
                    ? elementType.MakeArrayType()
                    : elementType.MakeArrayType(pattern.GetArrayRank());
            }
            if (!pattern.IsGenericType)
            {
                return pattern;
            }
            var definition = pattern.GetGenericTypeDefinition();
            return definition.MakeGenericType(pattern.GetGenericArguments()
                .Select(argument => SubstituteTypePattern(argument, substitutions))
                .ToArray());
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

        internal static bool TryUnifyTypePattern(
            Type pattern,
            Type actual,
            IDictionary<Type, Type> substitutions,
            bool allowObjectPlaceholder = false)
        {
            if (pattern.IsGenericParameter
                || allowObjectPlaceholder && pattern == typeof(object))
            {
                if (substitutions.TryGetValue(pattern, out var existing))
                {
                    return existing == actual;
                }
                substitutions.Add(pattern, actual);
                return true;
            }
            if (pattern.IsArray)
            {
                return actual.IsArray
                    && pattern.GetArrayRank() == actual.GetArrayRank()
                    && TryUnifyTypePattern(
                        pattern.GetElementType()!,
                        actual.GetElementType()!,
                        substitutions,
                        allowObjectPlaceholder: true);
            }
            if (pattern.IsGenericType)
            {
                if (!actual.IsGenericType
                    || pattern.GetGenericTypeDefinition() != actual.GetGenericTypeDefinition())
                {
                    return false;
                }
                var patternArguments = pattern.GetGenericArguments();
                var actualArguments = actual.GetGenericArguments();
                for (var index = 0; index < patternArguments.Length; index++)
                {
                    if (!TryUnifyTypePattern(
                        patternArguments[index],
                        actualArguments[index],
                        substitutions,
                        allowObjectPlaceholder: true))
                    {
                        return false;
                    }
                }
                return true;
            }
            return pattern == actual;
        }

    }
}
