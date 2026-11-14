using Microsoft.CodeAnalysis;

namespace FrontEndCSharp.Fecsx.Editor;

public sealed class FecsxComponentCatalog : IFecsxComponentMetadataProvider
{
    private readonly IReadOnlyList<ComponentEntry> _components;

    private FecsxComponentCatalog(IReadOnlyList<ComponentEntry> components)
    {
        _components = components;
        ComponentNames = Array.AsReadOnly(components
            .Select(component => component.MarkupName)
            .ToArray());
    }

    public IReadOnlyList<string> ComponentNames { get; }

    public static FecsxComponentCatalog Create(Compilation compilation)
    {
        if (compilation == null) throw new ArgumentNullException(nameof(compilation));

        var components = GetComponentTypes(compilation.Assembly.GlobalNamespace)
            .GroupBy(GetComponentMarkupName, StringComparer.Ordinal)
            .Select(group => CreateEntry(group.First()))
            .OrderBy(component => component.MarkupName, StringComparer.Ordinal)
            .ToArray();
        return new FecsxComponentCatalog(Array.AsReadOnly(components));
    }

    public bool TryGetProperties(
        string componentName,
        out IReadOnlyList<FecsxCompletionProperty> properties)
    {
        if (componentName == null) throw new ArgumentNullException(nameof(componentName));

        var simpleName = componentName.Split('.').Last();
        var component = _components.FirstOrDefault(candidate =>
                string.Equals(candidate.MarkupName, componentName, StringComparison.Ordinal))
            ?? _components.FirstOrDefault(candidate =>
                string.Equals(candidate.SimpleName, simpleName, StringComparison.Ordinal));
        if (component == null || !component.HasPropertyMetadata)
        {
            properties = Array.Empty<FecsxCompletionProperty>();
            return false;
        }

        properties = component.Properties;
        return true;
    }

    private static ComponentEntry CreateEntry(INamedTypeSymbol component)
    {
        var create = component.GetMembers("Create")
            .OfType<IMethodSymbol>()
            .First(method => IsComponentFactory(method));
        var action = create.Parameters[0].Type as INamedTypeSymbol;
        var propsType = action?.TypeArguments.Length == 1
            ? action.TypeArguments[0] as INamedTypeSymbol
            : null;
        var properties = propsType == null
            ? Array.Empty<FecsxCompletionProperty>()
            : propsType.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property => !property.IsStatic && property.SetMethod != null)
                .Select(property => new FecsxCompletionProperty(
                    property.Name,
                    GetValueKind(property.Type)))
                .ToArray();
        return new ComponentEntry(
            GetComponentMarkupName(component),
            component.Name,
            propsType != null,
            Array.AsReadOnly(properties));
    }

    private static IEnumerable<INamedTypeSymbol> GetComponentTypes(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            foreach (var component in GetComponentTypes(type))
            {
                yield return component;
            }
        }

        foreach (var child in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var component in GetComponentTypes(child))
            {
                yield return component;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetComponentTypes(INamedTypeSymbol type)
    {
        if (type.GetMembers("Create").OfType<IMethodSymbol>().Any(IsComponentFactory))
        {
            yield return type;
        }

        foreach (var nestedType in type.GetTypeMembers())
        {
            foreach (var component in GetComponentTypes(nestedType))
            {
                yield return component;
            }
        }
    }

    private static bool IsComponentFactory(IMethodSymbol method) =>
        method.IsStatic && method.Parameters.Length == 2 && method.Parameters[1].IsParams;

    private static string GetComponentMarkupName(INamedTypeSymbol component)
    {
        var names = new Stack<string>();
        for (var current = component; current != null; current = current.ContainingType)
        {
            names.Push(current.Name);
        }

        return string.Join(".", names);
    }

    private static FecsxCompletionValueKind GetValueKind(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return FecsxCompletionValueKind.String;
        }

        if (type.SpecialType == SpecialType.System_Boolean)
        {
            return FecsxCompletionValueKind.Boolean;
        }

        if (type.TypeKind == TypeKind.Delegate)
        {
            return FecsxCompletionValueKind.EventHandler;
        }

        switch (type.SpecialType)
        {
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
                return FecsxCompletionValueKind.Number;
            default:
                return FecsxCompletionValueKind.Expression;
        }
    }

    private sealed class ComponentEntry
    {
        public ComponentEntry(
            string markupName,
            string simpleName,
            bool hasPropertyMetadata,
            IReadOnlyList<FecsxCompletionProperty> properties)
        {
            MarkupName = markupName;
            SimpleName = simpleName;
            HasPropertyMetadata = hasPropertyMetadata;
            Properties = properties;
        }

        public string MarkupName { get; }
        public string SimpleName { get; }
        public bool HasPropertyMetadata { get; }
        public IReadOnlyList<FecsxCompletionProperty> Properties { get; }
    }
}
