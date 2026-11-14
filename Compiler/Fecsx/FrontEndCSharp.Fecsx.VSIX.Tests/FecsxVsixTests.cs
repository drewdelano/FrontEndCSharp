using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FrontEndCSharp.Fecsx.VSIX.Tests;

public sealed class FecsxVsixTests
{
    private static readonly string ProjectDirectory = Path.Combine(
        FindRepositoryRoot(), "Compiler", "Fecsx", "FrontEndCSharp.Fecsx.VSIX");

    [Test]
    public void RegistersTheFecsxContentTypeGrammarAndLanguageConfiguration()
    {
        var packageDefinition = File.ReadAllText(Path.Combine(ProjectDirectory, "Fecsx.pkgdef"));
        var contentTypeDefinition = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxContentType.cs"));

        Assert.Contains("[$RootKey$\\TextMate\\Repositories]", packageDefinition);
        Assert.Contains("\"FrontEndCSharp.Fecsx\"=\"$PackageFolder$\\Assets\"", packageDefinition);
        Assert.Contains("\"source.fecsx\"=\"$PackageFolder$\\Assets\\language-configuration.json\"", packageDefinition);
        Assert.Contains("[$RootKey$\\TextMate\\LanguageConfiguration\\ContentTypeMapping]", packageDefinition);
        Assert.Contains("[BaseDefinition(\"code++\")]", contentTypeDefinition);
        Assert.DoesNotContain("[BaseDefinition(\"code\")]", contentTypeDefinition);
    }

