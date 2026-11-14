using System;
using System.Reflection;

namespace Compiler.Helpers
{
    internal readonly record struct MetadataModuleIdentity(
        string AssemblyIdentity,
        string ModuleScopeName,
        Guid ModuleVersionId);

    internal readonly record struct MetadataDefinitionIdentity(
        string AssemblyIdentity,
        string ModuleScopeName,
        Guid ModuleVersionId,
        int MetadataToken)
    {
        public MetadataModuleIdentity ModuleIdentity =>
            new(AssemblyIdentity, ModuleScopeName, ModuleVersionId);

        public override string ToString() =>
            $"{AssemblyIdentity}; module '{ModuleScopeName}' "
            + $"({ModuleVersionId:D}); token 0x{MetadataToken:X8}";
    }

    internal static class MetadataIdentity
    {
        public static MetadataDefinitionIdentity GetDefinition(MemberInfo member)
        {
            ArgumentNullException.ThrowIfNull(member);
            return Create(member.Module, member.MetadataToken);
        }

        public static MetadataDefinitionIdentity GetTypeDefinition(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);
            while (type.HasElementType)
            {
                type = type.GetElementType()!;
            }
            if (type.IsConstructedGenericType)
            {
                type = type.GetGenericTypeDefinition();
            }
            return Create(type.Module, type.MetadataToken);
        }

        private static MetadataDefinitionIdentity Create(Module module, int metadataToken)
        {
            var assemblyName = module.Assembly.GetName();
            var assemblyIdentity = assemblyName.FullName
                ?? assemblyName.Name
                ?? throw new InvalidOperationException(
                    $"Module '{module.ScopeName}' has no assembly identity.");
            return new MetadataDefinitionIdentity(
                assemblyIdentity,
                module.ScopeName,
                module.ModuleVersionId,
                metadataToken);
        }
    }
}
