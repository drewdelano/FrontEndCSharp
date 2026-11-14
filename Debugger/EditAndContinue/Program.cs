using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;

namespace FrontEndCSharp.EditAndContinue;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.Length != 2)
            {
                throw new ArgumentException(
                    "Usage: FrontEndCSharp.EditAndContinue <project.csproj> <session-directory>");
            }
            MSBuildLocator.RegisterDefaults();
            await using var session = await EditSession.CreateAsync(
                Path.GetFullPath(args[0]),
                Path.GetFullPath(args[1]));
            await WriteAsync(session.InitialResponse);
            while (await Console.In.ReadLineAsync() is { } line)
            {
                HostResponse response;
                try
                {
                    var request = JsonSerializer.Deserialize<HostRequest>(line, JsonOptions)
                        ?? throw new InvalidDataException("The Edit and Continue request was empty.");
                    response = request.Command switch
                    {
                        "prepare" => await session.PrepareAsync(),
                        "commit" => session.Commit(request.Generation),
                        "discard" => session.Discard(request.Generation),
                        _ => throw new InvalidDataException($"Unknown command '{request.Command}'.")
                    };
                }
                catch (Exception exception)
                {
                    response = HostResponse.Failure(exception.Message);
                }
                await WriteAsync(response);
            }
            return 0;
        }
        catch (Exception exception)
        {
            await WriteAsync(HostResponse.Failure(exception.ToString()));
            return 1;
        }
    }

    private static Task WriteAsync(HostResponse response) =>
        Console.Out.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

internal sealed class EditSession : IAsyncDisposable
{
    private readonly string projectPath;
    private readonly string sessionDirectory;
    private MSBuildWorkspace workspace;
    private Project project;
    private Compilation compilation;
    private readonly ModuleMetadata moduleMetadata;
    private EmitBaseline baseline;
    private PendingUpdate? pending;
    private int generation;

    private EditSession(
        string projectPath,
        string sessionDirectory,
        MSBuildWorkspace workspace,
        Project project,
        Compilation compilation,
        ModuleMetadata moduleMetadata,
        EmitBaseline baseline,
        HostResponse initialResponse)
    {
        this.projectPath = projectPath;
        this.sessionDirectory = sessionDirectory;
        this.workspace = workspace;
        this.project = project;
        this.compilation = compilation;
        this.moduleMetadata = moduleMetadata;
        this.baseline = baseline;
        InitialResponse = initialResponse;
    }

    internal HostResponse InitialResponse { get; }

