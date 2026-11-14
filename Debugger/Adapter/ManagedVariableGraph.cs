using System.Collections.ObjectModel;

namespace FrontEndCSharp.DebugAdapter;

public class ManagedValueSnapshot
{
    public ManagedValueSnapshot()
    {
    }

    public ManagedValueSnapshot(string type, string value, int? objectId = null)
    {
        Type = type;
        Value = value;
        ObjectId = objectId;
    }

    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int? ObjectId { get; set; }
}

public sealed class ManagedNamedValueSnapshot : ManagedValueSnapshot
{
    public ManagedNamedValueSnapshot()
    {
    }

    public ManagedNamedValueSnapshot(
        string name,
        string type,
        string value,
        int? objectId = null)
        : base(type, value, objectId)
    {
        Name = name;
    }

    public string Name { get; set; } = string.Empty;

    public ManagedAssignmentTarget? Target { get; set; }
}

public sealed class ManagedAssignmentTarget
{
    public string Kind { get; set; } = string.Empty;
    public int Index { get; set; } = -1;
    public IReadOnlyList<string> Path { get; set; } = Array.Empty<string>();
}

public sealed class ManagedObjectSnapshot
{
    public ManagedObjectSnapshot()
    {
    }

    public ManagedObjectSnapshot(int id, IReadOnlyList<ManagedNamedValueSnapshot> fields)
    {
        Id = id;
        Fields = fields;
    }

    public int Id { get; set; }
    public IReadOnlyList<ManagedNamedValueSnapshot> Fields { get; set; }
        = Array.Empty<ManagedNamedValueSnapshot>();
}

public sealed record ProjectedManagedVariable(
    string Name,
    string Value,
    string Type,
    int VariablesReference,
    ManagedAssignmentTarget? Target = null);

public sealed record ManagedVariableProjection(
    int ScopeReference,
    IReadOnlyDictionary<int, IReadOnlyList<ProjectedManagedVariable>> Variables);

public static class ManagedVariableGraph
{
    public static ManagedVariableProjection Project(
        IReadOnlyList<ManagedValueSnapshot> arguments,
        IReadOnlyList<ManagedValueSnapshot> locals,
        IReadOnlyList<ManagedObjectSnapshot> objects,
        Func<int> allocateReference,
        IReadOnlyList<string> argumentNames,
        IReadOnlyList<string> localNames,
        IReadOnlyList<ManagedAssignmentTarget?>? argumentTargets = null,
        IReadOnlyList<ManagedAssignmentTarget?>? localTargets = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(locals);
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(allocateReference);
        ArgumentNullException.ThrowIfNull(argumentNames);
        ArgumentNullException.ThrowIfNull(localNames);
        if (arguments.Count != argumentNames.Count || locals.Count != localNames.Count)
        {
            throw new ArgumentException("Every managed value must have one source-level name.");
        }
        if (argumentTargets !=  null && argumentTargets.Count != arguments.Count
            || localTargets !=  null && localTargets.Count != locals.Count)
        {
            throw new ArgumentException("Every managed value must have one assignment target.");
        }

        var objectReferences = objects.ToDictionary(
            item => item.Id,
            _ => allocateReference());
        var projected = new Dictionary<int, IReadOnlyList<ProjectedManagedVariable>>();
        foreach (var item in objects)
        {
            var fields = new List<ProjectedManagedVariable>();
            foreach (var field in item.Fields)
            {
                var propertyName = TryGetAutoPropertyName(field.Name);
                if (propertyName !=  null)
                {
                    fields.Add(Project(propertyName, field, objectReferences, field.Target));
                }
                fields.Add(Project(field.Name, field, objectReferences, field.Target));
            }
            projected.Add(objectReferences[item.Id], fields);
        }

        var scopeReference = allocateReference();
        projected.Add(
            scopeReference,
            arguments.Select((value, index) => Project(
                    argumentNames[index],
                    value,
                    objectReferences,
                    argumentTargets?[index]))
                .Concat(locals.Select((value, index) => Project(
                    localNames[index],
                    value,
                    objectReferences,
                    localTargets?[index])))
                .ToList());
        return new ManagedVariableProjection(
            scopeReference,
            new ReadOnlyDictionary<int, IReadOnlyList<ProjectedManagedVariable>>(projected));
    }

    private static ProjectedManagedVariable Project(
        string name,
        ManagedValueSnapshot value,
        IReadOnlyDictionary<int, int> objectReferences,
        ManagedAssignmentTarget? target)
    {
        var variablesReference = value.ObjectId is int objectId
            && objectReferences.TryGetValue(objectId, out var reference)
                ? reference
                : 0;
        return new ProjectedManagedVariable(
            name,
            value.Value,
            value.Type,
            variablesReference,
            target);
    }

    private static string? TryGetAutoPropertyName(string fieldName)
    {
        const string suffix = ">k__BackingField";
        return fieldName.Length > suffix.Length + 1
            && fieldName[0] == '<'
            && fieldName.EndsWith(suffix, StringComparison.Ordinal)
                ? fieldName.Substring(1, fieldName.Length - suffix.Length - 1)
                : null;
    }
}
