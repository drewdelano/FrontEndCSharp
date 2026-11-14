using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using FrontEndCSharp.RuntimeOverrides;
using Newtonsoft.Json.Linq;

namespace Compiler.Helpers;

// Run in the compiler's target runtime: PowerShell's CoreLib may be .NET Framework
// or a newer .NET version, neither of which describes the companion's target ABI.
internal static class RuntimeOverrideProviderGenerator
{
    private static readonly Regex Registration = new Regex(
        "globalThis\\.DotNet\\.Methods\\[['\"](?<signature>[^'\"]+)['\"]\\]",
        RegexOptions.CultureInvariant);

    public static void Generate(string resourceDirectory, string outputPath)
    {
        var companionConfiguration = ReadCompanionConfiguration(resourceDirectory);
        var companionBodies = companionConfiguration.Bodies;
        var companionParameterAccesses = companionConfiguration.ParameterAccesses;
        var companionGenericParameterAccesses = companionConfiguration.GenericParameterAccesses;
        var signatures = Directory.GetFiles(resourceDirectory, "*.js")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .Select(resource =>
            {
                var registrations = Registration.Matches(File.ReadAllText(resource));
                if (registrations.Count != 1)
                {
                    throw new InvalidOperationException($"Resource '{resource}' must contain exactly one managed registration.");
                }
                return new ResourceRegistration(
                    resource,
                    registrations[0].Groups["signature"].Value);
            }).ToArray();
        var members = new Dictionary<string, MethodBase>(StringComparer.Ordinal);
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        foreach (var type in typeof(object).Assembly.GetTypes())
        {
            var prefix = NameNormalizers.GetFullyQualifiedTypeName(type) + ".";
            if (!signatures.Any(entry => entry.Signature.StartsWith(prefix, StringComparison.Ordinal))) { continue; }
            foreach (var member in type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags)))
            {
                string signature;
                try { signature = NameNormalizers.GetFullyQualifiedMethodName(member); }
                // Some unrelated BCL members have function-pointer signatures that
                // reflection cannot normalize. Requested unresolved members still fail below.
                catch (BadImageFormatException) { continue; }
                catch (NotSupportedException) { continue; }
                if (!members.ContainsKey(signature) || member.IsPublic) { members[signature] = member; }
            }
        }

        var entries = new List<ProviderEntry>();
        foreach (var entry in signatures)
        {
            var signature = entry.Signature;
            if (!members.TryGetValue(signature, out var member))
            {
                var parameterStart = signature.IndexOf('(');
                var memberStart = signature.LastIndexOf('.', parameterStart) + 1;
                var memberEnd = signature.IndexOfAny(new[] { '<', '(' }, memberStart);
                var memberName = memberEnd < 0
                    ? signature[memberStart..]
                    : signature[memberStart..memberEnd];
                var typePrefix = signature[..memberStart];
                var candidates = members.Keys
                    .Where(candidate => candidate.StartsWith(typePrefix + memberName, StringComparison.Ordinal))
                    .OrderBy(candidate => candidate, StringComparer.Ordinal)
                    .ToArray();
                var available = candidates.Length == 0
                    ? string.Empty
                    : $" Available overloads: {string.Join(", ", candidates)}.";
                throw new InvalidOperationException(
                    $"Resource '{entry.Resource}' targets '{signature}', which does not exist in {typeof(object).Assembly.GetName().Name}.{available}");
            }
            entries.Add(new ProviderEntry(signature, member));
        }
        var configuredSignatures = companionBodies.Keys
            .Concat(companionParameterAccesses.Keys)
            .Concat(companionGenericParameterAccesses.Keys)
            .Distinct(StringComparer.Ordinal);
        var unknownCompanionBodies = configuredSignatures
            .Except(entries.Select(entry => entry.Signature), StringComparer.Ordinal)
            .OrderBy(signature => signature, StringComparer.Ordinal)
            .ToArray();
        if (unknownCompanionBodies.Length != 0)
        {
            throw new InvalidDataException(
                $"Companion provider bodies in '{resourceDirectory}' target registrations that do not exist: "
                + string.Join(", ", unknownCompanionBodies));
        }

        var lines = new List<string>
        {
            "// <auto-generated />",
            "using FrontEndCSharp.RuntimeOverrides;",
            "using System.Runtime.CompilerServices;", "",
            "namespace FrontEndCSharp.SystemPrivateCoreLibRuntimeOverrides;", ""
        };
        foreach (var group in entries.GroupBy(entry => entry.Member.DeclaringType!)
            .OrderBy(group => group.Key.FullName, StringComparer.Ordinal))
        {
            var targetType = group.Key;
            var typeArguments = targetType.GetGenericArguments();
            lines.Add($"[RuntimeOverrideType(typeof({FormatType(targetType, openGeneric: true)}))]");
            lines.Add($"internal static class {Regex.Replace(targetType.FullName!, "[^A-Za-z0-9_]", "_")}_Overrides{GenericSuffix(typeArguments)}");
            lines.AddRange(GenericConstraints(typeArguments).Select(constraint => "    " + constraint));
            lines.Add("{");
            var providerSignatures = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in group.OrderBy(entry => entry.Signature, StringComparer.Ordinal))
            {
                var member = entry.Member;
                var constructor = member is ConstructorInfo;
                var instance = !constructor && !member.IsStatic;
                var attributes = new List<string>();
                if (constructor) { attributes.Add("MemberKind = RuntimeOverrideMemberKind.Constructor"); }
                else if (instance) { attributes.Add("MemberKind = RuntimeOverrideMemberKind.InstanceMethod"); }
                var returnType = constructor ? targetType : ((MethodInfo)member).ReturnType;
                var name = constructor ? "Create" : member.Name.Split('.').Last();
                var methodArguments = member is MethodInfo method && method.IsGenericMethodDefinition
                    ? method.GetGenericArguments() : Type.EmptyTypes;
                var genericParameterAccesses = companionGenericParameterAccesses.TryGetValue(
                    entry.Signature,
                    out var configuredGenericParameterAccesses)
                    ? configuredGenericParameterAccesses
                    : EmptyParameterAccesses;
                var validGenericParameterAccessKeys = methodArguments
                    .Select((_, index) => index.ToString(
                        System.Globalization.CultureInfo.InvariantCulture))
                    .ToHashSet(StringComparer.Ordinal);
                var invalidGenericParameterAccessKeys = genericParameterAccesses.Keys
                    .Except(validGenericParameterAccessKeys, StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
                if (invalidGenericParameterAccessKeys.Length != 0)
                {
                    throw new InvalidDataException(
                        $"Companion generic parameter access for '{entry.Signature}' uses invalid parameter keys: "
                        + string.Join(", ", invalidGenericParameterAccessKeys));
                }
                var genericSuffix = methodArguments.Length == 0
                    ? string.Empty
                    : $"<{string.Join(", ", methodArguments.Select((argument, index) =>
                        $"{FormatManagedTypeAccess(genericParameterAccesses, index.ToString(System.Globalization.CultureInfo.InvariantCulture))}{argument.Name}"))}>";
                var parameters = new List<string>();
                var parameterTypes = new List<string>();
                var parameterAccesses = companionParameterAccesses.TryGetValue(
                    entry.Signature,
                    out var configuredParameterAccesses)
                    ? configuredParameterAccesses
                    : EmptyParameterAccesses;
                var validParameterAccessKeys = new HashSet<string>(StringComparer.Ordinal);
                if (instance)
                {
                    const string receiverKey = "this";
                    validParameterAccessKeys.Add(receiverKey);
                    parameters.Add($"{FormatManagedTypeAccess(parameterAccesses, receiverKey)}{FormatType(targetType)} @this");
                    parameterTypes.Add(FormatType(targetType));
                }
                foreach (var parameter in member.GetParameters())
                {
                    var parameterKey = parameter.Position.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);
                    validParameterAccessKeys.Add(parameterKey);
                    var prefix = "";
                    if (parameter.ParameterType.IsByRef)
                    {
                        prefix += parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
                    }
                    parameters.Add($"{FormatManagedTypeAccess(parameterAccesses, parameterKey)}{prefix}{FormatType(parameter.ParameterType)} arg{parameter.Position}");
                    parameterTypes.Add(FormatType(parameter.ParameterType));
                }
                var invalidParameterAccessKeys = parameterAccesses.Keys
                    .Except(validParameterAccessKeys, StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
                if (invalidParameterAccessKeys.Length != 0)
                {
                    throw new InvalidDataException(
                        $"Companion parameter access for '{entry.Signature}' uses invalid parameter keys: "
                        + string.Join(", ", invalidParameterAccessKeys));
                }
                string ProviderSignature(string candidate) => $"{candidate}{genericSuffix}({string.Join(", ", parameterTypes)})";
                if (!providerSignatures.Add(ProviderSignature(name)))
                {
                    var targetName = constructor ? ".ctor" : member.Name;
                    var suffix = instance ? "Instance" : constructor ? "Constructor" : "Static";
                    var providerName = constructor ? "Create" : member.Name.Split('.').Last();
                    name = $"{providerName}__{suffix}";
                    for (var ordinal = 2; !providerSignatures.Add(ProviderSignature(name)); ordinal++)
                    {
                        name = $"{providerName}__{suffix}{ordinal}";
                    }
                    attributes.Add($"TargetMemberName = \"{targetName}\"");
                }
                lines.Add(attributes.Count == 0 ? "    [JavaScriptOverride]"
                    : $"    [JavaScriptOverride({string.Join(", ", attributes)})]");
                var body = companionBodies.TryGetValue(entry.Signature, out var companionBody)
                    ? companionBody
                    : DependencyBody(entry.Signature);
                if (body == null && (targetType.IsGenericTypeDefinition || methodArguments.Length != 0))
                {
                    body = new[] { "throw null!;" };
                }
                var hidesObjectMember = targetType == typeof(object) && name == nameof(object.Equals)
                    ? "new "
                    : string.Empty;
                var declaration = $"    internal static {hidesObjectMember}{FormatType(returnType)} {name}{genericSuffix}({string.Join(", ", parameters)})";
                if (body == null)
                {
                    lines.Add("    [MethodImpl(MethodImplOptions.InternalCall)]");
                    lines.Add(declaration.Replace("internal static ", "internal static extern ") + ";");
                }
                else
                {
                    lines.Add(declaration);
                    lines.Add("    {");
                    lines.AddRange(body.Select(statement => "        " + statement));
                    lines.Add("    }");
                }
                lines.Add("");
            }
            lines.Add("}");
            lines.Add("");
        }
        File.WriteAllLines(outputPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Generated {entries.Count} typed runtime override declarations in '{outputPath}'.");
    }

    private static string GenericSuffix(Type[] arguments) => arguments.Length == 0
        ? "" : $"<{string.Join(", ", arguments.Select(argument => argument.Name))}>";

    private static IEnumerable<string> GenericConstraints(IEnumerable<Type> arguments)
    {
        foreach (var argument in arguments)
        {
            var attributes = argument.GenericParameterAttributes;
            var special = attributes & GenericParameterAttributes.SpecialConstraintMask;
            var constraints = new List<string>();
            if ((special & GenericParameterAttributes.ReferenceTypeConstraint) != 0)
            {
                constraints.Add("class");
            }
            else if ((special & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0)
            {
                constraints.Add("struct");
            }
            constraints.AddRange(argument.GetGenericParameterConstraints()
                .Where(constraint => constraint != typeof(ValueType))
                .Select(constraint => FormatType(constraint)));
            if ((special & GenericParameterAttributes.DefaultConstructorConstraint) != 0
                && (special & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0)
            {
                constraints.Add("new()");
            }
            if (constraints.Count != 0)
            {
                yield return $"where {argument.Name} : {string.Join(", ", constraints)}";
            }
        }
    }

    private static string FormatType(Type type, bool openGeneric = false)
    {
        if (type.IsByRef) { return FormatType(type.GetElementType()!); }
        if (type.IsArray) { return FormatType(type.GetElementType()!) + "[]"; }
        if (type.IsGenericParameter) { return type.Name; }
        if (type == typeof(void)) { return "void"; }
        if (!type.IsGenericType) { return "global::" + type.FullName!.Replace('+', '.'); }
        return FormatNamedType(type.GetGenericTypeDefinition(), type.GetGenericArguments(), openGeneric);
    }

    private static string FormatNamedType(Type definition, Type[] arguments, bool openGeneric)
    {
        var declaring = definition.DeclaringType;
        var declaringCount = declaring?.GetGenericArguments().Length ?? 0;
        var prefix = declaring == null
            ? "global::" + (string.IsNullOrEmpty(definition.Namespace) ? "" : definition.Namespace + ".")
            : FormatNamedType(declaring, arguments.Take(declaringCount).ToArray(), openGeneric) + ".";
        var parts = definition.Name.Split('`');
        if (parts.Length == 1) { return prefix + parts[0]; }
        var arity = int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
        var contents = openGeneric ? new string(',', arity - 1)
            : string.Join(", ", arguments.Skip(declaringCount).Take(arity).Select(argument => FormatType(argument)));
        return $"{prefix}{parts[0]}<{contents}>";
    }

    private static string[] DependencyBody(string signature)
    {
        return signature switch
        {
        "System.Threading.Tasks.Task.WhenAll(System.Collections.Generic.IEnumerable`1[T])" or
        "System.Threading.Tasks.Task.WhenAll<TResult>(System.Collections.Generic.IEnumerable`1[T])" or
        "System.Threading.Tasks.Task.WhenAny(System.Collections.Generic.IEnumerable`1[T])" or
        "System.Threading.Tasks.Task.WhenAny<TResult>(System.Collections.Generic.IEnumerable`1[T])" => new[] {
            "var enumerator = arg0.GetEnumerator();",
            "_ = enumerator.Current;",
            "_ = enumerator.MoveNext();",
            "enumerator.Dispose();",
            "throw null!;" },
        "System.Nullable`1[T].get_Value()" => new[] { "throw new global::System.InvalidOperationException(default(string));" },
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Create()" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)null!).MoveNext();", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start<TStateMachine>(TStateMachine&)" or
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].Start<TStateMachine>(TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg0!).MoveNext();", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" or
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg1!).MoveNext();",
            "((global::System.Runtime.CompilerServices.INotifyCompletion)(object)arg0!).OnCompleted(null!);", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" or
        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[TResult].AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg1!).MoveNext();", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.Create()" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)null!).MoveNext();", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.MoveNext<TStateMachine>(TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg0!).MoveNext();", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.AwaitOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg1!).MoveNext();",
            "((global::System.Runtime.CompilerServices.INotifyCompletion)(object)arg0!).OnCompleted(null!);", "throw null!;" },
        "System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(TAwaiter&, TStateMachine&)" => new[] {
            "((global::System.Runtime.CompilerServices.IAsyncStateMachine)(object)arg1!).MoveNext();", "throw null!;" },
        "System.Threading.Tasks.Task.ConfigureAwait(System.Threading.Tasks.ConfigureAwaitOptions)" or
        "System.Threading.Tasks.Task.Delay(System.Int32)" or
        "System.Threading.Tasks.Task`1[TResult].ConfigureAwait(System.Threading.Tasks.ConfigureAwaitOptions)" => new[] {
            "throw new global::System.ArgumentOutOfRangeException(default(string));" },
        _ => null
        };
    }

    private static string FormatManagedTypeAccess(
        IReadOnlyDictionary<string, ManagedTypeAccess> accesses,
        string key)
    {
        if (!accesses.TryGetValue(key, out var access))
        {
            return string.Empty;
        }

        var flags = Enum.GetValues(typeof(ManagedTypeAccess))
            .Cast<ManagedTypeAccess>()
            .Where(flag => flag != ManagedTypeAccess.None && (access & flag) == flag)
            .Select(flag => $"ManagedTypeAccess.{flag}");
        return $"[ManagedTypeAccess({string.Join(" | ", flags)})] ";
    }

    private static CompanionConfiguration ReadCompanionConfiguration(
        string resourceDirectory)
    {
        var path = Path.Combine(resourceDirectory, "provider-bodies.json");
        if (!File.Exists(path))
        {
            return new CompanionConfiguration(
                new Dictionary<string, string[]>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>>(StringComparer.Ordinal));
        }

        var document = JObject.Parse(File.ReadAllText(path));
        if (document.Value<int?>("version") != 1
            || document["bodies"] is not JObject bodies)
        {
            throw new InvalidDataException(
                $"Companion provider bodies '{path}' must contain version 1 and a bodies object.");
        }

        var result = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var property in bodies.Properties())
        {
            if (property.Value is not JArray statements
                || statements.Count == 0
                || statements.Any(statement => statement.Type != JTokenType.String
                    || string.IsNullOrWhiteSpace(statement.Value<string>())))
            {
                throw new InvalidDataException(
                    $"Companion provider body '{property.Name}' in '{path}' must be a non-empty string array.");
            }
            result.Add(property.Name, statements.Values<string>().ToArray()!);
        }

        var genericParameterAccesses = ReadAccesses(document, "genericParameterAccesses", path);
        var parameterAccesses = ReadAccesses(document, "parameterAccesses", path);
        return new CompanionConfiguration(result, genericParameterAccesses, parameterAccesses);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>> ReadAccesses(
        JObject document,
        string propertyName,
        string path)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>>(
            StringComparer.Ordinal);
        if (document[propertyName] is JToken accessToken)
        {
            if (accessToken is not JObject accessObject)
            {
                throw new InvalidDataException(
                    $"Companion provider {propertyName} in '{path}' must be an object.");
            }
            foreach (var signature in accessObject.Properties())
            {
                if (signature.Value is not JObject memberAccesses || !memberAccesses.Properties().Any())
                {
                    throw new InvalidDataException(
                        $"Companion provider {propertyName} '{signature.Name}' in '{path}' must be a non-empty object.");
                }
                var accesses = new Dictionary<string, ManagedTypeAccess>(StringComparer.Ordinal);
                foreach (var parameter in memberAccesses.Properties())
                {
                    var accessName = parameter.Value.Type == JTokenType.String
                        ? parameter.Value.Value<string>()
                        : null;
                    if (string.IsNullOrWhiteSpace(parameter.Name)
                        || !Enum.TryParse(accessName, ignoreCase: false, out ManagedTypeAccess access)
                        || access == ManagedTypeAccess.None
                        || (access & ~AllManagedTypeAccess) != 0)
                    {
                        throw new InvalidDataException(
                            $"Companion provider {propertyName} '{signature.Name}' parameter "
                            + $"'{parameter.Name}' in '{path}' is invalid.");
                    }
                    accesses.Add(parameter.Name, access);
                }
                result.Add(signature.Name, accesses);
            }
        }
        return result;
    }

    private static readonly IReadOnlyDictionary<string, ManagedTypeAccess> EmptyParameterAccesses =
        new Dictionary<string, ManagedTypeAccess>(StringComparer.Ordinal);

    private static readonly ManagedTypeAccess AllManagedTypeAccess = Enum.GetValues(
            typeof(ManagedTypeAccess))
        .Cast<ManagedTypeAccess>()
        .Aggregate(ManagedTypeAccess.None, (current, flag) => current | flag);

    private sealed record class CompanionConfiguration(
        IReadOnlyDictionary<string, string[]> Bodies,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>> GenericParameterAccesses,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ManagedTypeAccess>> ParameterAccesses);

    private sealed record class ResourceRegistration(string Resource, string Signature);

    private sealed record class ProviderEntry(string Signature, MethodBase Member);
}