    internal static async Task<EditSession> CreateAsync(string projectPath, string sessionDirectory)
    {
        Directory.CreateDirectory(sessionDirectory);
        var workspace = MSBuildWorkspace.Create();
        var failures = new List<string>();
        workspace.WorkspaceFailed += (_, eventArgs) => failures.Add(eventArgs.Diagnostic.Message);
        var project = await workspace.OpenProjectAsync(projectPath);
        var compilation = await project.GetCompilationAsync()
            ?? throw new InvalidOperationException("Roslyn did not create a project compilation.");
        var assemblyPath = Path.Combine(sessionDirectory, $"{compilation.AssemblyName}.dll");
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        byte[] assembly;
        byte[] pdb;
        using (var peStream = new MemoryStream())
        using (var pdbStream = new MemoryStream())
        {
            var emit = compilation.Emit(
                peStream,
                pdbStream,
                options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
            EnsureSuccess(emit.Diagnostics, "baseline compilation");
            assembly = peStream.ToArray();
            pdb = pdbStream.ToArray();
        }
        await File.WriteAllBytesAsync(assemblyPath, assembly);
        await File.WriteAllBytesAsync(pdbPath, pdb);
        var runtimeOverrideReferences = CopyNonFrameworkReferences(project, compilation, sessionDirectory);
        WriteRuntimeOverrideDependencyManifest(assemblyPath, runtimeOverrideReferences);

        var moduleMetadata = ModuleMetadata.CreateFromImage(assembly);
        var baseline = EmitBaseline.CreateInitialBaseline(
            compilation,
            moduleMetadata,
            _ => default,
            _ => default,
            true);
        return new EditSession(
            projectPath,
            sessionDirectory,
            workspace,
            project,
            compilation,
            moduleMetadata,
            baseline,
            new HostResponse
            {
                Success = true,
                Generation = 0,
                AssemblyPath = assemblyPath,
                PdbPath = pdbPath,
                Diagnostics = failures
            });
    }

    internal async Task<HostResponse> PrepareAsync()
    {
        if (pending !=  null)
        {
            throw new InvalidOperationException(
                $"Generation {pending.Generation} is already prepared; commit or discard it first.");
        }

        var nextWorkspace = MSBuildWorkspace.Create();
        var workspaceDiagnostics = new List<string>();
        nextWorkspace.WorkspaceFailed += (_, eventArgs) =>
            workspaceDiagnostics.Add(eventArgs.Diagnostic.Message);
        var nextProject = await nextWorkspace.OpenProjectAsync(projectPath);
        var nextCompilation = await nextProject.GetCompilationAsync()
            ?? throw new InvalidOperationException("Roslyn did not create the updated project compilation.");
        EnsureSuccess(nextCompilation.GetDiagnostics(), "updated compilation");

        var edits = await ConservativeEditClassifier.CreateEditsAsync(project, compilation, nextProject, nextCompilation);
        if (edits.Count == 0)
        {
            nextWorkspace.Dispose();
            throw new InvalidOperationException("No supported method-body changes were found.");
        }

        using var metadataDelta = new MemoryStream();
        using var ilDelta = new MemoryStream();
        using var pdbDelta = new MemoryStream();
        var difference = nextCompilation.EmitDifference(
            baseline,
            edits,
            _ => false,
            metadataDelta,
            ilDelta,
            pdbDelta,
            CancellationToken.None);
        EnsureSuccess(difference.Diagnostics, "Edit and Continue delta");

        var nextGeneration = generation + 1;
        var generationDirectory = Path.Combine(sessionDirectory, $"generation-{nextGeneration}");
        Directory.CreateDirectory(generationDirectory);
        var assemblyPath = Path.Combine(generationDirectory, $"{nextCompilation.AssemblyName}.dll");
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        await EmitFullCompilationAsync(nextCompilation, assemblyPath, pdbPath);
        var runtimeOverrideReferences = CopyNonFrameworkReferences(
            nextProject,
            nextCompilation,
            generationDirectory);
        WriteRuntimeOverrideDependencyManifest(assemblyPath, runtimeOverrideReferences);
        var metadataDeltaPath = Path.Combine(generationDirectory, "metadata.delta");
        var ilDeltaPath = Path.Combine(generationDirectory, "il.delta");
        var pdbDeltaPath = Path.Combine(generationDirectory, "pdb.delta");
        await File.WriteAllBytesAsync(metadataDeltaPath, metadataDelta.ToArray());
        await File.WriteAllBytesAsync(ilDeltaPath, ilDelta.ToArray());
        await File.WriteAllBytesAsync(pdbDeltaPath, pdbDelta.ToArray());

        pending = new PendingUpdate(
            nextGeneration,
            nextWorkspace,
            nextProject,
            nextCompilation,
            difference.Baseline!,
            difference.UpdatedMethods.Select(handle => MetadataTokens.GetToken(handle)).ToArray());
        return new HostResponse
        {
            Success = true,
            Generation = nextGeneration,
            AssemblyPath = assemblyPath,
            PdbPath = pdbPath,
            MetadataDeltaPath = metadataDeltaPath,
            IlDeltaPath = ilDeltaPath,
            PdbDeltaPath = pdbDeltaPath,
            UpdatedMethodTokens = pending.UpdatedMethodTokens,
            Diagnostics = workspaceDiagnostics
        };
    }

    internal HostResponse Commit(int requestedGeneration)
    {
        var update = RequirePending(requestedGeneration);
        workspace.Dispose();
        workspace = update.Workspace;
        project = update.Project;
        compilation = update.Compilation;
        baseline = update.Baseline;
        generation = update.Generation;
        pending = null;
        return new HostResponse { Success = true, Generation = generation };
    }

    internal HostResponse Discard(int requestedGeneration)
    {
        var update = RequirePending(requestedGeneration);
        update.Workspace.Dispose();
        pending = null;
        return new HostResponse { Success = true, Generation = generation };
    }

    private PendingUpdate RequirePending(int requestedGeneration)
    {
        if (pending == null || pending.Generation != requestedGeneration)
        {
            throw new InvalidOperationException(
                $"Generation {requestedGeneration} is not the prepared generation.");
        }
        return pending;
    }

    private static async Task EmitFullCompilationAsync(
        Compilation compilation,
        string assemblyPath,
        string pdbPath)
    {
        await using var assembly = File.Create(assemblyPath);
        await using var pdb = File.Create(pdbPath);
        var result = compilation.Emit(
            assembly,
            pdb,
            options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb));
        EnsureSuccess(result.Diagnostics, "updated full compilation");
    }