    [Test]
    public void RegistersACompactFecsxFileIconWithAGreenXOverlay()
    {
        XNamespace imageNs = "http://schemas.microsoft.com/VisualStudio/ImageManifestSchema/2014";
        XNamespace vsixNs = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var packageDefinition = File.ReadAllText(Path.Combine(ProjectDirectory, "Fecsx.pkgdef"));
        var imageManifest = XDocument.Load(Path.Combine(ProjectDirectory, "FecsxImages.imagemanifest"));
        var vsixManifest = XDocument.Load(Path.Combine(ProjectDirectory, "source.extension.vsixmanifest"));
        var project = XDocument.Load(Path.Combine(ProjectDirectory, "FrontEndCSharp.Fecsx.VSIX.csproj"));
        var icon = XDocument.Load(Path.Combine(ProjectDirectory, "Assets", "FecsxFile.xaml"));

        Assert.Contains("[$RootKey$\\ShellFileAssociations\\.fecsx]", packageDefinition);
        Assert.Contains("{dd37ad22-31f8-4dde-b7bd-5d1424d89a93}:1", packageDefinition);
        Assert.Contains("[$RootKey$\\BindingPaths\\{dd37ad22-31f8-4dde-b7bd-5d1424d89a93}]", packageDefinition);
        Assert.Contains("\"$PackageFolder$\"=\"\"", packageDefinition);
        Assert.Contains("RuntimeConfiguration\\dependentAssembly\\codeBase", packageDefinition);
        Assert.Contains("\"name\"=\"FrontEndCSharp.Fecsx.VSIX\"", packageDefinition);
        Assert.Contains("\"codeBase\"=\"$PackageFolder$\\FrontEndCSharp.Fecsx.VSIX.dll\"", packageDefinition);
        Assert.Equal(
            "FecsxFile.xaml",
            imageManifest.Descendants(imageNs + "Source")
                .Single(element => element.Attribute("Uri")!.Value.EndsWith("/FecsxFile.xaml", StringComparison.Ordinal))
                .Attribute("Uri")!.Value.Split('/').Last());
        Assert.Contains(vsixManifest.Descendants(vsixNs + "Asset"), asset =>
            (string?)asset.Attribute("Type") == "Microsoft.VisualStudio.ImageManifest" &&
            (string?)asset.Attribute("Path") == "FecsxImages.imagemanifest");
        Assert.Equal("16", icon.Root!.Attribute("Width")!.Value);
        Assert.Equal("16", icon.Root.Attribute("Height")!.Value);
        Assert.Contains(project.Descendants(), element =>
            element.Name.LocalName == "UseWPF" && element.Value == "true");
        Assert.Contains(project.Descendants(), element =>
            element.Name.LocalName == "Resource" &&
            (string?)element.Attribute("Include") == "Assets\\FecsxFile.xaml");
        const string xOverlayData =
            "M 10.5,10.5 L 14.5,14.5 M 14.5,10.5 L 10.5,14.5";
        var xOverlay = icon.Descendants()
            .Where(element =>
                element.Name.LocalName == "Path" &&
                string.Equals(
                    (string?)element.Attribute("Data"),
                    xOverlayData,
                    StringComparison.Ordinal))
            .ToArray();
        Assert.Collection(
            xOverlay,
            backing =>
            {
                Assert.Equal("#FFFFFF", (string?)backing.Attribute("Stroke"), ignoreCase: true);
                Assert.Equal("3", (string?)backing.Attribute("StrokeThickness"));
            },
            foreground =>
            {
                Assert.Equal("#167342", (string?)foreground.Attribute("Stroke"), ignoreCase: true);
                Assert.Equal("1.5", (string?)foreground.Attribute("StrokeThickness"));
            });
        Assert.DoesNotContain(icon.Descendants().Attributes("Stroke"), attribute =>
            string.Equals(attribute.Value, "red", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(attribute.Value, "#FF0000", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void TailwindCompletionUsesThePinnedCatalogDescriptionsAndOfficialMark()
    {
        XNamespace imageNs = "http://schemas.microsoft.com/VisualStudio/ImageManifestSchema/2014";
        var imageManifest = XDocument.Load(Path.Combine(ProjectDirectory, "FecsxImages.imagemanifest"));
        var project = XDocument.Load(Path.Combine(ProjectDirectory, "FrontEndCSharp.Fecsx.VSIX.csproj"));
        var icon = XDocument.Load(Path.Combine(ProjectDirectory, "Assets", "TailwindCompletion.xaml"));
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpCompletionSource.cs"));
        var sessionController = File.ReadAllText(Path.Combine(
            ProjectDirectory,
            "FecsxTailwindCompletionSessionController.cs"));
        var documentService = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxRoslynDocumentService.cs"));
        var commitManager = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpCompletionCommitManager.cs"));

        Assert.Contains(imageManifest.Descendants(imageNs + "ID"), element =>
            (string?)element.Attribute("Name") == "TailwindCompletion" &&
            (string?)element.Attribute("Value") == "2");
        Assert.Contains(imageManifest.Descendants(imageNs + "Source"), element =>
            ((string?)element.Attribute("Uri"))?.EndsWith("/TailwindCompletion.xaml", StringComparison.Ordinal) == true);
        Assert.Contains(imageManifest.Descendants(imageNs + "Source"), element =>
            ((string?)element.Attribute("Uri"))?.EndsWith("/TailwindCompletion.xaml", StringComparison.Ordinal) == true
            && element.Element(imageNs + "Size")?.Attribute("Value")?.Value == "16");
        Assert.Contains(project.Descendants(), element =>
            element.Name.LocalName == "Resource" &&
            (string?)element.Attribute("Include") == "Assets\\TailwindCompletion.xaml");
        Assert.Contains(icon.Descendants().Attributes("Fill"), attribute =>
            string.Equals(attribute.Value, "#38BDF8", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(icon.Descendants().Attributes("FillRule"));
        Assert.Contains("GetTailwindClassCompletionSpan", source, StringComparison.Ordinal);
        Assert.Contains("IsTailwindAvailable", source, StringComparison.Ordinal);
        Assert.Contains("build_property.FrontEndCSharpTailwindAvailable", documentService, StringComparison.Ordinal);
        Assert.Contains("build_property.FrontEndCSharpTailwindEnabled", documentService, StringComparison.Ordinal);
        Assert.Contains("build_property.FrontEndCSharpTailwindEditorCatalog", documentService, StringComparison.Ordinal);
        Assert.Contains("build_property.FrontEndCSharpProject", documentService, StringComparison.Ordinal);
        Assert.Contains("GetTailwindEditorCatalogPath", documentService, StringComparison.Ordinal);
        Assert.Contains("GetTailwindEditorCatalog()", source, StringComparison.Ordinal);
        Assert.Contains("FecsxTailwindClassCatalog.FromCompressedBytes", documentService, StringComparison.Ordinal);
        Assert.Contains("Tailwind CSS", source, StringComparison.Ordinal);
        Assert.Contains("TailwindItemKey", source, StringComparison.Ordinal);
        Assert.Contains("IAsyncCompletionBroker", sessionController, StringComparison.Ordinal);
        Assert.Contains("IsTailwindAvailable", sessionController, StringComparison.Ordinal);
        Assert.Contains("NeedsTailwindClassCompletionRefresh", sessionController, StringComparison.Ordinal);
        Assert.Contains("Caret.PositionChanged += OnCaretPositionChanged", sessionController, StringComparison.Ordinal);
        Assert.Contains("Caret.PositionChanged -= OnCaretPositionChanged", sessionController, StringComparison.Ordinal);
        Assert.Contains("GetSession(_view)?.Dismiss()", sessionController, StringComparison.Ordinal);
        Assert.Contains("TriggerCompletion", sessionController, StringComparison.Ordinal);
        Assert.Contains("CommitTailwind", commitManager, StringComparison.Ordinal);
        Assert.Contains("CaretOffsetFromEnd", commitManager, StringComparison.Ordinal);
    }

    [Test]
    public void DebuggingTheVsixLaunchesTheExperimentalHive()
    {
        var project = XDocument.Load(Path.Combine(ProjectDirectory, "FrontEndCSharp.Fecsx.VSIX.csproj"));
        string Property(string name) => project.Descendants()
            .Single(element => element.Name.LocalName == name)
            .Value;

        Assert.Equal("Program", Property("StartAction"));
        Assert.Equal("$(DevEnvDir)devenv.exe", Property("StartProgram"));
        Assert.Equal("/RootSuffix Exp", Property("StartArguments"));
    }

    [Test]
    public void VssdkProjectsCanLoadBeforeTheirBuildToolsPackageIsRestored()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sharedBuildTools = XDocument.Load(Path.Combine(
            repositoryRoot,
            "Vssdk.BuildTools.props"));
        var projects = new[]
        {
            Path.Combine(
                repositoryRoot,
                "Compiler",
                "Fecsx",
                "FrontEndCSharp.Fecsx.VSIX",
                "FrontEndCSharp.Fecsx.VSIX.csproj"),
            Path.Combine(
                repositoryRoot,
                "Compiler",
                "Fecsx",
                "FrontEndCSharp.Fecsx.VSIX",
                "Templates",
                "FrontEndCSharp.Fecsx",
                "FrontEndCSharp.Fecsx.Template.csproj"),
            Path.Combine(
                repositoryRoot,
                "Debugger",
                "VisualStudio",
                "FrontEndCSharp.VisualStudio.csproj"),
            Path.Combine(
                repositoryRoot,
                "Debugger",
                "VisualStudio",
                "Templates",
                "FrontEndCSharp.Browser",
                "FrontEndCSharp.Browser.Template.csproj"),
        };

        foreach (var projectPath in projects)
        {
            var project = XDocument.Load(projectPath);
            var buildToolsImport = project.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Import" &&
                    (string?)element.Attribute("Project") ==
                    "$(_FrontEndCSharpRepositoryRoot)Vssdk.BuildTools.props");
            var import = project.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Import" &&
                    (string?)element.Attribute("Project") ==
                    "$(VSToolsPath)\\VSSDK\\Microsoft.VsSDK.targets");
            var guard = project.Descendants()
                .Single(element =>
                    element.Name.LocalName == "Target" &&
                    (string?)element.Attribute("Name") == "RequireVssdkBuildTools");

            Assert.Equal(
                "Exists('$(VSToolsPath)\\VSSDK\\Microsoft.VsSDK.targets')",
                (string?)import.Attribute("Condition"));
            Assert.Equal(
                "!Exists('$(VSToolsPath)\\VSSDK\\Microsoft.VsSDK.targets')",
                (string?)guard.Attribute("Condition"));
            Assert.Contains(guard.Descendants(), element =>
                element.Name.LocalName == "Error" &&
                ((string?)element.Attribute("Text"))?.Contains(
                    "Restore",
                    StringComparison.Ordinal) == true);
            Assert.NotNull(buildToolsImport);
        }

        foreach (var templateProjectPath in projects.Where(path =>
                     path.EndsWith(".Template.csproj", StringComparison.Ordinal)))
        {
            var templateProject = XDocument.Load(templateProjectPath);
            Assert.Equal(
                "false",
                templateProject.Descendants()
                    .Single(element =>
                        element.Name.LocalName ==
                        "FrontEndCSharpIncludeVssdkBuildToolsPackageReference")
                    .Value);
            Assert.DoesNotContain(templateProject.Descendants(), element =>
                element.Name.LocalName == "TargetFramework");
            Assert.Equal(
                "v4.7.2",
                templateProject.Descendants()
                    .Single(element => element.Name.LocalName == "TargetFrameworkVersion")
                    .Value);
            Assert.Equal(
                "None",
                templateProject.Descendants()
                    .Single(element => element.Name.LocalName == "RestoreProjectStyle")
                    .Value);
            Assert.Equal(
                "false",
                templateProject.Descendants()
                    .Single(element => element.Name.LocalName == "ResolveNuGetPackages")
                    .Value);
            var visibleTemplateTrees = templateProject.Descendants()
                .Where(element => element.Name.LocalName == "None")
                .Select(element => (string?)element.Attribute("Include"))
                .ToArray();
            Assert.Contains("Host\\**\\*", visibleTemplateTrees);
            Assert.Contains("Browser\\**\\*", visibleTemplateTrees);
        }

        Assert.Contains(sharedBuildTools.Descendants(), element =>
            element.Name.LocalName == "PackageReference" &&
            (string?)element.Attribute("Include") == "Microsoft.VSSDK.BuildTools" &&
            (string?)element.Attribute("GeneratePathProperty") == "true");
        Assert.Contains(sharedBuildTools.Descendants(), element =>
            element.Name.LocalName == "VSToolsPath" &&
            element.Value.Contains(
                "_FrontEndCSharpVssdkBuildToolsRoot",
                StringComparison.Ordinal));
        Assert.Contains(sharedBuildTools.Descendants(), element =>
            element.Name.LocalName == "_FrontEndCSharpVssdkBuildToolsRoot" &&
            ((string?)element.Attribute("Condition"))?.Contains(
                "$(UserProfile)\\.nuget\\packages",
                StringComparison.Ordinal) == true);
        Assert.Contains(sharedBuildTools.Descendants(), element =>
            element.Name.LocalName == "Target" &&
            (string?)element.Attribute("Name") ==
            "SetFrontEndCSharpVssdkEnvironmentForTemplateManifest" &&
            (string?)element.Attribute("BeforeTargets") ==
            "GenerateTemplatesManifest" &&
            (string?)element.Attribute("DependsOnTargets") ==
            "SetVsSDKEnvironmentVariables");
    }

    [Test]
    public void FecsxProjectTemplateDefaultsAttributeCompletionToCamelCase()
    {
        var browserProject = XDocument.Load(Path.Combine(
            ProjectDirectory,
            "Templates",
            "FrontEndCSharp.Fecsx",
            "Browser",
            "Browser.csproj"));

        Assert.Equal(
            "camelCase",
            browserProject.Descendants()
                .Single(element =>
                    element.Name.LocalName == "FrontEndCSharpFecsxAttributeCasing")
                .Value);
    }

    [Test]
    public void SemanticCSharpTaggerUsesTheLiveRoslynWorkspaceAndProjection()
    {
        var project = XDocument.Load(Path.Combine(ProjectDirectory, "FrontEndCSharp.Fecsx.VSIX.csproj"));
        var packages = project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .ToArray();
        var tagger = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpClassificationTagger.cs"));
        var documents = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxRoslynDocumentService.cs"));

        Assert.Contains("Microsoft.CodeAnalysis.CSharp.Workspaces", packages);
        Assert.Contains("Classifier.GetClassifiedSpansAsync", tagger);
        Assert.Contains("FecsxRoslynDocumentService", tagger);
        Assert.Contains("TryMapProjectedSpan", tagger);
        Assert.Contains("FecsxCSharpProjector.Create", documents);
        Assert.Contains("AdditionalDocuments", documents);
        Assert.Contains("SComponentModel", documents);
        Assert.Contains("WorkspaceChanged", tagger);
        Assert.Contains("TextDocumentDisposed", tagger);
        Assert.Contains("delayMilliseconds: 0", tagger);
        Assert.DoesNotContain("JoinableTaskFactory", tagger);
        Assert.Contains("!Equals(e.ProjectId, project.Id)", tagger);
        Assert.Contains("new AdhocWorkspace()", tagger);
        Assert.Contains("ScheduleFallbackClassification", tagger);
        Assert.Contains("HasSemanticClassificationFor", tagger);
    }

    [Test]
    public void CSharpCompletionUsesTheLiveRoslynProjectAndAsyncEditorApi()
    {
        var project = XDocument.Load(Path.Combine(ProjectDirectory, "FrontEndCSharp.Fecsx.VSIX.csproj"));
        var packages = project.Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .ToArray();
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpCompletionSource.cs"));
        var documents = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxRoslynDocumentService.cs"));

        Assert.Contains("Microsoft.CodeAnalysis.CSharp.Features", packages);
        Assert.Contains("IAsyncCompletionSourceProvider", source);
        Assert.Contains("CompletionService.GetService", source);
        Assert.Contains("GetCompletionsAsync", source);
        Assert.Contains("FecsxRoslynDocumentService", source);
        Assert.Contains("TryMapProjectedSpan", source);
        Assert.Contains("CompletionParticipation.ExclusivelyProvidesItems", source);
        Assert.Contains("GetOpeningTagNameCompletionSpan", source);
        Assert.Contains("projectedDocument.ComponentProject", source);
        Assert.Contains("FecsxComponentCatalog.Create", source);
        Assert.Contains("catalog.ComponentNames", source);
        Assert.Contains("GetAttributeNameCompletionSpan", source);
        Assert.Contains("GetAttributeCompletions", source);
        Assert.Contains("WithAdditionalDocumentText", documents);
        Assert.Contains("new FecsxRoslynDocument(projection, document, componentProject)", documents);
    }

    [Test]
    public void QuickInfoUsesTheLiveRoslynProjectAndMapsItsApplicableSpan()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpQuickInfoSource.cs"));

        Assert.Contains("IAsyncQuickInfoSourceProvider", source);
        Assert.Contains("QuickInfoService.GetService", source);
        Assert.Contains("GetQuickInfoAsync", source);
        Assert.Contains("FecsxRoslynDocumentService", source);
        Assert.Contains("TryMapSourceCaretPosition", source);
        Assert.Contains("TryMapProjectedSpan", source);
        Assert.Contains("ClassifiedTextElement.CreatePlainText", source);
    }

    [Test]
    public void DiagnosticsCombineNativeFecsxErrorsWithMappedRoslynErrors()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxDiagnosticTagger.cs"));

        Assert.Contains("ITagger<ErrorTag>", source);
        Assert.Contains("FecsxEditorDiagnostics.GetNativeDiagnostics", source);
        Assert.Contains("GetSemanticModelAsync", source);
        Assert.Contains("FecsxEditorDiagnostics.MapRoslynDiagnostics", source);
        Assert.Contains("PredefinedErrorTypeNames.CompilerError", source);
        Assert.Contains("PredefinedErrorTypeNames.Warning", source);
        Assert.DoesNotContain("ThreadHelper.JoinableTaskFactory.Run", source);
    }

    [Test]
    public void GoToDefinitionResolvesTheProjectedSymbolAndNavigatesToAuthoredSource()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxGoToDefinitionCommandFilter.cs"));

        Assert.Contains("VSStd97CmdID.GotoDefn", source);
        Assert.Contains("TryMapSourceCaretPosition", source);
        Assert.Contains("SymbolFinder.FindSymbolAtPositionAsync", source);
        Assert.Contains("SymbolFinder.FindSourceDefinitionAsync", source);
        Assert.Contains("location.SourceTree?.FilePath", source);
        Assert.Contains("TryResolveNavigationTarget", source);
        Assert.Contains("TryMapProjectedSpan", source);
        Assert.Contains("candidate.FilePath + \".editor.g.cs\"", source);
        Assert.Contains("VsShellUtilities.OpenDocument", source);
        Assert.Contains("TryNavigateWithinActiveDocument", source);
        Assert.Contains("_textView.Caret.MoveTo", source);
        Assert.Contains("_textView.Caret.EnsureVisible", source);
        Assert.Contains("SetCaretPos", source);
        Assert.Contains("NavigateAsync(request, next.Token).Forget()", source);
        Assert.Contains("FecsxComponentNavigation.TryGetManagedTag", source);
        Assert.Contains("FecsxComponentNavigation.TryGetDeclaration", source);
        Assert.DoesNotContain("JoinableTaskFactory.Run(", source);
    }

