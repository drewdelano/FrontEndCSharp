using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace FrontEndCSharp.Fecsx.VSIX;

internal static class FecsxContentType
{
    public const string Name = "FrontEndCSharp.Fecsx";

#pragma warning disable 169, 649
    [Export(typeof(ContentTypeDefinition))]
    [Name(Name)]
    [BaseDefinition("code++")]
    private static readonly ContentTypeDefinition? Definition;

    [Export(typeof(FileExtensionToContentTypeDefinition))]
    [ContentType(Name)]
    [FileExtension(".fecsx")]
    private static readonly FileExtensionToContentTypeDefinition? Extension;
#pragma warning restore 169, 649
}