    private static void EnsureSuccess(IEnumerable<Diagnostic> diagnostics, string operation)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"The {operation} failed:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
        }
    }

    private static IReadOnlyList<string> CopyNonFrameworkReferences(
        Project project,
        Compilation compilation,
        string destinationDirectory)
    {
        var dotnetRoot = Path.GetFullPath(Path.Combine(
            System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),
            "..", "..", ".."));
        var runtimeOverrideReferences = new List<string>();
        foreach (var referencePath in compilation.References
            .OfType<PortableExecutableReference>()
            .Select(reference => reference.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path!))
            .Where(path => !path.StartsWith(dotnetRoot, StringComparison.OrdinalIgnoreCase)))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(referencePath));
            if (!string.Equals(referencePath, destination, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(referencePath, destination, overwrite: true);
            }
            if (destination.EndsWith(".fecs.dll", StringComparison.OrdinalIgnoreCase))
            {
                runtimeOverrideReferences.Add(destination);
            }
            var pdbPath = Path.ChangeExtension(referencePath, ".pdb");
            if (File.Exists(pdbPath))
            {
                File.Copy(pdbPath, Path.Combine(destinationDirectory, Path.GetFileName(pdbPath)), overwrite: true);
            }
        }
        foreach (var reference in compilation.References.OfType<CompilationReference>())
        {
            var assemblyName = reference.Compilation.AssemblyName
                ?? throw new InvalidOperationException("A project reference has no assembly name.");
            var referencedProject = project.Solution.Projects.SingleOrDefault(candidate =>
                string.Equals(candidate.AssemblyName, assemblyName, StringComparison.Ordinal));
            var referencePath = referencedProject?.OutputFilePath;
            if (string.IsNullOrWhiteSpace(referencePath) || !File.Exists(referencePath))
            {
                throw new FileNotFoundException(
                    $"Referenced project '{assemblyName}' has not been built. Build the project before starting Edit and Continue.",
                    referencePath);
            }
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(referencePath));
            File.Copy(referencePath, destination, overwrite: true);
            if (destination.EndsWith(".fecs.dll", StringComparison.OrdinalIgnoreCase))
            {
                runtimeOverrideReferences.Add(destination);
            }
            var pdbPath = Path.ChangeExtension(referencePath, ".pdb");
            if (File.Exists(pdbPath))
            {
                File.Copy(pdbPath, Path.Combine(destinationDirectory, Path.GetFileName(pdbPath)), overwrite: true);
            }
        }
        return runtimeOverrideReferences;
    }

    private static void WriteRuntimeOverrideDependencyManifest(
        string assemblyPath,
        IReadOnlyList<string> runtimeOverrideReferences)
    {
        const string targetName = "FrontEndCSharp.EditAndContinue";
        var target = runtimeOverrideReferences
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                path => $"{Path.GetFileNameWithoutExtension(path)}/1.0.0",
                path => new
                {
                    runtime = new Dictionary<string, object>
                    {
                        [Path.GetFileName(path)] = new { }
                    }
                },
                StringComparer.Ordinal);
        var manifest = new
        {
            runtimeTarget = new { name = targetName },
            targets = new Dictionary<string, object>
            {
                [targetName] = target
            }
        };
        File.WriteAllText(
            Path.ChangeExtension(assemblyPath, ".deps.json"),
            JsonSerializer.Serialize(manifest, Program.JsonOptions));
    }

    public async ValueTask DisposeAsync()
    {
        if (pending !=  null)
        {
            pending.Workspace.Dispose();
        }
        workspace.Dispose();
        moduleMetadata.Dispose();
        await Task.CompletedTask;
    }

    private sealed record PendingUpdate(
        int Generation,
        MSBuildWorkspace Workspace,
        Project Project,
        Compilation Compilation,
        EmitBaseline Baseline,
        int[] UpdatedMethodTokens);
}