    [Test]
    public void RenameProjectsEveryFecsxDocumentAndAppliesOnlyMappedAuthoredEdits()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxGoToDefinitionCommandFilter.cs"));

        Assert.Contains("VSStd2KCmdID.RENAME", source);
        Assert.Contains("project.AdditionalDocuments", source);
        Assert.Contains("Renamer.RenameSymbolAsync", source);
        Assert.Contains("FecsxCSharpRenameEdits.TryMapChanges", source);
        Assert.Contains("WithAdditionalDocumentText", source);
        Assert.Contains("WithDocumentText", source);
        Assert.Contains("TryApplyChanges", source);
        Assert.DoesNotContain("WithDocumentText(entry.ProjectionDocumentId", source);
    }

    [Test]
    public void CSharpCompletionCommitsRoslynReplacementAndImportEditsThroughTheProjection()
    {
        var source = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpCompletionSource.cs"));
        var commitManager = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpCompletionCommitManager.cs"));

        Assert.Contains("IAsyncCompletionCommitManagerProvider", commitManager);
        Assert.Contains("GetChangeAsync", commitManager);
        Assert.Contains("TryMapProjectedEdits", commitManager);
        Assert.Contains("change.NewPosition", commitManager);
        Assert.Contains("CreateEdit", commitManager);
        Assert.Contains("currentEdits.OrderByDescending", commitManager);
        Assert.Contains("TranslateTo(currentSnapshot", commitManager);
    }

    [Test]
    public void VisualCaptureWaitsForVisibleEditorContent()
    {
        var capture = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioEditorCapture.cs"));

        Assert.Contains("HasVisibleEditorContent", capture);
        Assert.Contains("did not render visible content", capture);
        Assert.Contains(".diagnostic.png", capture);
        Assert.Contains("CloseProcesses", capture);
        Assert.Contains("finally", capture);
        Assert.Contains("ReadinessLogPath", capture);
        Assert.Contains("ReadinessPattern", capture);
    }

    [Test]
    public void SemanticClassificationVisualCheckWaitsForAndVerifiesReferencedTypes()
    {
        var capture = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioEditorCapture.cs"));
        var documentService = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxRoslynDocumentService.cs"));
        var classificationTagger = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxCSharpClassificationTagger.cs"));

        Assert.Contains("FRONTENDCSHARP_FECSX_CLASSIFICATION_LOG", capture);
        Assert.Contains("Classification complete: semantic=True", capture);
        Assert.Contains("ReactNode", capture);
        Assert.Contains("OrbitStore", capture);
        Assert.Contains("WaitForDte", capture);
        Assert.Contains("WaitForSolution", capture);
        Assert.Contains("ItemOperations.OpenFile", capture);
        Assert.Contains("Regex.Matches", capture);
        Assert.Contains("CS0229", capture);
        Assert.Contains("CS0119", capture);
        Assert.Contains("WNetGetConnection", documentService);
        Assert.Contains("IsWithinDirectory", documentService);
        Assert.Contains("FindOwningProjectAfterLoadAsync", classificationTagger);
        Assert.Contains("DescribeMappedTypes", classificationTagger);
        Assert.Contains("GetDiagnosticIdsAsync", classificationTagger);
        Assert.Contains("MoveToLineAndOffset(editLine", capture);
        Assert.Contains("EditLine = options.EditLine", capture);
    }

    [Test]
    public void TextMateRegressionReadsActualVisualStudioScopesForBothPanelFixtures()
    {
        var probe = File.ReadAllText(Path.Combine(ProjectDirectory, "FecsxTextMateScopeProbe.cs"));
        var capture = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioEditorCapture.cs"));

        Assert.Contains("ITMScopeTag", probe);
        Assert.Contains("ClassificationTag", probe);
        Assert.Contains("IBufferTagAggregatorFactoryService", probe);
        Assert.Contains("TextMateScopeCaptureComplete", probe);
        Assert.Contains("WeatherPanel.fecsx", capture);
        Assert.Contains("EnergyPanel.fecsx", capture);
        Assert.Contains("entity.other.attribute-name.fecsx", capture);
        Assert.Contains("RequireEveryTokenScope(source, records, \"className\"", capture);
        Assert.Contains("RequireEveryTokenScope(source, records, \"type\"", capture);
        Assert.Contains("RequireEveryTokenScope(source, records, \"onClick\"", capture);
        Assert.Contains("RequireEveryTokenLacksScope(source, records, \"props\", \"string.\"", capture);
        Assert.Contains("RequireEquivalentClassifications", capture);
    }

    [Test]
    public void CompletionPopupCaptureVerifiesRepresentativeRoslynResults()
    {
        var verifier = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioCompletionVerification.cs"));
        var automation = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioAutomation.cs"));
        var itemIdentity = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioCompletionItemIdentity.cs"));

        Assert.Contains("Edit.ListMembers", verifier);
        Assert.Contains("GetVisibleMenuItems", verifier);
        Assert.Contains("VisualStudioCompletionItemIdentity.Matches", verifier);
        Assert.Contains("Do this for every project in the current solution", automation);
        Assert.Contains("SetChecked(false)", automation);
        Assert.Contains("nextRefresh", verifier);
        Assert.Contains("RetryTransientCom", verifier);
        Assert.Contains("selection.Insert(newText)", verifier);
        Assert.Contains("CurrentIsOffscreen", automation);
        Assert.Contains("NormalizeName", itemIdentity);
        Assert.Contains("CopyFromScreen", automation);
        Assert.Contains("Edit.Undo", verifier);
        Assert.Contains("ReactDemo", verifier);
        Assert.Contains("EnergyLevel", verifier);
        Assert.Contains("TaskCard", verifier);
        Assert.Contains("drop-shadow-xl", verifier);
        Assert.Contains("VerifyTailwindSessionRetargeting", verifier);
        Assert.Contains("SendText(\"p-\")", verifier);
        Assert.Contains("SendText(\"blo\")", verifier);
        Assert.Contains("WaitForVisibleText(\"display: block\"", verifier);
        Assert.Contains("Tailwind CSS v4.3.5", verifier);
        Assert.Contains("WaitForVisibleText", verifier);
        Assert.Contains("default", verifier);
        Assert.Contains("System.Collections.ObjectModel", verifier);
        Assert.Contains("SelectionItem", automation);
        Assert.Contains("ObservableCollectio", verifier);
        Assert.Contains("SendInput", automation);
        Assert.Contains("CommitSelectedCompletion", verifier);
        Assert.Contains("fecsx-completion-component-TaskCard-committed-invalid.png", verifier);
        Assert.Contains("Stopwatch.StartNew()", verifier);
        Assert.Contains("TimeSpan.FromSeconds(5)", verifier);
        Assert.Contains("ActiveDocument.Saved", verifier);
        Assert.Contains("Edit.GoToDefinition", verifier);
        Assert.Contains("OrbitStore.cs:8", verifier);
        Assert.Contains("App.fecsx:4", verifier);
        Assert.Contains("FocusCard.fecsx:6", verifier);
        Assert.Contains("App.fecsx:44", verifier);
        Assert.Contains("NavigationOnly", verifier);
        Assert.Contains("TailwindOnly", verifier);
        Assert.Contains("--tailwind-only", verifier);
        Assert.Contains("TailwindRetargetOnly", verifier);
        Assert.Contains("--tailwind-retarget-only", verifier);
        Assert.Contains("VerifyTailwindAdvancedSyntax", verifier);
        Assert.Contains("VerifyTailwindUtilityFamily", verifier);
        Assert.Contains("VerifyTailwindSemanticFamilies", verifier);
        Assert.Contains("WaitForVisibleText(\"Adds whitespace outside the box\"", verifier);
        Assert.Contains("WaitForCompletion(\"grow\"", verifier);
        Assert.Contains("WaitForCompletion(\"grow-\"", verifier);
        Assert.Contains("WaitForCompletion(\"bg-auto\"", verifier);
        Assert.Contains("WaitForCompletion(\"bg-clip-\"", verifier);
        Assert.Contains("WaitForCompletion(\"bg-amber-\"", verifier);
        Assert.Contains("WaitForCompletion(\"ring-\"", verifier);
        Assert.Contains("ring-1 → ring width: 1px", verifier);
        Assert.Contains("ring-[13px] → ring width: 13px", verifier);
        Assert.Contains("WaitForCompletion(\"m-\"", verifier);
        Assert.Contains("WaitForVisibleText(\"margin: 56px\"", verifier);
        Assert.Contains("WaitForCompletion(\"m-0\"", verifier);
        Assert.Contains("WaitForCompletion(\"m-8\"", verifier);
        Assert.Contains("BuildTailwindCompletionFixture", verifier);
        Assert.Contains("UsesTailwindCompletionFixture", verifier);
        Assert.Contains("WaitForCompletion(\"drop-shadow-card\"", verifier);
        Assert.Contains("@media (width >= 50rem)", verifier);
        Assert.Contains("VerifyTailwindStateVariant", verifier);
        Assert.Contains("WaitForCompletion(expectedCompletion", verifier);
        Assert.Contains("@media (hover: hover) { &:hover }", verifier);
        Assert.Contains("&:focus", verifier);
        Assert.Contains("&:where(.dark, .dark *)", verifier);
        Assert.Contains("VerifyTailwindResponsiveMediaVariant", verifier);
        Assert.Contains("WaitForCompletion(\"md:\"", verifier);
        Assert.Contains("WaitForCompletion(\"md:block\"", verifier);
        Assert.Contains("WaitForCompletion(\"md:h-\"", verifier);
        Assert.Contains("@media (width >= 48rem)", verifier);
        Assert.Contains("TailwindAdvancedOnly", verifier);
        Assert.Contains("--tailwind-advanced-only", verifier);
        Assert.Contains("drop-shadow-[0_35px_35px_rgba(0,0,0,0.25)]", verifier);
        Assert.Contains("Resulting CSS value: 0 35px 35px rgba(0,0,0,0.25)", verifier);
        Assert.Contains("drop-shadow-(--my-drop-shadow)", verifier);
        Assert.Contains("FRONTENDCSHARP_FECSX_COMPLETION_LOG", verifier);
    }

    [Test]
    public void SolutionRenameIntegrationUsesAuthoredCSharpAndFecsxFilesOnly()
    {
        var verifier = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioSolutionRename.cs"));

        Assert.Contains("Refactor.Rename", verifier);
        Assert.Contains("RenamedProps", verifier);
        Assert.Contains("Program.cs", verifier);
        Assert.Contains("First.fecsx", verifier);
        Assert.Contains("Second.fecsx", verifier);
        Assert.Contains("__FecsxEditorProjection", verifier);
        Assert.Contains(".editor.g.cs", verifier);
        Assert.Contains("File.SaveAll", verifier);
        Assert.Contains("Directory.Delete(RootDirectory, recursive: true)", verifier);
    }

    [Test]
    public void CSharpVsixInstallationRetriesTheRegistrationRefreshRace()
    {
        var installer = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Debugger",
            "Tests",
            "Integration",
            "VisualStudioVsixInstaller.cs"));

        Assert.Contains("devenv.com", installer);
        Assert.Contains("/UpdateConfiguration", installer);
        Assert.Contains("attempt < 3", installer);
        Assert.Contains("VerifyFecsxRegistry", installer);
        Assert.Contains("[0, -1]", installer);
        Assert.Contains("result.ExitCode == 0", installer);
        Assert.Contains("VerifyFecsxInstalledFiles", installer);
        Assert.Contains("installed payload and MEF catalog", installer);
        Assert.Contains("VsRegEdit.exe", installer);
        Assert.Contains("ComponentModelCache", installer);
        Assert.Contains("Fecsx.pkgdef", installer);
        Assert.Contains("ShellFileAssociations\\.fecsx", installer);
        Assert.Contains("BindingPaths\\{dd37ad22-31f8-4dde-b7bd-5d1424d89a93}", installer);
        Assert.Contains("FecsxImages.imagemanifest", installer);
        Assert.Contains("FrontEndCSharp.Fecsx.VSIX", installer);
    }

    [Test]
    public void GrammarEmbedsCSharpOnlyAtTheDocumentAndExpressionSeams()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var text = grammar.RootElement.GetRawText();

        Assert.Equal("source.fecsx", grammar.RootElement.GetProperty("scopeName").GetString());
        Assert.Contains("meta.embedded.expression.cs", text);
        Assert.Contains("source.cs", text);
        Assert.Contains("entity.name.tag.fecsx", text);
        Assert.Contains("entity.other.attribute-name.fecsx", text);
        Assert.DoesNotContain("\"name\": \"meta.tag.fecsx\"", text);
        Assert.Contains("Microsoft/TypeScript-TmLanguage@3d2222f", text);
    }

    [Test]
    public void GrammarBalancesCSharpInterpolationBracesInsideAContainingJsxExpression()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var expression = grammar.RootElement.GetProperty("repository").GetProperty("jsx-expression");
        var repository = grammar.RootElement.GetProperty("repository");
        var weatherPanel = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "Demo",
            "FrontEndCSharp.ReactDemo",
            "Components",
            "WeatherPanel.fecsx"));
        var interpolatedAttribute = weatherPanel.IndexOf("className={$\"", StringComparison.Ordinal);
        var followingAttribute = weatherPanel.IndexOf("<div className=\"weather-head\">", StringComparison.Ordinal);

        Assert.True(interpolatedAttribute >= 0);
        Assert.True(followingAttribute > interpolatedAttribute);
        Assert.False(expression.TryGetProperty("applyEndPatternLast", out _));
        Assert.Equal(
            "#csharp-expression-content",
            expression.GetProperty("patterns")[0].GetProperty("include").GetString());
        var expressionPatterns = repository.GetProperty("csharp-expression-content").GetProperty("patterns").GetRawText();
        var interpolatedString = repository.GetProperty("csharp-interpolated-string");
        Assert.Contains("#csharp-interpolated-string", expressionPatterns, StringComparison.Ordinal);
        Assert.Contains("#csharp-balanced-braces", expressionPatterns, StringComparison.Ordinal);
        Assert.Contains("#csharp-string", expressionPatterns, StringComparison.Ordinal);
        Assert.Contains("#csharp-character", expressionPatterns, StringComparison.Ordinal);
        Assert.Contains("#csharp-line-comment", expressionPatterns, StringComparison.Ordinal);
        Assert.Contains("#csharp-block-comment", expressionPatterns, StringComparison.Ordinal);
        Assert.False(interpolatedString.TryGetProperty("name", out _));
        Assert.Contains(
            "string.quoted.double.interpolated.cs",
            interpolatedString.GetProperty("beginCaptures").GetRawText(),
            StringComparison.Ordinal);
        Assert.Contains(
            "#csharp-balanced-braces",
            interpolatedString.GetProperty("patterns").GetRawText(),
            StringComparison.Ordinal);
    }

    [Test]
    public void ThemeUsesVisualStudiosTsxClassificationsForMarkup()
    {
        var theme = XDocument.Load(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmTheme"));
        var text = theme.ToString();

        Assert.Contains("meta.jsx.children.fecsx", text);
        Assert.Contains("xml literal - text", text);
        Assert.Contains("entity.other.attribute-name.fecsx", text);
        Assert.Contains("HTML Attribute Name", text);
        Assert.Contains("entity.name.tag.fecsx", text);
        Assert.Contains("HTML Element Name", text);
        Assert.Contains("support.class.component.fecsx", text);
        Assert.Contains("<string>type</string>", text);
        Assert.DoesNotContain("<string>meta.tag.fecsx</string>", text);
        Assert.Contains("punctuation.definition.tag.fecsx", text);
        Assert.Contains("html operator", text);
        Assert.Contains("comment.line.double-slash.fecsx", text);
        Assert.Contains("<string>comment</string>", text);
    }

    [Test]
    public void GrammarClassifiesTheJsxEntityFormsAcceptedByTheLowerer()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var rule = grammar.RootElement.GetProperty("repository").GetProperty("jsx-entity");
        var pattern = Assert.IsType<string>(rule.GetProperty("match").GetString());
        var expression = new Regex("^(?:" + pattern + ")$", RegexOptions.CultureInvariant);

        Assert.Matches(expression, "&copy;");
        Assert.Matches(expression, "&#65;");
        Assert.Matches(expression, "&#x1F680;");
        Assert.DoesNotMatch(expression, "&#X41;");
        Assert.DoesNotMatch(expression, "&copy");
        Assert.Equal("constant.character.entity.fecsx", rule.GetProperty("name").GetString());
    }

    [Test]
    public void GrammarDistinguishesIntrinsicElementsFromComponentTagsLikeTsx()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var repository = grammar.RootElement.GetProperty("repository");
        var intrinsicRule = repository.GetProperty("jsx-intrinsic");
        var componentRule = repository.GetProperty("jsx-component");
        var intrinsicBegin = intrinsicRule.GetProperty("begin").GetString();
        var componentBegin = componentRule.GetProperty("begin").GetString();

        Assert.NotNull(intrinsicBegin);
        Assert.NotNull(componentBegin);
        var intrinsic = Regex.Match("<section>", intrinsicBegin!);
        var component = Regex.Match("<Observed.FocusCard>", componentBegin!);

        Assert.Equal("section", intrinsic.Groups[2].Value);
        Assert.Equal("Observed.FocusCard", component.Groups[2].Value);
        Assert.Equal("entity.name.tag.fecsx", intrinsicRule.GetProperty("beginCaptures").GetProperty("2").GetProperty("name").GetString());
        Assert.Equal("support.class.component.fecsx", componentRule.GetProperty("beginCaptures").GetProperty("2").GetProperty("name").GetString());
    }

    [TestCase("jsx-root-intrinsic", "</section>", "entity.name.tag.fecsx")]
    [TestCase("jsx-root-component", "</MotionElement>", "support.class.component.fecsx")]
    [TestCase("jsx-intrinsic", "</section>", "entity.name.tag.fecsx")]
    [TestCase("jsx-component", "</MotionElement>", "support.class.component.fecsx")]
    public void GrammarClassifiesClosingTagNames(string ruleName, string closingTag, string expectedScope)
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var rule = grammar.RootElement.GetProperty("repository").GetProperty(ruleName);
        var end = rule.GetProperty("end").GetString();

        Assert.NotNull(end);
        var match = Regex.Match(closingTag, end!);

        Assert.True(match.Success, $"The {ruleName} end pattern did not recognize '{closingTag}'.");
        Assert.Equal(closingTag, match.Value);
        Assert.Equal(closingTag[2..^1], match.Groups[3].Value);
        Assert.Equal(expectedScope, rule.GetProperty("endCaptures").GetProperty("3").GetProperty("name").GetString());
    }

    [Test]
    public void GrammarUsesLineLocalRenderStateBeforeMatchingRootMarkup()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var root = grammar.RootElement;
        var repository = root.GetProperty("repository");
        var renderExpression = repository.GetProperty("jsx-render-expression");
        var renderBegin = renderExpression.GetProperty("begin").GetString();
        var renderPatterns = renderExpression.GetProperty("patterns").GetRawText();
        var componentBegin = repository.GetProperty("jsx-root-component").GetProperty("begin").GetString();

        Assert.Contains("#jsx-render-expression", root.GetProperty("patterns").GetRawText(), StringComparison.Ordinal);
        Assert.NotNull(renderBegin);
        Assert.Matches(new Regex(renderBegin!), "public static ReactNode Card() =>");
        Assert.Matches(new Regex(renderBegin!), "public static ReactNode Card() => <Panel>");
        Assert.Matches(new Regex(renderBegin!), "public static ReactNode Card() => MobXReact.Observe(() => <Panel>");
        Assert.NotNull(componentBegin);
        Assert.Matches(new Regex(componentBegin!), "<Panel ClassName=\"panel\"");
        Assert.DoesNotContain("=>", componentBegin, StringComparison.Ordinal);
        Assert.Contains("#jsx-root-component", renderPatterns, StringComparison.Ordinal);
        Assert.Contains("#jsx-root-intrinsic", renderPatterns, StringComparison.Ordinal);
        Assert.Contains("#jsx-root-fragment", renderPatterns, StringComparison.Ordinal);
    }

    [Test]
    public void GrammarEntersMarkupForABlockBodyReturnAndThenReturnsToCSharp()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var root = grammar.RootElement;
        var repository = root.GetProperty("repository");
        var returnStatement = repository.GetProperty("jsx-return-statement");
        var begin = returnStatement.GetProperty("begin").GetString();
        var end = returnStatement.GetProperty("end").GetString();
        var patterns = returnStatement.GetProperty("patterns").GetRawText();

        Assert.Contains("#jsx-return-statement", root.GetProperty("patterns").GetRawText(), StringComparison.Ordinal);
        Assert.NotNull(begin);
        Assert.Matches(new Regex(begin!), "    return <Panel>");
        Assert.Matches(new Regex(begin!), "    return MobXReact.Observe(() => <Panel>");
        Assert.Equal("keyword.control.flow.return.fecsx", returnStatement.GetProperty("beginCaptures").GetProperty("1").GetProperty("name").GetString());
        Assert.NotNull(end);
        Assert.Matches(new Regex(end!), ";\n}");
        Assert.Contains("#jsx-root-component", patterns, StringComparison.Ordinal);
        Assert.Contains("#jsx-root-intrinsic", patterns, StringComparison.Ordinal);
        Assert.Contains("#jsx-root-fragment", patterns, StringComparison.Ordinal);
    }

    [Test]
    public void GrammarConsumesCommentedComponentsBeforeLookingForTheRenderArrow()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var root = grammar.RootElement;
        var patterns = root.GetProperty("patterns");
        var commentInclude = patterns[0].GetProperty("include").GetString();
        var comment = root.GetProperty("repository").GetProperty("csharp-line-comment");
        var commentPattern = comment.GetProperty("match").GetString();
        var renderBoundary = root.GetProperty("repository")
            .GetProperty("jsx-render-expression")
            .GetProperty("begin")
            .GetString();

        Assert.Equal("#csharp-line-comment", commentInclude);
        Assert.Equal("comment.line.double-slash.fecsx", comment.GetProperty("name").GetString());
        Assert.NotNull(commentPattern);
        Assert.Equal("// public static ReactNode App() =>", Regex.Match("// public static ReactNode App() =>", commentPattern!).Value);
        Assert.Equal("//     <div />", Regex.Match("//     <div />", commentPattern!).Value);
        Assert.NotNull(renderBoundary);
        Assert.Matches(new Regex(renderBoundary!), "public static ReactNode App() => <div>");
    }

    [Test]
    public void GrammarClosesEachRootBeforeReturningToCSharpForTheNextComponent()
    {
        using var grammar = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "fecsx.tmLanguage.json")));
        var repository = grammar.RootElement.GetProperty("repository");
        var intrinsicRoot = repository.GetProperty("jsx-root-intrinsic");
        var componentRoot = repository.GetProperty("jsx-root-component");
        var intrinsicEnd = intrinsicRoot.GetProperty("end").GetString();
        var componentEnd = componentRoot.GetProperty("end").GetString();
        var rootPatterns = grammar.RootElement.GetProperty("patterns").GetRawText();

        Assert.DoesNotContain("\\2", intrinsicEnd, StringComparison.Ordinal);
        Assert.DoesNotContain("\\2", componentEnd, StringComparison.Ordinal);
        Assert.Matches(new Regex(componentRoot.GetProperty("begin").GetString()!), "<Panel>");
        Assert.Contains("source.cs", rootPatterns, StringComparison.Ordinal);
        Assert.DoesNotContain("(?=\\z)", intrinsicRoot.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("(?=\\z)", componentRoot.GetRawText(), StringComparison.Ordinal);
    }

    [Test]
    public void LanguageConfigurationIsValidAndPairsCSharpDelimiters()
    {
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDirectory, "Assets", "language-configuration.json")));
        var root = configuration.RootElement;

        Assert.Equal("//", root.GetProperty("comments").GetProperty("lineComment").GetString());
        Assert.Contains(root.GetProperty("brackets").EnumerateArray(), pair => pair[0].GetString() == "{" && pair[1].GetString() == "}");
    }

    [Test]
    public void ManifestPublishesTheMefHostAndTextMateRegistration()
    {
        XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
        var manifest = XDocument.Load(Path.Combine(ProjectDirectory, "source.extension.vsixmanifest"));
        var assetTypes = manifest.Descendants(ns + "Asset")
            .Select(element => (string?)element.Attribute("Type"))
            .ToArray();

        Assert.Contains("Microsoft.VisualStudio.MefComponent", assetTypes);
        Assert.Contains("Microsoft.VisualStudio.VsPackage", assetTypes);
        Assert.Contains("Microsoft.VisualStudio.ImageManifest", assetTypes);
        Assert.Contains("Microsoft.VisualStudio.ProjectTemplate", assetTypes);
    }

    [Test]
    public void BuiltVsixContainsTheHostSharedServicesAndLicensedAssets()
    {
        Assert.False(
            File.Exists(Path.Combine(ProjectDirectory, "extension.vsixmanifest")),
            "The detokenized VSIX manifest is generated output and must remain under obj.");
        var path = Path.Combine(ProjectDirectory, "bin", "Debug", "net472", "FrontEndCSharp.Fecsx.VSIX.vsix");
        Assert.True(File.Exists(path), $"Expected the project build to create {path}.");

        using var archive = ZipFile.OpenRead(path);
        var entries = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("FrontEndCSharp.Fecsx.VSIX.dll", entries);
        Assert.Contains("FrontEndCSharp.Fecsx.Editor.dll", entries);
        Assert.Contains("FrontEndCSharp.Fecsx.dll", entries);
        Assert.Contains("Fecsx.pkgdef", entries);
        Assert.Contains("Assets/fecsx.tmLanguage.json", entries);
        Assert.Contains("Assets/fecsx.tmTheme", entries);
        Assert.Contains("Assets/language-configuration.json", entries);
        Assert.Contains("FecsxImages.imagemanifest", entries);
        Assert.Contains("THIRD-PARTY-NOTICES.txt", entries);
        Assert.Contains("ProjectTemplates/templateManifest0.1033.vstman", entries);

        const string templatePrefix = "ProjectTemplates/CSharp/1033/FrontEndCSharp.Fecsx/";
        foreach (var required in new[]
        {
            "FrontEndCSharp.Fecsx.vstemplate",
            "Host/Host.vstemplate",
            "Host/Host.csproj",
            "Host/Program.cs",
            "Host/Properties/launchSettings.json",
            "Host/wwwroot/index.html",
            "Host/wwwroot/site.css",
            "Browser/Browser.vstemplate",
            "Browser/Browser.csproj",
            "Browser/Program.cs",
            "Browser/AppProps.cs",
            "Browser/App.fecsx",
        })
        {
            Assert.Contains(templatePrefix + required, entries);
        }

        var browserProjectEntry = archive.Entries.Single(entry => string.Equals(
            entry.FullName.Replace('\\', '/'),
            templatePrefix + "Browser/Browser.csproj",
            StringComparison.OrdinalIgnoreCase));
        using var projectReader = new StreamReader(browserProjectEntry.Open());
        var browserProject = projectReader.ReadToEnd();
        Assert.Contains("FrontEndCSharp.Fecsx.Build", browserProject);
        Assert.Contains("Interop.JS.React.fecs", browserProject);
        Assert.DoesNotContain("<ProjectReference", browserProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..\\..\\Compiler", browserProject, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..\\..\\RuntimeOverrides", browserProject, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory !=  null && !File.Exists(Path.Combine(directory.FullName, "FrontEndCSharp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find the FrontEndCSharp repository root.");
    }
}
