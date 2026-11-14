using Compiler.Helpers;
using Compiler.Read;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Compiler.Debugging
{
    public static class DebugManifestBuilder
    {
        private static readonly Guid Sha1DocumentHash = new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460");
        private static readonly Guid Sha256DocumentHash = new Guid("8829d00f-11b8-4213-878b-770e8597ac16");
        private static readonly Guid CSharpLanguage = new Guid("3f5162f8-07c6-11d3-9053-00c04fa302a1");
        private static readonly Guid EmbeddedSourceKind = new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe");
        private static readonly Guid StateMachineHoistedLocalScopesKind = new Guid("6da9a61e-f8c7-4874-be62-68bc5630df71");

        public static bool HasMatchingPortablePdb(string assemblyPath)
        {
            using var assemblyStream = new FileStream(
                assemblyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(assemblyStream);
            if (!peReader.HasMetadata)
            {
                return false;
            }
            if (!peReader.TryOpenAssociatedPortablePdb(
                assemblyPath,
                OpenPdb,
                out var pdbProvider,
                out _))
            {
                return false;
            }
            pdbProvider.Dispose();
            return true;
        }

        public static DebugManifest Build(IEnumerable<MethodDecoder> methodDecoders)
            => Build(methodDecoders, entryModule: null);

        internal static DebugManifest Build(
            IEnumerable<MethodDecoder> methodDecoders,
            Module entryModule)
        {
            if (methodDecoders == null)
            {
                throw new ArgumentNullException(nameof(methodDecoders));
            }

            var decoders = methodDecoders
                .Where(decoder => decoder?.MethodBase?.Module !=  null)
                .GroupBy(decoder => new
                {
                    ModulePath = PathLocator.GetModulePath(decoder.MethodBase.Module),
                    ModuleId = decoder.MethodBase.Module.ModuleVersionId
                })
                .OrderBy(group => group.Key.ModuleId)
                .ToList();
            if (decoders.Count == 0)
            {
                throw new InvalidOperationException("A debug manifest requires at least one decoded method.");
            }

            var manifest = new DebugManifest();
            foreach (var moduleGroup in decoders)
            {
                manifest.Modules.Add(BuildModule(
                    moduleGroup.Key.ModulePath,
                    moduleGroup.Key.ModuleId,
                    entryModule !=  null && moduleGroup.Key.ModuleId == entryModule.ModuleVersionId,
                    moduleGroup
                        .GroupBy(decoder => decoder.MethodBase.MetadataToken)
                        .Select(group => group.First())
                        .OrderBy(decoder => decoder.MethodBase.MetadataToken)
                        .ToList()));
            }

            manifest.BuildId = ComputeBuildId(manifest);
            return manifest;
        }

        private static DebugModule BuildModule(
            string assemblyPath,
            Guid expectedModuleId,
            bool isEntryModule,
            IReadOnlyList<MethodDecoder> decoders)
        {
            using var assemblyStream = new FileStream(
                assemblyPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var peReader = new PEReader(assemblyStream);
            if (!peReader.HasMetadata)
            {
                throw new BadImageFormatException($"'{assemblyPath}' does not contain CLI metadata.");
            }

            var assemblyMetadata = peReader.GetMetadataReader();
            var moduleDefinition = assemblyMetadata.GetModuleDefinition();
            var actualModuleId = assemblyMetadata.GetGuid(moduleDefinition.Mvid);
            if (actualModuleId != expectedModuleId)
            {
                throw new InvalidDataException(
                    $"The loaded module MVID '{expectedModuleId:D}' does not match '{actualModuleId:D}' in '{assemblyPath}'.");
            }

            if (!peReader.TryOpenAssociatedPortablePdb(
                assemblyPath,
                OpenPdb,
                out var pdbProvider,
                out _))
            {
                throw new InvalidDataException(
                    $"A matching Portable PDB could not be found for '{assemblyPath}'.");
            }

            using (pdbProvider)
            {
                var pdbReader = pdbProvider.GetMetadataReader();
                var module = new DebugModule
                {
                    ModuleId = actualModuleId.ToString("D"),
                    AssemblyName = Path.GetFileName(assemblyPath),
                    IsEntryModule = isEntryModule,
                    PdbId = ToHex(pdbReader.DebugMetadataHeader!.Id),
                    IsEmbeddedPdb = peReader.ReadDebugDirectory()
                        .Any(entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb)
                };

                var embeddedDocuments = GetEmbeddedDocuments(pdbReader);
                foreach (var documentHandle in pdbReader.Documents)
                {
                    var document = pdbReader.GetDocument(documentHandle);
                    var hashAlgorithm = document.HashAlgorithm.IsNil
                        ? Guid.Empty
                        : pdbReader.GetGuid(document.HashAlgorithm);
                    var language = document.Language.IsNil
                        ? Guid.Empty
                        : pdbReader.GetGuid(document.Language);
                    module.Documents.Add(new DebugDocument
                    {
                        Id = MetadataTokens.GetRowNumber(documentHandle),
                        Path = pdbReader.GetString(document.Name),
                        Language = GetLanguageName(language),
                        ChecksumAlgorithm = GetHashAlgorithmName(hashAlgorithm),
                        Checksum = document.Hash.IsNil
                            ? string.Empty
                            : ToHex(pdbReader.GetBlobBytes(document.Hash)),
                        HasEmbeddedSource = embeddedDocuments.Contains(documentHandle)
                    });
                }

                foreach (var decoder in decoders)
                {
                    module.Methods.Add(BuildMethod(assemblyMetadata, pdbReader, decoder));
                }

                return module;
            }
        }

        private static DebugMethod BuildMethod(
            MetadataReader assemblyMetadata,
            MetadataReader pdbReader,
            MethodDecoder decoder)
        {
            var methodToken = decoder.MethodBase.MetadataToken;
            var methodHandle = MetadataTokens.MethodDefinitionHandle(methodToken & 0x00FFFFFF);
            var methodDefinition = assemblyMetadata.GetMethodDefinition(methodHandle);
            var methodDebugHandle = MetadataTokens.MethodDebugInformationHandle(
                MetadataTokens.GetRowNumber(methodHandle));
            var methodDebugInformation = pdbReader.GetMethodDebugInformation(methodDebugHandle);
            var instructionMap = decoder.InstructionBlocks
                .SelectMany((block, blockIndex) => block.Instructions.Select(
                    (instruction, instructionIndex) => new DebugInstructionLocation
                    {
                        IlOffset = instruction.ILOffset,
                        BlockIndex = blockIndex,
                        InstructionIndex = instructionIndex
                    }))
                .OrderBy(location => location.IlOffset)
                .ToList();
            var locationsByOffset = instructionMap.ToDictionary(location => location.IlOffset);
            var method = new DebugMethod
            {
                MethodToken = methodToken,
                Name = $"{decoder.MethodBase.DeclaringType?.FullName}.{decoder.MethodBase.Name}",
                HasThis = !decoder.MethodBase.IsStatic,
                Instructions = instructionMap
            };

            foreach (var parameterHandle in methodDefinition.GetParameters())
            {
                var parameter = assemblyMetadata.GetParameter(parameterHandle);
                if (parameter.SequenceNumber == 0)
                {
                    continue;
                }
                method.Parameters.Add(new DebugParameter
                {
                    Index = parameter.SequenceNumber - 1,
                    Name = parameter.Name.IsNil
                        ? string.Empty
                        : assemblyMetadata.GetString(parameter.Name)
                });
            }

            var currentDocument = methodDebugInformation.Document;
            foreach (var sequencePoint in methodDebugInformation.GetSequencePoints())
            {
                if (!sequencePoint.Document.IsNil)
                {
                    currentDocument = sequencePoint.Document;
                }
                locationsByOffset.TryGetValue(sequencePoint.Offset, out var location);
                method.SequencePoints.Add(new DebugSequencePoint
                {
                    IlOffset = sequencePoint.Offset,
                    DocumentId = currentDocument.IsNil
                        ? null
                        : MetadataTokens.GetRowNumber(currentDocument),
                    IsHidden = sequencePoint.IsHidden,
                    StartLine = sequencePoint.IsHidden ? 0 : sequencePoint.StartLine,
                    StartColumn = sequencePoint.IsHidden ? 0 : sequencePoint.StartColumn,
                    EndLine = sequencePoint.IsHidden ? 0 : sequencePoint.EndLine,
                    EndColumn = sequencePoint.IsHidden ? 0 : sequencePoint.EndColumn,
                    BlockIndex = location?.BlockIndex,
                    InstructionIndex = location?.InstructionIndex
                });
            }

            foreach (var scopeHandle in pdbReader.GetLocalScopes(methodHandle))
            {
                var scope = pdbReader.GetLocalScope(scopeHandle);
                var debugScope = new DebugLocalScope
                {
                    StartOffset = scope.StartOffset,
                    EndOffset = scope.StartOffset + scope.Length
                };
                foreach (var localHandle in scope.GetLocalVariables())
                {
                    var local = pdbReader.GetLocalVariable(localHandle);
                    var debugLocal = new DebugLocal
                    {
                        Slot = local.Index,
                        Name = local.Name.IsNil ? string.Empty : pdbReader.GetString(local.Name),
                        Attributes = local.Attributes.ToString()
                    };
                    if (local.Index < decoder.MethodBody.LocalVariables.Count)
                    {
                        var localType = decoder.MethodBody.LocalVariables[local.Index].LocalType;
                        if (IsDisplayClass(localType))
                        {
                            debugLocal.IsCompilerGeneratedClosure = true;
                            debugLocal.CapturedLocals = GetCapturedLocals(
                                localType,
                                method.Parameters.Select(parameter => parameter.Name));
                        }
                    }
                    debugScope.Locals.Add(debugLocal);
                }
                method.LocalScopes.Add(debugScope);
            }

            var kickoffMethod = methodDebugInformation.GetStateMachineKickoffMethod();
            if (!kickoffMethod.IsNil)
            {
                method.StateMachineKickoffMethodToken = MetadataTokens.GetToken(kickoffMethod);
                AddHoistedLocals(pdbReader, methodHandle, methodDebugHandle, decoder, method);
            }

            AddCustomDebugInformation(pdbReader, methodHandle, method.CustomDebugInformation);
            AddCustomDebugInformation(pdbReader, methodDebugHandle, method.CustomDebugInformation);
            return method;
        }

        private static bool IsDisplayClass(Type type) =>
            type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
            && type.Name.IndexOf("<>c__DisplayClass", StringComparison.Ordinal) >= 0;

        private static List<DebugCapturedLocal> GetCapturedLocals(
            Type displayClass,
            IEnumerable<string> parameterNames)
        {
            var parameters = new HashSet<string>(parameterNames, StringComparer.Ordinal);
            var captured = new List<DebugCapturedLocal>();
            AddCapturedLocals(
                displayClass,
                new List<string>(),
                parameters,
                new HashSet<Type>(),
                captured);
            return captured
                .GroupBy(local => local.Name, StringComparer.Ordinal)
                .Select(group => group.OrderBy(local => local.FieldPath.Count).First())
                .OrderBy(local => local.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static void AddCapturedLocals(
            Type displayClass,
            IReadOnlyList<string> parentPath,
            ISet<string> parameterNames,
            ISet<Type> visited,
            ICollection<DebugCapturedLocal> captured)
        {
            if (!visited.Add(displayClass))
            {
                return;
            }
            foreach (var field in displayClass.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var path = parentPath.Concat(new[] { field.Name }).ToList();
                if (IsDisplayClass(field.FieldType))
                {
                    AddCapturedLocals(field.FieldType, path, parameterNames, visited, captured);
                    continue;
                }
                if (field.Name.StartsWith("<", StringComparison.Ordinal)
                    || parameterNames.Contains(field.Name))
                {
                    continue;
                }
                captured.Add(new DebugCapturedLocal
                {
                    Name = field.Name,
                    Type = field.FieldType.FullName ?? field.FieldType.Name,
                    FieldPath = path
                });
            }
            visited.Remove(displayClass);
        }

        private static void AddHoistedLocals(
            MetadataReader pdbReader,
            MethodDefinitionHandle methodHandle,
            MethodDebugInformationHandle methodDebugHandle,
            MethodDecoder decoder,
            DebugMethod method)
        {
            var scopes = GetHoistedLocalScopes(pdbReader, methodHandle)
                ?? GetHoistedLocalScopes(pdbReader, methodDebugHandle)
                ?? new List<HoistedLocalScope>();
            var fields = decoder.MethodBase.DeclaringType?
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => new HoistedLocalField(
                    field,
                    TryGetHoistedLocalSlot(field.Name)))
                .Where(item => item.Slot.HasValue)
                .OrderBy(item => item.Slot)
                .ToList() ?? new List<HoistedLocalField>();

            foreach (var item in fields)
            {
                var slotIndex = item.Slot!.Value - 1;
                var scope = slotIndex >= 0 && slotIndex < scopes.Count
                    ? scopes[slotIndex]
                    : new HoistedLocalScope(0, int.MaxValue);
                method.HoistedLocals.Add(new DebugHoistedLocal
                {
                    FieldName = item.Field.Name,
                    Name = item.Field.Name.Substring(1, item.Field.Name.IndexOf('>') - 1),
                    Type = item.Field.FieldType.FullName ?? item.Field.FieldType.Name,
                    StartOffset = scope.StartOffset,
                    EndOffset = scope.EndOffset
                });
            }
        }

        private static int? TryGetHoistedLocalSlot(string fieldName)
        {
            if (!fieldName.StartsWith("<", StringComparison.Ordinal))
            {
                return null;
            }
            var marker = fieldName.IndexOf(">5__", StringComparison.Ordinal);
            if (marker <= 1
                || !int.TryParse(fieldName.Substring(marker + 4), out var slot)
                || slot <= 0)
            {
                return null;
            }
            return slot;
        }

        private static List<HoistedLocalScope> GetHoistedLocalScopes(
            MetadataReader pdbReader,
            EntityHandle parent)
        {
            foreach (var customHandle in pdbReader.GetCustomDebugInformation(parent))
            {
                var custom = pdbReader.GetCustomDebugInformation(customHandle);
                if (pdbReader.GetGuid(custom.Kind) != StateMachineHoistedLocalScopesKind)
                {
                    continue;
                }
                var reader = pdbReader.GetBlobReader(custom.Value);
                if (reader.RemainingBytes % 8 != 0)
                {
                    throw new InvalidDataException("The state-machine hoisted-local scope record is malformed.");
                }
                var result = new List<HoistedLocalScope>();
                while (reader.RemainingBytes > 0)
                {
                    var startOffset = reader.ReadInt32();
                    var endOffset = reader.ReadInt32();
                    result.Add(new HoistedLocalScope(startOffset, endOffset));
                }
                return result;
            }
            return null;
        }

        private sealed record class HoistedLocalScope(int StartOffset, int EndOffset);

        private sealed record class HoistedLocalField(FieldInfo Field, int? Slot);

        private static HashSet<DocumentHandle> GetEmbeddedDocuments(MetadataReader pdbReader)
        {
            var result = new HashSet<DocumentHandle>();
            foreach (var documentHandle in pdbReader.Documents)
            {
                foreach (var customHandle in pdbReader.GetCustomDebugInformation(documentHandle))
                {
                    var custom = pdbReader.GetCustomDebugInformation(customHandle);
                    if (pdbReader.GetGuid(custom.Kind) == EmbeddedSourceKind)
                    {
                        result.Add(documentHandle);
                    }
                }
            }
            return result;
        }

        private static void AddCustomDebugInformation(
            MetadataReader pdbReader,
            EntityHandle parent,
            List<DebugCustomInformation> target)
        {
            foreach (var customHandle in pdbReader.GetCustomDebugInformation(parent))
            {
                var custom = pdbReader.GetCustomDebugInformation(customHandle);
                var item = new DebugCustomInformation
                {
                    Kind = pdbReader.GetGuid(custom.Kind).ToString("D"),
                    Value = Convert.ToBase64String(pdbReader.GetBlobBytes(custom.Value))
                };
                if (!target.Any(existing => existing.Kind == item.Kind && existing.Value == item.Value))
                {
                    target.Add(item);
                }
            }
            target.Sort((left, right) => string.CompareOrdinal(left.Kind, right.Kind));
        }

        private static FileStream OpenPdb(string path) => new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        private static string ComputeBuildId(DebugManifest manifest)
        {
            manifest.BuildId = string.Empty;
            var serialized = DebugManifestWriter.SerializeForHash(manifest);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(serialized));
            var identities = string.Join(",", manifest.Modules.Select(
                module => $"{module.ModuleId}:{module.PdbId}"));
            return $"{identities}:{ToHex(hash)}";
        }

        private static string GetHashAlgorithmName(Guid algorithm) =>
            algorithm == Sha256DocumentHash
                ? "SHA256"
                : algorithm == Sha1DocumentHash
                    ? "SHA1"
                    : algorithm == Guid.Empty
                        ? string.Empty
                        : algorithm.ToString("D");

        private static string GetLanguageName(Guid language) =>
            language == CSharpLanguage
                ? "C#"
                : language == Guid.Empty
                    ? string.Empty
                    : language.ToString("D");

        private static string ToHex(IEnumerable<byte> bytes) =>
            Convert.ToHexString(bytes.ToArray()).ToLowerInvariant();
    }
}