internal static class ConservativeEditClassifier
{
    internal static async Task<IReadOnlyList<SemanticEdit>> CreateEditsAsync(
        Project oldProject,
        Compilation oldCompilation,
        Project newProject,
        Compilation newCompilation)
    {
        var oldShape = GetDeclarationShape(oldCompilation);
        var newShape = GetDeclarationShape(newCompilation);
        if (!oldShape.SetEquals(newShape))
        {
            throw new InvalidOperationException(
                "Only method-body edits are supported; a type, member, signature, field, or parameter declaration changed.");
        }

        var oldMethods = await GetMethodsAsync(oldProject, oldCompilation);
        var newMethods = await GetMethodsAsync(newProject, newCompilation);
        var edits = new List<SemanticEdit>();
        foreach (var (key, oldMethod) in oldMethods)
        {
            if (!newMethods.TryGetValue(key, out var newMethod))
            {
                throw new InvalidOperationException($"Method '{key}' is missing from the updated compilation.");
            }
            if (oldMethod.Body == newMethod.Body)
            {
                continue;
            }
            RejectGeneratedState(newMethod.Syntax, key);
            edits.Add(new SemanticEdit(
                SemanticEditKind.Update,
                oldMethod.Symbol,
                newMethod.Symbol,
                syntaxMap: null,
                runtimeRudeEdit: null,
                instrumentation: default));
        }
        return edits;
    }

    private static HashSet<string> GetDeclarationShape(Compilation compilation)
    {
        var shape = new HashSet<string>(StringComparer.Ordinal);
        AddNamespace(compilation.Assembly.GlobalNamespace, shape);
        return shape;
    }

    private static void AddNamespace(INamespaceSymbol symbol, HashSet<string> shape)
    {
        foreach (var child in symbol.GetNamespaceMembers())
        {
            AddNamespace(child, shape);
        }
        foreach (var type in symbol.GetTypeMembers())
        {
            AddType(type, shape);
        }
    }

