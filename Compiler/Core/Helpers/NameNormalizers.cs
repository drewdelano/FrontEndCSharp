using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Compiler.Helpers
{
    public static class NameNormalizers
    {
        static string InternalGetFileSystemSafeFullTypeName(Type type, bool includeGenericTypeDefinitions = false)
        {
            var fullName = type.Name.Split("`")[0];
            if (!string.IsNullOrWhiteSpace(type.Namespace) && type.DeclaringType == null)
            {
                fullName = $"{type.Namespace}.{fullName}";
            }
            else if (type.DeclaringType !=  null)
            {
                fullName = $"{InternalGetFileSystemSafeFullTypeName(type.DeclaringType, includeGenericTypeDefinitions)}+{fullName}";
            }
            if (type.GenericTypeArguments.Any()) // we've had issues using IsGeneric here in the past
            {
                var typeDef = type.GetGenericTypeDefinition();
                fullName += $"<{string.Join(", ", typeDef.GetGenericArguments().Select(g => g.Name))}>";
            }
            else if (includeGenericTypeDefinitions && type.IsGenericTypeDefinition)
            {
                fullName += $"<{string.Join(", ", type.GetGenericArguments().Select(g => g.Name))}>";
            }
            return fullName;
        }

        public static string GetFileSystemSafeFullTypeName(Type type)
        {
            return ReplaceFileSystemInvalidChars(InternalGetFileSystemSafeFullTypeName(type));
        }

        static string ReplaceFileSystemInvalidCharsSimple(string partialPath)
        {
            return partialPath
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("`", "_")         // Generic arity: List`1 -> List_1 (matches BCL cache naming)
                .Replace("|", "_");        // NOTE: We do NOT replace "." with "_" because BCL cache filenames keep namespace dots
        }

        static string ReplaceFileSystemInvalidChars(string partialPath)
        {
            return ReplaceFileSystemInvalidCharsSimple(partialPath)
                .Replace("*", "-Ptr")      // Pointer types: System.Char* -> System.Char-Ptr
                .Replace("&", "-ByRef")    // ByRef types: System.Int32& -> System.Int32-ByRef
                .Replace("[]", "-Array");   // Array types: System.Int32[] -> System.Int32-Array
        }

        public static string GetFileSystemSafeMethodNameWithPrefix(MethodBase method)
        {
            return $"method.{GetFileSystemSafeMethodName(method)}.js";
        }

        public static string GetRuntimeOverrideFileName(string canonicalMethodSignature)
        {
            return GetHashedModuleFileName("method", canonicalMethodSignature);
        }

        public static string GetMethodModuleFileName(MethodBase method)
        {
            ArgumentNullException.ThrowIfNull(method);
            return GetHashedModuleFileName("method", GetFullyQualifiedMethodName(method));
        }

        public static string GetTypeModuleFileName(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            return GetHashedModuleFileName("type", GetFullyQualifiedTypeName(type));
        }

        private static string GetHashedModuleFileName(string prefix, string canonicalIdentity)
        {
            if (string.IsNullOrWhiteSpace(canonicalIdentity))
            {
                throw new ArgumentException(
                    "A canonical managed identity is required.",
                    nameof(canonicalIdentity));
            }

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalIdentity));
            return $"{prefix}.{Convert.ToHexString(hash).ToLowerInvariant()}.js";
        }

        public static string GetFileSystemSafeFullTypeNameWithPrefix(Type type)
        {
            return $"type.{GetFileSystemSafeFullTypeName(type)}.js";
        }

        public static string GetFileSystemSafeTypeModuleNameWithPrefix(Type type)
        {
            return $"type.{ReplaceFileSystemInvalidChars(InternalGetFileSystemSafeFullTypeName(
                type,
                includeGenericTypeDefinitions: true))}.js";
        }

        public static string GetFileSystemSafeMethodName(MethodBase method)
        {
            var typeName = GetFileSystemSafeFullTypeName(method.DeclaringType);
            var methodName = ReplaceFileSystemInvalidChars(GetMethodName(method));

            return $"{typeName}.{methodName.TrimEnd('_')}";
        }

        public static string GetMethodName(MethodBase method)
        {
            if (method.IsConstructedGenericMethod && method is MethodInfo methodInfo)
            {
                method = methodInfo.GetGenericMethodDefinition();
            }
            var paramSig = string.Join(", ", method
                .GetParameters()
                .Select(p => GetFullTypeName(p.ParameterType)))
                .ToString();

            var genericParams = "";
            if (method.ContainsGenericParameters && method is MethodInfo methodInfo2 && methodInfo2.GetGenericArguments().Any())
            {
                genericParams = $"<{string.Join(", ", method.GetGenericArguments().Select(g => g.Name))}>";
            }

            // sometimes interface methods are prefix with the interface name:
            // e.g.
            // => System.Collections.Generic.IEnumerator<System.Int32>.get_Current
            // instead of
            // => getCurrent
            var methodName = GetSimpleMethodName(method.Name);
            if (method is MethodInfo methodInfo3
                && methodInfo3.Name.Contains('.', StringComparison.Ordinal)
                && HasNormalizedSignatureCollision(methodInfo3, methodName, paramSig, genericParams))
            {
                // Explicit interface implementations normally share the interface
                // member's simple runtime identity. Preserve their qualified CLR
                // name only when that identity would collide with another method
                // on the same type (for example, a public generic property getter
                // and an explicit object-valued interface getter).
                methodName = GetExplicitInterfaceMethodName(methodInfo3, methodName);
            }
            return $"{methodName}{genericParams}({paramSig})";
        }

        private static string GetSimpleMethodName(string methodName)
        {
            var methodBits = methodName.Split('.');
            return methodBits.Length > 2 ? methodBits.Last() : methodName;
        }

        private static bool HasNormalizedSignatureCollision(
            MethodInfo method,
            string methodName,
            string parameterSignature,
            string genericParameters)
        {
            var declaringType = method.DeclaringType;
            if (declaringType == null)
            {
                return false;
            }
            if (declaringType.IsConstructedGenericType)
            {
                declaringType = declaringType.GetGenericTypeDefinition();
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static
                | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            return declaringType.GetMethods(flags).Any(candidate =>
            {
                if (candidate.MetadataToken == method.MetadataToken)
                {
                    return false;
                }
                var candidateParameters = string.Join(", ", candidate
                    .GetParameters()
                    .Select(parameter => GetFullTypeName(parameter.ParameterType)));
                var candidateGenericParameters = candidate.ContainsGenericParameters
                    && candidate.GetGenericArguments().Any()
                        ? $"<{string.Join(", ", candidate.GetGenericArguments().Select(argument => argument.Name))}>"
                        : "";
                return GetSimpleMethodName(candidate.Name) == methodName
                    && candidateParameters == parameterSignature
                    && candidateGenericParameters == genericParameters;
            });
        }

        private static string GetExplicitInterfaceMethodName(
            MethodInfo method,
            string simpleMethodName)
        {
            var declaringType = method.DeclaringType;
            if (declaringType == null)
            {
                return method.Name;
            }
            if (declaringType.IsConstructedGenericType)
            {
                declaringType = declaringType.GetGenericTypeDefinition();
            }

            var separator = method.Name.LastIndexOf('.');
            if (separator < 0)
            {
                return method.Name;
            }
            var explicitInterfaceName = method.Name[..separator];
            foreach (var interfaceType in declaringType.GetInterfaces())
            {
                var definition = interfaceType.IsConstructedGenericType
                    ? interfaceType.GetGenericTypeDefinition()
                    : interfaceType;
                var metadataName = definition.FullName;
                if (metadataName == null)
                {
                    continue;
                }
                var genericMarker = metadataName.IndexOf('`');
                if (genericMarker < 0)
                {
                    if (explicitInterfaceName == metadataName)
                    {
                        return $"{GetFullTypeName(interfaceType)}.{simpleMethodName}";
                    }
                    continue;
                }

                var genericBaseName = metadataName[..genericMarker];
                if (explicitInterfaceName == metadataName
                    || explicitInterfaceName.StartsWith(
                        $"{genericBaseName}<",
                        StringComparison.Ordinal))
                {
                    return $"{GetFullTypeName(interfaceType)}.{simpleMethodName}";
                }
            }
            return method.Name;
        }

        public static string GetFullyQualifiedMethodName(MethodBase method)
        {
            return $"{GetFullTypeName(method.DeclaringType)}.{GetMethodName(method)}";
        }

        public static string GetFullyQualifiedTypeName(Type type)
        {
            return GetFullTypeName(type);
        }

        public static string GetFieldStorageKey(FieldInfo field)
        {
            ArgumentNullException.ThrowIfNull(field);
            var declaringType = field.DeclaringType
                ?? throw new ArgumentException("A managed field must have a declaring type.", nameof(field));
            if (declaringType.IsConstructedGenericType)
            {
                declaringType = declaringType.GetGenericTypeDefinition();
            }
            return $"$field:{GetFullTypeName(declaringType)}:{field.Name}:{field.MetadataToken:X8}";
        }


        // TODO: these need to handle generics, I'm pretty sure
        public static string GetShortTypeName(Type type)
        {
            return type.Name;
        }

        // TODO: these need to handle generics, I'm pretty sure
        public static string GetFullTypeName(Type type)
        {
            if (type.IsByRef)
            {
                return $"{GetFullTypeName(type.GetElementType()!)}&";
            }
            if (type.IsArray)
            {
                return $"{GetFullTypeName(type.GetElementType()!)}[]";
            }
            if (type.IsGenericTypeParameter || type.IsGenericParameter)
            {
                return type.Name; // Name = "TTT", FullName = "" -- generic params are weird
            }
            var genericTypeParams = "";
            if (type.IsGenericType)
            {
                type = type.GetGenericTypeDefinition();
                var genericTypeParamsRaw = ((TypeInfo)type).GenericTypeParameters;
                genericTypeParams = $"[{string.Join(", ", genericTypeParamsRaw.Select(gtp => GetFullTypeName(gtp)))}]";
            }
            var rawNs = type.Namespace;
            var ns = string.IsNullOrEmpty(rawNs) ? "" : $"{rawNs}.";
            var name = type.Name;
            if (!string.IsNullOrEmpty(type.FullName))
            {
                name = type.FullName.Split('.').Last();
            }
            return $"{ns}{name}{genericTypeParams}";
        }
    }
}
