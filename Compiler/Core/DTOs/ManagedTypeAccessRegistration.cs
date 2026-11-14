using System;
using FrontEndCSharp.RuntimeOverrides;

namespace Compiler.DTOs
{
    public sealed record class ManagedTypeAccessRegistration(
        Type Type,
        ManagedTypeAccess Access,
        string AttributeType,
        bool IsDirectValueTypeContract,
        string ExcludedTypes);
}