    private static void AddType(INamedTypeSymbol type, HashSet<string> shape)
    {
        if (!type.Locations.Any(location => location.IsInSource))
        {
            return;
        }
        shape.Add($"type:{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}:{type.TypeKind}:{type.Arity}:{type.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
        foreach (var member in type.GetMembers().Where(member => member.Locations.Any(location => location.IsInSource)))
        {
            switch (member)
            {
                case IMethodSymbol method:
                    shape.Add($"method:{MethodKey(method)}");
                    break;
                case IFieldSymbol field:
                    shape.Add($"field:{field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}:{field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}:{field.IsStatic}:{field.IsConst}");
                    break;
                case IPropertySymbol property:
                    shape.Add($"property:{property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}:{property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
                    break;
                case IEventSymbol @event:
                    shape.Add($"event:{@event.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}:{@event.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
                    break;
                case INamedTypeSymbol nested:
                    AddType(nested, shape);
                    break;
            }
        }
    }

    private static async Task<Dictionary<string, MethodBody>> GetMethodsAsync(
        Project project,
        Compilation compilation)
    {
        var result = new Dictionary<string, MethodBody>(StringComparer.Ordinal);
        foreach (var document in project.Documents.Where(document => document.SourceCodeKind == SourceCodeKind.Regular))
        {
            var root = await document.GetSyntaxRootAsync();
            var model = await document.GetSemanticModelAsync();
            if (root == null || model == null)
            {
                continue;
            }
            foreach (var syntax in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(syntax) is not IMethodSymbol symbol)
                {
                    continue;
                }
                var body = syntax switch
                {
                    MethodDeclarationSyntax method => method.Body?.WithoutTrivia().ToFullString()
                        ?? method.ExpressionBody?.Expression.WithoutTrivia().ToFullString()
                        ?? string.Empty,
                    ConstructorDeclarationSyntax constructor => constructor.Body?.WithoutTrivia().ToFullString()
                        ?? constructor.ExpressionBody?.Expression.WithoutTrivia().ToFullString()
                        ?? string.Empty,
                    DestructorDeclarationSyntax destructor => destructor.Body?.WithoutTrivia().ToFullString() ?? string.Empty,
                    OperatorDeclarationSyntax op => op.Body?.WithoutTrivia().ToFullString()
                        ?? op.ExpressionBody?.Expression.WithoutTrivia().ToFullString()
                        ?? string.Empty,
                    ConversionOperatorDeclarationSyntax conversion => conversion.Body?.WithoutTrivia().ToFullString()
                        ?? conversion.ExpressionBody?.Expression.WithoutTrivia().ToFullString()
                        ?? string.Empty,
                    _ => string.Empty
                };
                result.Add(MethodKey(symbol), new MethodBody(symbol, syntax, body));
            }
        }
        return result;
    }

    private static string MethodKey(IMethodSymbol method) =>
        method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        + $":{method.MethodKind}:{method.Arity}:{method.IsStatic}:{method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}"
        + string.Concat(method.Parameters.Select(parameter =>
            $":{parameter.RefKind}:{parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}"));

    private static void RejectGeneratedState(BaseMethodDeclarationSyntax syntax, string key)
    {
        if (syntax.Modifiers.Any(SyntaxKind.AsyncKeyword)
            || syntax.DescendantNodes().Any(node => node is YieldStatementSyntax
                or AnonymousFunctionExpressionSyntax
                or LocalFunctionStatementSyntax))
        {
            throw new InvalidOperationException(
                $"Method '{key}' changes async, iterator, closure, lambda, or local-function state and cannot be updated yet.");
        }
    }

    private sealed record MethodBody(
        IMethodSymbol Symbol,
        BaseMethodDeclarationSyntax Syntax,
        string Body);
}

internal sealed class HostRequest
{
    public string Command { get; set; } = string.Empty;
    public int Generation { get; set; }
}

internal sealed class HostResponse
{
    public bool Success { get; set; }
    public int Generation { get; set; }
    public string AssemblyPath { get; set; } = string.Empty;
    public string PdbPath { get; set; } = string.Empty;
    public string MetadataDeltaPath { get; set; } = string.Empty;
    public string IlDeltaPath { get; set; } = string.Empty;
    public string PdbDeltaPath { get; set; } = string.Empty;
    public int[] UpdatedMethodTokens { get; set; } = [];
    public List<string> Diagnostics { get; set; } = [];
    public string Error { get; set; } = string.Empty;

    internal static HostResponse Failure(string error) => new() { Error = error };
}
